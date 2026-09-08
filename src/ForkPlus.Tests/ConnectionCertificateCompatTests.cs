using System;
using System.Net.Http;
using System.Net.Security;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// AI 服务证书兼容回归测试：
	/// 部分机器的推理服务（本地 Ollama/vLLM/one-api 或企业内网网关）用自签名 HTTPS，
	/// 刷新模型报 "The remote certificate is invalid because of errors in the certificate
	/// chain: UntrustedRoot"。修复口径：仅对用户配置的 AiReviewServiceUrl 同 host 的请求放宽
	/// 证书校验，其余目标（GitHub 等）仍严格校验。
	/// 单元部分直接断言判定方法；端到端部分用自签 HTTPS 桩服务器走真实 TLS 握手。
	/// 同属 "HeadlessAvalonia" 集合：与 E2e24AiTests 串行，避免全局 ForkPlusSettings 竞态。
	/// </summary>
	[Collection("HeadlessAvalonia")]
	public class ConnectionCertificateCompatTests
	{
		// ============================ 单元：判定方法 ============================

		[Fact]
		public void ShouldAcceptCertificate_NoErrors_AlwaysAccepted()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = "";
				// 证书校验无错误时任意目标都应放行（与 .NET 默认行为一致）
				Assert.True(Connection.ShouldAcceptCertificate(new Uri("https://api.github.com/repos"), SslPolicyErrors.None));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		[Fact]
		public void ShouldAcceptCertificate_AiEndpointHostWithUntrustedRoot_Accepted()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = "https://ai.internal.corp:8443/v1";
				Assert.True(Connection.ShouldAcceptCertificate(
					new Uri("https://ai.internal.corp:8443/v1/models"),
					SslPolicyErrors.RemoteCertificateChainErrors));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		[Fact]
		public void ShouldAcceptCertificate_OtherHostWithUntrustedRoot_Rejected()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = "https://ai.internal.corp:8443";
				// GitHub 等非 AI 目标即使出现证书错误也必须保持严格校验
				Assert.False(Connection.ShouldAcceptCertificate(
					new Uri("https://api.github.com/repos/hebin123456/ForkPlus/releases/latest"),
					SslPolicyErrors.RemoteCertificateChainErrors));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		[Fact]
		public void ShouldAcceptCertificate_HostMatchIsCaseInsensitive()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = "https://AI.Internal.Corp:8443";
				Assert.True(Connection.ShouldAcceptCertificate(
					new Uri("https://ai.internal.corp:8443/v1/models"),
					SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		[Fact]
		public void ShouldAcceptCertificate_HostOnlyConfig_Accepted()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				// 用户只填 "host:port"（无 scheme）的常见写法也应识别
				ForkPlusSettings.Default.AiReviewServiceUrl = "127.0.0.1:8443";
				Assert.True(Connection.ShouldAcceptCertificate(
					new Uri("https://127.0.0.1:8443/v1/models"),
					SslPolicyErrors.RemoteCertificateChainErrors));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		[Fact]
		public void ShouldAcceptCertificate_EmptyConfigOrNullUri_Rejected()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			try
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = "";
				Assert.False(Connection.ShouldAcceptCertificate(new Uri("https://127.0.0.1:8443/v1/models"), SslPolicyErrors.RemoteCertificateChainErrors));
				Assert.False(Connection.ShouldAcceptCertificate(null, SslPolicyErrors.RemoteCertificateChainErrors));
			}
			finally
			{
				ForkPlusSettings.Default.AiReviewServiceUrl = saved;
			}
		}

		// ============================ 端到端：真实 TLS 握手 ============================

		[Fact]
		public void ListModels_SelfSignedHttpsAiEndpoint_Succeeds()
		{
			string savedUrl = ForkPlusSettings.Default.AiReviewServiceUrl;
			string savedKey = ForkPlusSettings.Default.AiReviewApiKey;
			string savedModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			int savedRetry = ForkPlusSettings.Default.AiReviewRetryCount;
			int savedTimeout = ForkPlusSettings.Default.AiReviewTimeoutSeconds;
			using (var server = SelfSignedHttpsStubServer.Start())
			{
				// 把自签 HTTPS 桩配置为推理服务地址——复现刷新模型报 UntrustedRoot 的场景
				ForkPlusSettings.Default.AiReviewServiceUrl = server.BaseUrl;
				ForkPlusSettings.Default.AiReviewApiKey = "stub-key";
				ForkPlusSettings.Default.AiReviewSelectedModel = "stub-model-a";
				ForkPlusSettings.Default.AiReviewRetryCount = 0;
				ForkPlusSettings.Default.AiReviewTimeoutSeconds = 15;
				try
				{
					ServiceResult<string[]> result = OpenAiService.CreateFromAiReviewSettings().ListModels();
					Assert.True(result.Succeeded, "AI 端点为自签 HTTPS 时刷新模型应成功，实际错误: "
						+ (result.Error?.FriendlyMessage ?? "<null>"));
					Assert.Equal(3, result.Result.Length);
					Assert.Contains("stub-model-a", result.Result);
					Assert.True(server.RequestCount >= 1, "桩服务器应至少收到一次请求");
				}
				finally
				{
					ForkPlusSettings.Default.AiReviewServiceUrl = savedUrl;
					ForkPlusSettings.Default.AiReviewApiKey = savedKey;
					ForkPlusSettings.Default.AiReviewSelectedModel = savedModel;
					ForkPlusSettings.Default.AiReviewRetryCount = savedRetry;
					ForkPlusSettings.Default.AiReviewTimeoutSeconds = savedTimeout;
				}
			}
		}

		[Fact]
		public void Request_SelfSignedHttpsNonAiEndpoint_FailsWithCertificateError()
		{
			string saved = ForkPlusSettings.Default.AiReviewServiceUrl;
			using (var server = SelfSignedHttpsStubServer.Start())
			{
				// AI 服务地址指向别处：同一自签端点必须仍然被严格校验拒绝（负控）
				ForkPlusSettings.Default.AiReviewServiceUrl = "https://api.openai.com";
				try
				{
					Connection connection = new Connection(server.BaseUrl, null, 15);
					Connection.HttpRequestResult result = connection.Request(new ApiRequest(HttpMethod.Get, "/v1/models"));
					Assert.False(result.Succeeded, "非 AI 目标的自签证书应被拒绝");
					string message = result.Error?.FriendlyMessage ?? "";
					Assert.True(
						message.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
						|| message.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0,
						"错误信息应指向证书校验失败，实际: " + message);
				}
				finally
				{
					ForkPlusSettings.Default.AiReviewServiceUrl = saved;
				}
			}
		}
	}
}
