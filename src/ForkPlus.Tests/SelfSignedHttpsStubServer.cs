using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace ForkPlus.Tests
{
	/// <summary>自签名 HTTPS 桩服务器（AI 证书兼容测试基建，OpenAiStubServer 同款模式）：
	/// 用 TcpListener + SslStream（运行时自生成的自签名证书）实现裸 HTTP/1.1，
	/// 复现"本地自建 AI 推理服务用自签 HTTPS"的场景——客户端 .NET 默认校验必报
	/// UntrustedRoot（证书链不受信）。
	/// 仅需应答 Connection.Request 发出的一次 GET（读完请求头即回 200 JSON）。
	/// 用法：using var server = SelfSignedHttpsStubServer.Start();
	///   ForkPlusSettings.Default.AiReviewServiceUrl = server.BaseUrl; 后走真实 TLS 握手。</summary>
	internal sealed class SelfSignedHttpsStubServer : IDisposable
	{
		/// <summary>已完成的 TLS 请求数（线程安全——服务器线程写、测试线程读）。</summary>
		public int RequestCount;

		private readonly TcpListener _listener;
		private readonly X509Certificate2 _certificate;
		private volatile bool _running = true;

		/// <summary>HTTPS 基地址（https://127.0.0.1:port）。</summary>
		public string BaseUrl { get; }

		private SelfSignedHttpsStubServer(TcpListener listener, int port, X509Certificate2 certificate)
		{
			_listener = listener;
			_certificate = certificate;
			BaseUrl = "https://127.0.0.1:" + port;
			Thread acceptThread = new Thread(AcceptLoop)
			{
				IsBackground = true,
				Name = "SelfSignedHttpsStubServer"
			};
			acceptThread.Start();
		}

		public static SelfSignedHttpsStubServer Start()
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
			return new SelfSignedHttpsStubServer(listener, port, CreateSelfSignedCertificate());
		}

		/// <summary>运行时自生成自签名证书（CN=127.0.0.1）。
		/// Linux 上 CertificateRequest.CreateSelfSigned 产出的私钥是 ephemeral，
		/// 导出 PFX 再重新导入后 SslStream 才能可靠地用它完成服务端握手。</summary>
		private static X509Certificate2 CreateSelfSignedCertificate()
		{
			using RSA rsa = RSA.Create(2048);
			var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
			return new X509Certificate2(ephemeral.Export(X509ContentType.Pfx));
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
					break; // listener 已 Stop/Close（Dispose 路径）
				}
				ThreadPool.QueueUserWorkItem(delegate (object state)
				{
					HandleClient((TcpClient)state);
				}, client);
			}
		}

		private void HandleClient(TcpClient client)
		{
			try
			{
				using (client)
				using (SslStream ssl = new SslStream(client.GetStream(), false))
				{
					ssl.AuthenticateAsServer(_certificate, clientCertificateRequired: false, SslProtocols.None, checkCertificateRevocation: false);
					ReadRequestHeaders(ssl);
					Interlocked.Increment(ref RequestCount);
					RespondModels(ssl);
				}
			}
			catch (Exception)
			{
				// 客户端拒绝证书（负控用例）会在握手阶段断开，属预期路径，忽略
			}
		}

		/// <summary>读到空行（\r\n\r\n）即认为请求头结束（Connection.Request 的 GET 无请求体）。</summary>
		private static void ReadRequestHeaders(SslStream ssl)
		{
			var buffer = new byte[4096];
			var received = new StringBuilder();
			while (!received.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
			{
				int read = ssl.Read(buffer, 0, buffer.Length);
				if (read <= 0)
				{
					return;
				}
				received.Append(Encoding.ASCII.GetString(buffer, 0, read));
			}
		}

		/// <summary>GET /v1/models 应答（与 OpenAiStubServer 相同的模型列表形态）。</summary>
		private static void RespondModels(SslStream ssl)
		{
			byte[] body = Encoding.UTF8.GetBytes(
				"{\"object\":\"list\",\"data\":[{\"id\":\"stub-model-a\"},{\"id\":\"stub-model-b\"},{\"id\":\"stub-model-c\"}]}");
			string header = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
				+ body.Length + "\r\nConnection: close\r\n\r\n";
			ssl.Write(Encoding.ASCII.GetBytes(header), 0, header.Length);
			ssl.Write(body, 0, body.Length);
			ssl.Flush();
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
