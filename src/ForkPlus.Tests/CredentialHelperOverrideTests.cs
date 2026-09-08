using System;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 凭据收编（Layer A）回归测试：原生凭据弹窗收编方案的第一层——覆盖链收拢。
	/// 见 docs/credential-popup-unification.md。
	///
	/// 背景（收编前的两个泄露点）：
	/// ① 覆盖链末端显式保留 credential.helper="manager"（GCM），账号未命中时 git
	///   继续遍历到它，弹出操作系统原生的凭据窗口。
	/// ② 账号管理器无账号时 getter 退化返回 string[0]，完全不注入覆盖链，
	///   任由 git 走用户系统/全局配置里的 helper 链（Windows 默认就是 GCM）。
	///
	/// 收编后契约：
	/// - 两个 getter 无条件返回覆盖链（与账号状态无关，getter 内不再查 AccountManager）；
	/// - 覆盖链只含两条：清空已累积的 helper 列表 + 挂 ForkPlus 自带的 AskPass helper；
	/// - 链上不存在任何指向第三方/原生 credential manager 的条目。
	/// </summary>
	public class CredentialHelperOverrideTests
	{
		[Fact]
		public void OverrideCredentialHelper_NotEmpty_RegardlessOfAccounts()
		{
			// 泄露点②的收编验证：CI 沙盒里账号管理器为空（Accounts.Length == 0），
			// 收编前此处会拿到 string[0]；收编后必须恒为完整覆盖链。
			// （getter 已不读账号状态，本断言环境无关地稳定成立。）
			Assert.NotEmpty(App.OverrideCredentialHelper);
		}

		[Fact]
		public void OverrideCredentialHelperBt_NotEmpty_RegardlessOfAccounts()
		{
			Assert.NotEmpty(App.OverrideCredentialHelperBt);
		}

		[Fact]
		public void OverrideCredentialHelper_ContainsNoManagerEntry()
		{
			// 泄露点①的收编验证：链上不得再出现 "manager"（GCM）。
			Assert.DoesNotContain(App.OverrideCredentialHelper,
				entry => entry.IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0);
		}

		[Fact]
		public void OverrideCredentialHelperBt_ContainsNoManagerEntry()
		{
			Assert.DoesNotContain(App.OverrideCredentialHelperBt,
				entry => entry.IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0);
		}

		[Fact]
		public void OverrideCredentialHelper_HasResetThenAskPassOnly()
		{
			// 契约：4 元素 = 两组 -c + key=value。
			//   [0]/[1] 重置：credential.helper 置空，清空系统/全局配置累积的 helper 列表；
			//   [2]/[3] 挂载：credential.helper = ForkPlus 自带 AskPass helper 路径。
			// BT 变体（用于命令行拼接回显）语义相同，仅引号风格不同。
			var chain = App.OverrideCredentialHelper;
			Assert.Equal(4, chain.Length);
			Assert.Equal("-c", chain[0]);
			Assert.Equal("-c", chain[2]);
			// 重置项：值为空（`credential.helper=""`）
			Assert.StartsWith("credential.helper=", chain[1]);
			Assert.Equal("\"\"", chain[1].Substring(chain[1].IndexOf('=') + 1));
			// 挂载项：值指向 ForkPlus.AskPass 可执行文件
			Assert.StartsWith("credential.helper=", chain[3]);
			string mounted = chain[3].Substring(chain[3].IndexOf('=') + 1).Trim('"');
			Assert.Contains(Consts.ForkPlus.AskPassFilename, mounted, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void OverrideCredentialHelperBt_HasResetThenAskPassOnly()
		{
			var chain = App.OverrideCredentialHelperBt;
			Assert.Equal(4, chain.Length);
			Assert.Equal("-c", chain[0]);
			Assert.Equal("-c", chain[2]);
			// BT 变体重置项：`credential.helper=`（裸空值，无引号）
			Assert.Equal("credential.helper=", chain[1]);
			// 挂载项：裸路径，指向 ForkPlus.AskPass
			Assert.StartsWith("credential.helper=", chain[3]);
			Assert.Contains(Consts.ForkPlus.AskPassFilename, chain[3], StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void BothChains_PointToSameAskPassHelper()
		{
			// 两个变体挂载的是同一个 ForkPlus.AskPass（App.ForkCredentialHelperPath），
			// 只是引号/转义风格不同（前者用于 git 直接参数，后者用于命令行拼接回显）。
			// 断言与实现同构：EscapeSpaces(NormalizeUnix(path))（构建输出路径通常无空格，
			// EscapeSpaces 退化为恒等，含空格的本地路径下依然精确匹配）。
			Assert.NotNull(App.ForkCredentialHelperPath);
			string escaped = PathHelper.NormalizeUnix(App.ForkCredentialHelperPath).EscapeSpaces();
			Assert.Contains(escaped, App.OverrideCredentialHelper[3], StringComparison.Ordinal);
			Assert.Contains(escaped, App.OverrideCredentialHelperBt[3], StringComparison.Ordinal);
		}
	}
}
