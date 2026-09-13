using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ForkPlus.AutoUpdater;
using Xunit;

namespace ForkPlus.AutoUpdaterTests
{
	/// <summary>
	/// 更新包下载测试：本地桩服务器验证成功下载（进度回调 + 落盘内容一致）、过小
	/// 载荷拦截（门户劫持防护）、HTTP 失败重试后抛错、取消语义。
	/// 桩服务器用裸 TcpListener + 原始 HTTP/1.1 应答（对齐主测试工程
	/// UpdateCheckStubServer 的口径：HttpListener 不暴露 LocalEndpoint 也不支持
	/// port 0，而"先探测空闲端口再绑定"存在 TOCTOU 竞态——直接绑 port 0 让内核
	/// 分配且 listener 立即持有，无竞态窗口）。
	/// </summary>
	public class UpdateDownloaderTests : IDisposable
	{
		/// <summary>桩服务器：GET 任意路径返回固定状态码 + 字节载荷。</summary>
		private sealed class StubServer : IDisposable
		{
			private readonly TcpListener _listener;

			private readonly Thread _thread;

			private volatile bool _running = true;

			public StubServer(int statusCode, byte[] payload)
			{
				_listener = new TcpListener(IPAddress.Loopback, 0);
				_listener.Start();
				BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/";
				_thread = new Thread((ThreadStart)delegate
				{
					AcceptLoop(statusCode, payload);
				})
				{
					IsBackground = true,
					Name = "UpdateDownloaderStubServer"
				};
				_thread.Start();
			}

			public string BaseUrl { get; }

			private void AcceptLoop(int statusCode, byte[] payload)
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
						Handle((TcpClient)state, statusCode, payload);
					}, client);
				}
			}

			private static void Handle(TcpClient client, int statusCode, byte[] payload)
			{
				try
				{
					using (client)
					using (NetworkStream stream = client.GetStream())
					{
						ReadRequestHeaders(stream);
						byte[] body = statusCode == 200 ? payload : new byte[0];
						string reason = statusCode == 200 ? "OK" : "Error";
						byte[] header = Encoding.ASCII.GetBytes(
							"HTTP/1.1 " + statusCode + " " + reason + "\r\n"
							+ "Content-Type: application/zip\r\n"
							+ "Content-Length: " + body.Length + "\r\n"
							+ "Connection: close\r\n\r\n");
						stream.Write(header, 0, header.Length);
						stream.Write(body, 0, body.Length);
						stream.Flush();
					}
				}
				catch (Exception)
				{
					// 客户端取消/超时断开，忽略
				}
			}

			/// <summary>读到空行（\r\n\r\n）即认为请求头结束（HttpClient GET 无请求体）。</summary>
			private static void ReadRequestHeaders(NetworkStream stream)
			{
				byte[] buffer = new byte[4096];
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

		private readonly string _root;

		public UpdateDownloaderTests()
		{
			_root = Path.Combine(Path.GetTempPath(), "fpau-dl-" + Guid.NewGuid().ToString("N").Substring(0, 10));
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch (Exception)
			{
			}
		}

		private static byte[] MakePayload(int size)
		{
			byte[] payload = new byte[size];
			for (int i = 0; i < size; i++)
			{
				payload[i] = (byte)(i % 251);
			}
			return payload;
		}

		[Fact]
		public void Download_Success_WritesFileAndReportsProgress()
		{
			byte[] payload = MakePayload(300000);
			using (StubServer server = new StubServer(200, payload))
			{
				string zipPath = Path.Combine(_root, "update.zip");
				UpdateDownloader downloader = new UpdateDownloader(server.BaseUrl + "pkg.zip", zipPath, minimumBytes: 1024L);
				long lastReceived = -1;
				long lastTotal = -1;
				bool downloaded = downloader.Download(
					delegate (long received, long total)
					{
						lastReceived = received;
						lastTotal = total;
					},
					CancellationToken.None,
					maxAttempts: 2);
				Assert.True(downloaded);
				Assert.Equal(zipPath, downloader.ZipPath);
				Assert.True(File.Exists(zipPath));
				Assert.Equal(payload, File.ReadAllBytes(zipPath));
				Assert.Equal(payload.Length, lastReceived);
				Assert.Equal(payload.Length, lastTotal);
			}
		}

		[Fact]
		public void Download_TooSmallPayload_FailsImmediately()
		{
			// 门户劫持防护：几百字节的 HTML 错误页不该当 zip 写下来
			byte[] tiny = MakePayload(512);
			using (StubServer server = new StubServer(200, tiny))
			{
				string zipPath = Path.Combine(_root, "tiny.zip");
				UpdateDownloader downloader = new UpdateDownloader(server.BaseUrl + "page", zipPath, minimumBytes: 65536L);
				Assert.Throws<InvalidOperationException>(delegate
				{
					downloader.Download(null, CancellationToken.None, maxAttempts: 1);
				});
			}
		}

		[Fact]
		public void Download_HttpError_RetriesThenThrows()
		{
			using (StubServer server = new StubServer(404, new byte[0]))
			{
				string zipPath = Path.Combine(_root, "never.zip");
				UpdateDownloader downloader = new UpdateDownloader(server.BaseUrl + "missing.zip", zipPath, minimumBytes: 1L);
				InvalidOperationException ex = Assert.Throws<InvalidOperationException>(delegate
				{
					downloader.Download(null, CancellationToken.None, maxAttempts: 2);
				});
				Assert.Contains("2 attempts", ex.Message);
				Assert.False(File.Exists(zipPath), "失败路径不应留下半截文件");
			}
		}

		[Fact]
		public void Download_Precancelled_ReturnsFalseWithoutRequest()
		{
			using (StubServer server = new StubServer(200, MakePayload(300000)))
			{
				string zipPath = Path.Combine(_root, "cancelled.zip");
				UpdateDownloader downloader = new UpdateDownloader(server.BaseUrl + "pkg.zip", zipPath, minimumBytes: 1024L);
				using (CancellationTokenSource cts = new CancellationTokenSource())
				{
					cts.Cancel();
					bool downloaded = downloader.Download(null, cts.Token, maxAttempts: 2);
					Assert.False(downloaded);
				}
				Assert.False(File.Exists(zipPath));
			}
		}
	}
}
