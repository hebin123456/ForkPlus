using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Tests
{
	/// <summary>最小 OpenAI 兼容 API 服务器（模块24 测试基建，2026-09-06，LfsLocksApiServer 同款模式）：
	/// ForkPlus 的 AI 功能（提交拆分/代码审查/开发助手/模型下拉）全部经 OpenAiService 走
	/// HTTP：服务地址取自 AiReviewServiceUrl（NormalizeServiceUrl 剥 /v1 后缀），鉴权为
	/// Authorization: Bearer &lt;AiReviewApiKey&gt;。本类在 127.0.0.1 随机端口实现三个端点：
	///   GET  /v1/models → {"data":[{"id":...},...]}（InitializeModelComboBox 后台拉取）
	///   POST /v1/chat/completions（body "stream":true）→ SSE：data: {"choices":[{"delta":{"content":&lt;分片&gt;}}]}
	///     × N + data: [DONE]（Connection.RequestStream 逐行读 + ParseSseLine 解析 delta.content）
	///   POST /v1/chat/completions（body "stream":false）→ {"choices":[{"message":{"content":...}}]}
	///     （OpenAiResponse.Decode 读 choices[0].message.content）
	/// 回复内容由 ChatResponder（Func&lt;请求body, 回复文本&gt;）决定——按 prompt 关键字分发
	/// 各功能的 canned 回复；默认回退 DefaultReply。流式响应固定拆 3 片（真实走 chunk 管线）。
	/// 所有请求（路径/鉴权头/请求体）入 Captured 供断言（鉴权头、模型名、多轮历史）。
	/// 用法：using var server = OpenAiStubServer.Start(responder);
	///   ForkPlusSettings.Default.AiReviewServiceUrl = server.BaseUrl;（其余三项见 Configure()）。</summary>
	internal sealed class OpenAiStubServer : IDisposable
	{
		/// <summary>已捕获的 chat 请求体（按到达顺序；线程安全——后台 job 线程写、测试线程读）。</summary>
		public readonly List<string> ChatRequestBodies = new List<string>();

		/// <summary>已捕获的 chat 请求 Authorization 头（与 ChatRequestBodies 同序）。</summary>
		public readonly List<string> ChatAuthorizationHeaders = new List<string>();

		/// <summary>GET /v1/models 被请求的次数。</summary>
		public int ModelsRequestCount;

		private readonly HttpListener _listener;
		private readonly Thread _thread;
		private volatile bool _running = true;
		private readonly object _captureLock = new object();

		/// <summary>chat 回复选择器：入参=请求 body JSON，返回 assistant 文本。可随时替换（用例内多阶段）。</summary>
		public Func<string, string> ChatResponder { get; set; }

		/// <summary>Responder 未命中时的默认回复（默认 "Hello from stub."）。</summary>
		public string DefaultReply { get; set; } = "Hello from stub.";

		public string BaseUrl { get; }

		private OpenAiStubServer(HttpListener listener, int port)
		{
			_listener = listener;
			BaseUrl = "http://127.0.0.1:" + port;
			_thread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "OpenAiStubServer"
			};
			_thread.Start();
		}

		/// <summary>启动（随机空闲端口）。responder 为 null 时恒返回 DefaultReply。</summary>
		public static OpenAiStubServer Start(Func<string, string> responder = null)
		{
			int port;
			using (var probe = new TcpListener(IPAddress.Loopback, 0))
			{
				probe.Start();
				port = ((IPEndPoint)probe.LocalEndpoint).Port;
				probe.Stop();
			}
			var listener = new HttpListener();
			listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
			listener.Start();
			return new OpenAiStubServer(listener, port) { ChatResponder = responder };
		}

		private void AcceptLoop()
		{
			while (_running)
			{
				HttpListenerContext context;
				try
				{
					context = _listener.GetContext();
				}
				catch (Exception)
				{
					break; // listener 已 Stop/Close（Dispose 路径）
				}
				try
				{
					Handle(context);
				}
				catch (Exception)
				{
					// 单个请求处理失败不拖垮服务（连接中途断开等）
				}
			}
		}

		private void Handle(HttpListenerContext context)
		{
			string method = context.Request.HttpMethod;
			string path = context.Request.Url.AbsolutePath;
			if (method == "GET" && (path == "/v1/models" || path == "/models"))
			{
				Interlocked.Increment(ref ModelsRequestCount);
				RespondJson(context, 200, "{\"object\":\"list\",\"data\":[{\"id\":\"stub-model-a\"},{\"id\":\"stub-model-b\"},{\"id\":\"stub-model-c\"}]}");
			}
			else if (method == "POST" && (path == "/v1/chat/completions" || path == "/chat/completions"))
			{
				string body = ReadBody(context);
				lock (_captureLock)
				{
					ChatRequestBodies.Add(body);
					ChatAuthorizationHeaders.Add(context.Request.Headers["Authorization"] ?? "");
				}
				string reply = ResolveReply(body);
				bool streaming = body.Contains("\"stream\":true", StringComparison.OrdinalIgnoreCase)
					|| body.Contains("\"stream\": true", StringComparison.OrdinalIgnoreCase);
				if (streaming)
				{
					RespondSse(context, reply);
				}
				else
				{
					RespondJson(context, 200, ChatCompletionJson(reply));
				}
			}
			else
			{
				RespondJson(context, 404, "{\"error\":{\"message\":\"not found\"}}");
			}
		}

		private string ResolveReply(string body)
		{
			Func<string, string> responder = ChatResponder;
			if (responder != null)
			{
				try
				{
					string reply = responder(body);
					if (reply != null)
					{
						return reply;
					}
				}
				catch (Exception)
				{
					// responder 抛异常按未命中处理，回退 DefaultReply
				}
			}
			return DefaultReply;
		}

		/// <summary>非流式 chat/completions 响应体（OpenAiResponse.Decode 兼容形态）。</summary>
		private static string ChatCompletionJson(string content)
		{
			return "{\"id\":\"stub-1\",\"object\":\"chat.completion\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":" + JToken.FromObject(content).ToString(Newtonsoft.Json.Formatting.None) + "},\"finish_reason\":\"stop\"}]}";
		}

		/// <summary>流式 SSE 响应：把 reply 拆 3 片逐片下行（真实走 chunk 管线），[DONE] 终止。</summary>
		private static void RespondSse(HttpListenerContext context, string reply)
		{
			context.Response.StatusCode = 200;
			context.Response.ContentType = "text/event-stream";
			context.Response.SendChunked = true;
			string[] chunks = SplitIntoChunks(reply, 3);
			try
			{
				using (var writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false)))
				{
					foreach (string chunk in chunks)
					{
						string data = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":" + JToken.FromObject(chunk).ToString(Newtonsoft.Json.Formatting.None) + "}}]}";
						writer.Write("data: " + data + "\n\n");
						writer.Flush();
					}
					writer.Write("data: [DONE]\n\n");
					writer.Flush();
				}
			}
			catch (Exception)
			{
				// 客户端取消（Stop 按钮）会中途断开，忽略
			}
			try
			{
				context.Response.OutputStream.Close();
			}
			catch (Exception)
			{
			}
		}

		/// <summary>把文本均分成 count 片（空串回复产出单个空片——ParseSseLine 忽略空 delta）。</summary>
		private static string[] SplitIntoChunks(string text, int count)
		{
			if (string.IsNullOrEmpty(text))
			{
				return new[] { text ?? "" };
			}
			count = Math.Min(count, text.Length);
			var parts = new string[count];
			int baseSize = text.Length / count;
			int remainder = text.Length % count;
			int position = 0;
			for (int i = 0; i < count; i++)
			{
				int size = baseSize + (i < remainder ? 1 : 0);
				parts[i] = text.Substring(position, size);
				position += size;
			}
			return parts;
		}

		private static void RespondJson(HttpListenerContext context, int status, string json)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(json);
			context.Response.StatusCode = status;
			context.Response.ContentType = "application/json";
			context.Response.ContentLength64 = bytes.Length;
			context.Response.OutputStream.Write(bytes, 0, bytes.Length);
			context.Response.OutputStream.Close();
		}

		private static string ReadBody(HttpListenerContext context)
		{
			using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
			{
				return reader.ReadToEnd();
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
			try
			{
				_listener.Close();
			}
			catch (Exception)
			{
			}
		}
	}
}
