using System;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 检查更新 302 兼容回归测试：
	/// 部分机器直连 api.github.com 被网关劫持，返回 "302 AuthenticationRequired"
	/// （要求门户登录），或跟随重定向后拿到门户 HTML / 直连黑洞超时。
	/// 修复口径：直连（UseProxy=false）失败后回退走系统代理重试一次；
	/// 两次都失败时以直连错误为主报错（与旧行为一致）。
	/// UpdateChecker 的 release 查询地址可注入本地桩服务器（loopback 等价直连重试），
	/// 响应按顺序回放：第 1 个 = 直连尝试的结果，第 2 个 = 代理回退尝试的结果。
	/// </summary>
	public class UpdateCheckerProxyFallbackTests
	{
		private const string ReleasePath = "/repos/hebin123456/ForkPlus/releases/latest";

		/// <summary>GitHub latest release 正常 JSON 响应。</summary>
		private static UpdateCheckStubServer.StubResponse SuccessResponse(string tagName)
		{
			return new UpdateCheckStubServer.StubResponse
			{
				StatusCode = 200,
				Body = "{\"tag_name\":\"" + tagName + "\",\"name\":\"r1\",\"body\":\"notes\","
					+ "\"html_url\":\"https://example.com/r\","
					+ "\"assets\":[{\"browser_download_url\":\"https://example.com/d\"}]}"
			};
		}

		/// <summary>复现用户报障的 302（无 Location，客户端无法跟随，直接拿到 302 终态）。</summary>
		private static UpdateCheckStubServer.StubResponse Hijack302()
		{
			return new UpdateCheckStubServer.StubResponse
			{
				StatusCode = 302,
				ReasonPhrase = "AuthenticationRequired",
				ContentType = "text/html",
				Body = ""
			};
		}

		[Fact]
		public void CheckLatestRelease_DirectSucceeds_NoFallbackAttempt()
		{
			using (var server = UpdateCheckStubServer.Start())
			{
				server.Responses.Enqueue(SuccessResponse("v999.0.0"));
				var checker = new UpdateChecker(server.BaseUrl + ReleasePath, 5);
				UpdateInfo info = checker.CheckLatestRelease();
				Assert.Equal("", info.ErrorMessage);
				Assert.Equal("999.0.0", info.LatestVersion);
				Assert.Equal("https://example.com/d", info.DownloadUrl);
				// 直连成功：不应发起第二次尝试
				Assert.Equal(1, server.RequestCount);
			}
		}

		[Fact]
		public void CheckLatestRelease_DirectHijacked302_FallsBackToSecondAttempt()
		{
			using (var server = UpdateCheckStubServer.Start())
			{
				server.Responses.Enqueue(Hijack302());
				server.Responses.Enqueue(SuccessResponse("v999.0.0"));
				var checker = new UpdateChecker(server.BaseUrl + ReleasePath, 5);
				UpdateInfo info = checker.CheckLatestRelease();
				Assert.Equal("", info.ErrorMessage);
				Assert.Equal("999.0.0", info.LatestVersion);
				// 直连 302 被劫持 → 回退尝试成功
				Assert.Equal(2, server.RequestCount);
			}
		}

		[Fact]
		public void CheckLatestRelease_DirectReturnsPortalHtml_FallsBackToSecondAttempt()
		{
			using (var server = UpdateCheckStubServer.Start())
			{
				// 直连被劫持：302 跟随（或直接）落到门户登录页——200 但正文是 HTML
				server.Responses.Enqueue(new UpdateCheckStubServer.StubResponse
				{
					StatusCode = 200,
					ContentType = "text/html",
					Body = "<html><body>Portal login page</body></html>"
				});
				server.Responses.Enqueue(SuccessResponse("v999.0.0"));
				var checker = new UpdateChecker(server.BaseUrl + ReleasePath, 5);
				UpdateInfo info = checker.CheckLatestRelease();
				Assert.Equal("", info.ErrorMessage);
				Assert.Equal("999.0.0", info.LatestVersion);
				Assert.Equal(2, server.RequestCount);
			}
		}

		[Fact]
		public void CheckLatestRelease_DirectTimesOut_FallsBackToSecondAttempt()
		{
			using (var server = UpdateCheckStubServer.Start())
			{
				// 直连黑洞：挂起 7 秒不响应；检查器超时（3s）后回退，第二次立即成功。
				// 超时预算放宽到 3s：全量并行时本地响应也可能被调度延迟，1s 会误伤回退尝试。
				server.Responses.Enqueue(new UpdateCheckStubServer.StubResponse { DelayMilliseconds = 7000 });
				server.Responses.Enqueue(SuccessResponse("v999.0.0"));
				var checker = new UpdateChecker(server.BaseUrl + ReleasePath, 3);
				UpdateInfo info = checker.CheckLatestRelease();
				Assert.True(string.IsNullOrEmpty(info.ErrorMessage), "超时回退后应成功，实际错误: " + info.ErrorMessage);
				Assert.Equal("999.0.0", info.LatestVersion);
				Assert.Equal(2, server.RequestCount);
			}
		}

		[Fact]
		public void CheckLatestRelease_BothAttemptsFail_ReportsDirectError()
		{
			using (var server = UpdateCheckStubServer.Start())
			{
				server.Responses.Enqueue(Hijack302());
				server.Responses.Enqueue(Hijack302());
				var checker = new UpdateChecker(server.BaseUrl + ReleasePath, 5);
				UpdateInfo info = checker.CheckLatestRelease();
				// 两次都失败：主报错取直连错误，保持与旧行为一致的诊断口径
				Assert.StartsWith("302", info.ErrorMessage);
				Assert.Contains("AuthenticationRequired", info.ErrorMessage);
				Assert.False(info.HasUpdate);
				Assert.Equal(2, server.RequestCount);
			}
		}
	}
}
