using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 凭据收编（Layer B）回归测试：环境级注入。
	/// 见 docs/credential-popup-unification.md。
	///
	/// 契约：
	/// - 注入 7 对键值：GIT_ASKPASS / GIT_TERMINAL_PROMPT / GIT_CONFIG_COUNT（新值）
	///   + GIT_CONFIG_KEY_n/VALUE_n 两条 credential.helper 配置（n 从 startIndex 编号）；
	/// - 两条配置 = 空值重置 + 挂 ForkPlus.AskPass helper，与 Layer A 的 -c 覆盖链同语义；
	/// - 已有 GIT_CONFIG_COUNT 时顺延编号，不覆盖既有注入（父环境/调用方 additionalEnv 共存）；
	/// - ApplyToProcessStartInfo 把上述键值写入 ProcessStartInfo 环境字典。
	/// </summary>
	public class GitCredentialEnvTests
	{
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("garbage")]
		[InlineData("-1")]
		[InlineData("0")]
		public void ParseConfigCount_InvalidOrNonPositive_ReturnsZero(string raw)
		{
			Assert.Equal(0, GitCredentialEnv.ParseConfigCount(raw));
		}

		[Theory]
		[InlineData("1", 1)]
		[InlineData("3", 3)]
		[InlineData("42", 42)]
		public void ParseConfigCount_ValidPositive_ReturnsValue(string raw, int expected)
		{
			Assert.Equal(expected, GitCredentialEnv.ParseConfigCount(raw));
		}

		[Fact]
		public void BuildAllPairs_ZeroStart_HasSevenPairs()
		{
			(string, string)[] pairs = GitCredentialEnv.BuildAllPairs(0);
			Assert.Equal(7, pairs.Length);
		}

		[Fact]
		public void BuildAllPairs_ZeroStart_ContainsAskPassAndTerminalPromptAndCount()
		{
			(string, string)[] pairs = GitCredentialEnv.BuildAllPairs(0);
			var env = ToDictionary(pairs);
			// GIT_ASKPASS 封住 askpass 回落链第一环，值原样（git 直接 exec，无需 shell 转义）
			Assert.Equal(App.ForkCredentialHelperPath, env[GitCredentialEnv.GitAskPassKey]);
			// 终端询问快速失败
			Assert.Equal("0", env[GitCredentialEnv.TerminalPromptKey]);
			// 0 起点：新增两条配置，COUNT = 2
			Assert.Equal("2", env[GitCredentialEnv.ConfigCountKey]);
		}

		[Fact]
		public void BuildAllPairs_ZeroStart_ResetThenMountAskPass()
		{
			(string, string)[] pairs = GitCredentialEnv.BuildAllPairs(0);
			var env = ToDictionary(pairs);
			// 第一条：空值重置（清空系统/全局配置累积的 helper 链）
			Assert.Equal("credential.helper", env["GIT_CONFIG_KEY_0"]);
			Assert.Equal("", env["GIT_CONFIG_VALUE_0"]);
			// 第二条：挂 ForkPlus.AskPass，值带字面引号（git 对 helper 值做 shell 词法分词）
			Assert.Equal("credential.helper", env["GIT_CONFIG_KEY_1"]);
			string mounted = env["GIT_CONFIG_VALUE_1"];
			Assert.StartsWith("\"", mounted);
			Assert.EndsWith("\"", mounted);
			Assert.Contains(Consts.ForkPlus.AskPassFilename, mounted, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void BuildAllPairs_NonZeroStart_IndicesShift()
		{
			// 父环境/调用方已占用 5 条配置时，我们的条目从 KEY_5/VALUE_5 起，COUNT = 7
			(string, string)[] pairs = GitCredentialEnv.BuildAllPairs(5);
			var env = ToDictionary(pairs);
			Assert.Equal("credential.helper", env["GIT_CONFIG_KEY_5"]);
			Assert.Equal("", env["GIT_CONFIG_VALUE_5"]);
			Assert.Equal("credential.helper", env["GIT_CONFIG_KEY_6"]);
			Assert.Contains(Consts.ForkPlus.AskPassFilename, env["GIT_CONFIG_VALUE_6"], StringComparison.OrdinalIgnoreCase);
			Assert.Equal("7", env[GitCredentialEnv.ConfigCountKey]);
		}

		[Fact]
		public void FindConfigCount_EmptyFlatEnv_FallsBackToProcessEnvironment()
		{
			// 数组（增量覆盖集）里没有 COUNT：回退到当前进程环境。
			// 断言口径：结果 ≥ 0 且与直接解析进程环境值一致（环境值本身不可控，只验等价性）。
			var flatEnv = new List<string> { "SSH_ASKPASS_REQUIRE", "force" };
			Assert.Equal(
				GitCredentialEnv.ParseConfigCount(System.Environment.GetEnvironmentVariable(GitCredentialEnv.ConfigCountKey)),
				GitCredentialEnv.FindConfigCount(flatEnv));
		}

		[Fact]
		public void FindConfigCount_TailEntryWins()
		{
			// 平铺数组按序覆盖（后者胜），尾部（调用方 additionalEnv）的 COUNT 优先生效
			var flatEnv = new List<string>
			{
				"GIT_CONFIG_COUNT", "2",   // 前部（父环境语义）
				"OTHER", "x",
				"GIT_CONFIG_COUNT", "5"    // 尾部（additionalEnv 语义，应胜出）
			};
			Assert.Equal(5, GitCredentialEnv.FindConfigCount(flatEnv));
		}

		[Fact]
		public void ApplyToProcessStartInfo_AppendsWithoutOverwritingExistingConfig()
		{
			// 模拟：调用方 additionalEnv 已设 COUNT=1 + KEY_0/VALUE_0
			var psi = new ProcessStartInfo
			{
				FileName = "git",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			psi.EnvironmentVariables[GitCredentialEnv.ConfigCountKey] = "1";
			psi.EnvironmentVariables["GIT_CONFIG_KEY_0"] = "user.name";
			psi.EnvironmentVariables["GIT_CONFIG_VALUE_0"] = "caller-value";

			GitCredentialEnv.ApplyToProcessStartInfo(psi);

			// 调用方的 KEY_0 原样保留，我们的条目从 KEY_1 顺延
			Assert.Equal("user.name", psi.EnvironmentVariables["GIT_CONFIG_KEY_0"]);
			Assert.Equal("caller-value", psi.EnvironmentVariables["GIT_CONFIG_VALUE_0"]);
			Assert.Equal("credential.helper", psi.EnvironmentVariables["GIT_CONFIG_KEY_1"]);
			Assert.Equal("", psi.EnvironmentVariables["GIT_CONFIG_VALUE_1"]);
			Assert.Equal("credential.helper", psi.EnvironmentVariables["GIT_CONFIG_KEY_2"]);
			Assert.Contains(Consts.ForkPlus.AskPassFilename,
				psi.EnvironmentVariables["GIT_CONFIG_VALUE_2"], StringComparison.OrdinalIgnoreCase);
			Assert.Equal("3", psi.EnvironmentVariables[GitCredentialEnv.ConfigCountKey]);
			// 与凭据相关的其余两项也一并注入
			Assert.Equal(App.ForkCredentialHelperPath, psi.EnvironmentVariables[GitCredentialEnv.GitAskPassKey]);
			Assert.Equal("0", psi.EnvironmentVariables[GitCredentialEnv.TerminalPromptKey]);
		}

		[Fact]
		public void ApplyToProcessStartInfo_CleanEnvironment_StartsAtZero()
		{
			// 干净环境（无 COUNT）：从 KEY_0 起，COUNT = 2
			// 注意：沙盒进程环境若恰好带 GIT_CONFIG_COUNT，此用例退化为顺延场景仍成立——
			// 先移除字典里的值再断言起点（字典是 psi 私有的，移除无副作用）。
			var psi = new ProcessStartInfo { FileName = "git", UseShellExecute = false };
			psi.EnvironmentVariables.Remove(GitCredentialEnv.ConfigCountKey);

			GitCredentialEnv.ApplyToProcessStartInfo(psi);

			Assert.Equal("credential.helper", psi.EnvironmentVariables["GIT_CONFIG_KEY_0"]);
			Assert.Equal("", psi.EnvironmentVariables["GIT_CONFIG_VALUE_0"]);
			Assert.Equal("credential.helper", psi.EnvironmentVariables["GIT_CONFIG_KEY_1"]);
			Assert.Equal("2", psi.EnvironmentVariables[GitCredentialEnv.ConfigCountKey]);
		}

		private static Dictionary<string, string> ToDictionary((string, string)[] pairs)
		{
			var dict = new Dictionary<string, string>();
			foreach ((string, string) pair in pairs)
			{
				dict[pair.Item1] = pair.Item2;
			}
			return dict;
		}
	}
}
