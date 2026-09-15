using System;
using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 凭据收编（Layer C）回归测试：GCM 兼容键的构造与平台守卫。
	/// 见 docs/credential-popup-unification.md。
	///
	/// 键格式必须与 git-credential-manager 的 WindowsCredentialStore 完全一致
	/// （git:{protocol}://{user}@{host} / git:{protocol}://{host}），这是
	/// "GCM 存量凭据无痛迁移 + 外部读写互通"两个目标成立的前提。
	/// </summary>
	public class GcmCompatibleStoreTests
	{
		[Fact]
		public void BuildQueryTargetNames_WithUsername_UserHostKeyFirstThenHostKey()
		{
			// 宽容查询顺序：带 username 的键优先（精确命中），host 级键兜底
			// （覆盖 GCM 以"记住我"模式存的 host 级凭据）。
			string[] names = GcmCompatibleStore.BuildQueryTargetNames("https", "github.com", "alice");

			Assert.Equal(new[] { "git:https://alice@github.com", "git:https://github.com" }, names);
		}

		[Fact]
		public void BuildQueryTargetNames_WithoutUsername_OnlyHostKey()
		{
			string[] names = GcmCompatibleStore.BuildQueryTargetNames("https", "github.com", null);

			Assert.Equal(new[] { "git:https://github.com" }, names);
		}

		[Fact]
		public void BuildQueryTargetNames_EmptyUsername_TreatedAsMissing()
		{
			string[] names = GcmCompatibleStore.BuildQueryTargetNames("https", "github.com", "");

			Assert.Equal(new[] { "git:https://github.com" }, names);
		}

		[Fact]
		public void BuildQueryTargetNames_NullProtocol_DefaultsToHttps()
		{
			// git 的凭据描述必含 protocol，null 只可能来自异常输入，默认 https 与 GCM 行为一致。
			string[] names = GcmCompatibleStore.BuildQueryTargetNames(null, "example.com", null);

			Assert.Equal(new[] { "git:https://example.com" }, names);
		}

		[Fact]
		public void BuildStoreTargetName_WithUsername_WritesUserHostKey()
		{
			// 写入按 GCM 规则：描述带 username（git 的 store 输入必含）写 user@host 键。
			Assert.Equal("git:https://alice@github.com",
				GcmCompatibleStore.BuildStoreTargetName("https", "github.com", "alice"));
		}

		[Fact]
		public void BuildStoreTargetName_WithoutUsername_WritesHostKey()
		{
			Assert.Equal("git:http://example.com",
				GcmCompatibleStore.BuildStoreTargetName("http", "example.com", null));
		}

		// 修复（2026-09-14，"git mm init 弹凭据窗：明明凭据管理器里有凭据还是反复询问"）：
		// credential.usehttppath=true 时 GCM 按完整路径存凭据（键含仓库路径、不含 username，
		// username 记在凭据条目上）。带 path 的候选/写入键必须覆盖该粒度。
		[Fact]
		public void BuildQueryTargetNames_WithPath_PathKeyFirstThenBroaderFallbacks()
		{
			// 具体到宽泛：path 级（GCM usehttppath 存储粒度）→ user@host 级 → host 级。
			string[] names = GcmCompatibleStore.BuildQueryTargetNames(
				"https", "codehub-git-codeartsx.rnd.yinwang.com", "h00003968",
				"innersource/T4VB_G/TNC/build/manifest.git");

			Assert.Equal(new[]
			{
				"git:https://codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git",
				"git:https://h00003968@codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git",
				"git:https://h00003968@codehub-git-codeartsx.rnd.yinwang.com",
				"git:https://codehub-git-codeartsx.rnd.yinwang.com"
			}, names);
		}

		[Fact]
		public void BuildQueryTargetNames_WithPathWithoutUsername_PathKeyThenHostKey()
		{
			// get 首次询问通常无 username（URL 未内嵌）：path 级 + host 级。
			string[] names = GcmCompatibleStore.BuildQueryTargetNames(
				"https", "codehub-git-codeartsx.rnd.yinwang.com", null,
				"innersource/T4VB_G/TNC/build/manifest.git");

			Assert.Equal(new[]
			{
				"git:https://codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git",
				"git:https://codehub-git-codeartsx.rnd.yinwang.com"
			}, names);
		}

		[Fact]
		public void BuildQueryTargetNames_WithEmptyPath_DegeneratesToHostLevel()
		{
			// 空 path 视为缺失：候选与 3 参版本一致（兼容 usehttppath=false 的默认场景）。
			string[] names = GcmCompatibleStore.BuildQueryTargetNames("https", "github.com", "alice", "");

			Assert.Equal(new[] { "git:https://alice@github.com", "git:https://github.com" }, names);
		}

		[Fact]
		public void BuildStoreTargetName_WithPath_WritesPathLevelKeyLikeGcm()
		{
			// GCM usehttppath 规则：键含路径、不含 username（username 记在条目上）。
			Assert.Equal(
				"git:https://codehub-git-codeartsx.rnd.yinwang.com/innersource/T4VB_G/TNC/build/manifest.git",
				GcmCompatibleStore.BuildStoreTargetName(
					"https", "codehub-git-codeartsx.rnd.yinwang.com", "h00003968",
					"innersource/T4VB_G/TNC/build/manifest.git"));
		}

		[Fact]
		public void BuildStoreTargetName_WithoutPath_FallsBackToUserHostKey()
		{
			Assert.Equal("git:https://alice@github.com",
				GcmCompatibleStore.BuildStoreTargetName("https", "github.com", "alice", null));
		}

		[Fact]
		public void TryQuery_EmptyOrNullHost_ReturnsFalseBeforeAnyPlatformCall()
		{
			// 守卫顺序契约：空 host 判定先于 Windows P/Invoke——任何平台都不抛。
			Assert.False(GcmCompatibleStore.TryQuery("https", "", "user", out string username1, out string password1));
			Assert.Null(username1);
			Assert.Null(password1);

			Assert.False(GcmCompatibleStore.TryQuery("https", null, "user", out string username2, out string password2));
			Assert.Null(username2);
			Assert.Null(password2);
		}

		[Fact]
		public void Store_EmptyOrNullPassword_IsGuardedNoOp()
		{
			// 空 host/空密码守卫在任何平台都先于 P/Invoke——空凭据不落盘。
			GcmCompatibleStore.Store("https", "example.com", "user", null);
			GcmCompatibleStore.Store("https", "example.com", "user", "");
			GcmCompatibleStore.Store("https", "", "user", "secret");
		}

		[Fact]
		public void Erase_EmptyOrNullHost_IsGuardedNoOp()
		{
			GcmCompatibleStore.Erase("https", "", "user");
			GcmCompatibleStore.Erase("https", null, "user");
		}

		[Fact]
		public void NonWindows_Entries_AreNoThrowNoOp()
		{
			// 平台守卫回归（CI 跑 Linux，此断言在 CI 上生效）：非 Windows 上
			// Advapi32 P/Invoke 会抛 DllNotFoundException，而 AskPass 的 IPC
			// 服务线程只捕获 IOException——异常会把整条凭据链路带崩。
			// Windows 本地跑此用例时直接跳过（走真实 Credential Manager 路径，单测不伪造）。
			if (OperatingSystem.IsWindows())
			{
				return;
			}

			Assert.False(GcmCompatibleStore.TryQuery("https", "example.com", "user", out string username, out string password));
			Assert.Null(username);
			Assert.Null(password);
			GcmCompatibleStore.Store("https", "example.com", "user", "secret");
			GcmCompatibleStore.Erase("https", "example.com", "user");
		}
	}
}
