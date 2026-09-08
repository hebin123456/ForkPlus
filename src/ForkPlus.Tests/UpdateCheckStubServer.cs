using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ForkPlus.Tests
{
	/// <summary>更新检查桩服务器（302 兼容测试基建，裸 TcpListener + 原始 HTTP/1.1 应答）：
	/// 按 Responses 队列依次回放脚本化响应（状态行 ReasonPhrase 完全可控——HttpListener
	/// 不允许自定义 ReasonPhrase，这里能精确复现 "HTTP/1.1 302 AuthenticationRequired"）。
	/// 复现"直连出网被网关劫持返回 302 AuthenticationRequired / 门户 HTML / 黑洞超时"。
	/// UpdateChecker.CheckLatestRelease 直连失败后应回退走第二次尝试（loopback 等价直连重试），
	/// 第二次请求命中队列中的下一个响应。每个请求在线程池处理，延迟响应不阻塞后续连接。</summary>
	internal sealed class UpdateCheckStubServer : IDisposable
	{
		/// <summary>脚本化响应：按请求顺序出队回放。</summary>
		public sealed class StubResponse
		{
			public int StatusCode = 200;

			/// <summary>响应状态行 ReasonPhrase（复现 "302 AuthenticationRequired" 用）。</summary>
			public string ReasonPhrase;

			public string ContentType = "application/json";

			public string Body = "";

			/// <summary>非空时设置 Location 响应头（留空则 302 无法被跟随，客户端直接拿到 302 终态）。</summary>
			public string Location;

			/// <summary>响应前挂起毫秒数（模拟直连黑洞超时）。</summary>
			public int DelayMilliseconds;
		}

		/// <summary>已收到的请求数（线程安全——线程池写、测试线程读）。</summary>
		public int RequestCount;

		/// <summary>待回放的响应队列（用例内按顺序 Enqueue）。</summary>
		public readonly Queue<StubResponse> Responses = new Queue<StubResponse>();

		private readonly TcpListener _listener;
		private readonly Thread _thread;
		private volatile bool _running = true;

		public string BaseUrl { get; }

		private UpdateCheckStubServer(TcpListener listener, int port)
		{
			_listener = listener;
			BaseUrl = "http://127.0.0.1:" + port;
			_thread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "UpdateCheckStubServer"
			};
			_thread.Start();
		}

		public static UpdateCheckStubServer Start()
		{
			int port;
			using (var probe = new TcpListener(IPAddress.Loopback, 0))
			{
				probe.Start();
				port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();
			}
			var listener = new TcpListener(IPAddress.Loopback, port);
			listener.Start();
			return new UpdateCheckStubServer(listener, port);
		}

		private void AcceptLoop()
		{
			while (_running)
			{
				TcpClient client;
				try
				{
					client = _listener.AcceptTcpClient();
				}
				catch (Exception)
				{
					break; // listener 已 Stop（Dispose 路径）
				}
				ThreadPool.QueueUserWorkItem(delegate (object state)
				{
					Handle((TcpClient)state);
				}, client);
			}
		}

		private void Handle(TcpClient client)
		{
			try
			{
				using (client)
				using (NetworkStream stream = client.GetStream())
				{
					ReadRequestHeaders(stream);
					Interlocked.Increment(ref RequestCount);
					StubResponse response;
					lock (Responses)
					{
						response = Responses.Count > 0
							? Responses.Dequeue()
							: new StubResponse { StatusCode = 500, ReasonPhrase = "NoScriptedResponse", Body = "{}" };
					}
					if (response.DelayMilliseconds > 0)
					{
						Thread.Sleep(response.DelayMilliseconds);
					}
					byte[] bytes = Encoding.UTF8.GetBytes(BuildRawResponse(response));
					stream.Write(bytes, 0, bytes.Length);
					stream.Flush();
				}
			}
			catch (Exception)
			{
				// 客户端超时断开等场景，忽略
			}
		}

		/// <summary>读到空行（\r\n\r\n）即认为请求头结束（UpdateChecker 只发 GET，无请求体）。</summary>
		private static void ReadRequestHeaders(NetworkStream stream)
		{
			var buffer = new byte[4096];
			var received = new StringBuilder();
			while (!received.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
			{
				int read = stream.Read(buffer, 0, buffer.Length);
				if (read <= 0)
				{
					return;
				}
				received.Append(Encoding.ASCII.GetString(buffer, 0, read));
			}
		}

		/// <summary>拼原始 HTTP/1.1 响应（状态行 ReasonPhrase 完全可控）。</summary>
		private static string BuildRawResponse(StubResponse response)
		{
			string reason = string.IsNullOrEmpty(response.ReasonPhrase) ? DefaultReasonPhrase(response.StatusCode) : response.ReasonPhrase;
			var raw = new StringBuilder();
			raw.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(reason).Append("\r\n");
			raw.Append("Content-Type: ").Append(string.IsNullOrEmpty(response.ContentType) ? "application/json" : response.ContentType).Append("\r\n");
			if (!string.IsNullOrEmpty(response.Location))
			{
				raw.Append("Location: ").Append(response.Location).Append("\r\n");
			}
			raw.Append("Content-Length: ").Append(Encoding.UTF8.GetByteCount(response.Body ?? "")).Append("\r\n");
			raw.Append("Connection: close\r\n\r\n");
			raw.Append(response.Body ?? "");
			return raw.ToString();
		}

		private static string DefaultReasonPhrase(int statusCode)
		{
			switch (statusCode)
			{
				case 200: return "OK";
				case 302: return "Found";
				case 403: return "Forbidden";
				case 404: return "Not Found";
				case 500: return "Internal Server Error";
				default: return "Status";
			}
		}

		public void Dispose()
		{
			_running = false;
			try
			{
				_listener.Stop();
			}
			catch (Exception)
			{
			}
		}
	}
}
