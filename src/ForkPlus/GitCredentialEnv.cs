using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ForkPlus
{
	/// <summary>
	/// 凭据收编（Layer B）：环境级注入 helper。设计见 docs/credential-popup-unification.md。
	///
	/// 背景：`-c credential.helper=...` 只作用于当次 git 进程，不随进程树传播；
	/// git-mm、submodule update、clone --recurse-submodules 等场景下 git 内部再拉起的
	/// 子 git 进程拿不到覆盖链，回落到用户系统/全局配置里的 helper（Windows 默认 GCM），
	/// 于是弹出原生凭据窗口。环境变量形式的 GIT_CONFIG_COUNT/KEY_n/VALUE_n（git ≥ 2.31，
	/// 本仓库要求 git ≥ 2.40）与 `-c` 同语义且随进程树继承，是封堵该泄露路径的关键。
	///
	/// 注入内容与语义：
	/// - GIT_ASKPASS = ForkPlus.AskPass 路径：封住 git askpass 回落链第一环
	///   （GIT_ASKPASS → core.askPass → SSH_ASKPASS → 终端），防止外部残留的同名变量抢先劫持。
	/// - GIT_TERMINAL_PROMPT = 0：所有 helper/askpass 落空时快速失败，不挂死在不可见的终端询问上。
	/// - GIT_CONFIG_KEY_n/VALUE_n 两条：空值重置（清空系统/全局累积的 helper 链）+
	///   挂 ForkPlus.AskPass helper——与 App.OverrideCredentialHelper 的 -c 覆盖链（Layer A）
	///   同语义，幂等叠加。
	/// </summary>
	public static class GitCredentialEnv
	{
		public static readonly string GitAskPassKey = "GIT_ASKPASS";

		public static readonly string TerminalPromptKey = "GIT_TERMINAL_PROMPT";

		public static readonly string ConfigCountKey = "GIT_CONFIG_COUNT";

		/// <summary>
		/// 解析现有 GIT_CONFIG_COUNT。非正整数/解析失败/为 null 一律视为 0
		/// （git 对非法 COUNT 的行为是从 0 重新计数，这里与 git 的容错口径对齐）。
		/// </summary>
		public static int ParseConfigCount([Null] string rawCount)
		{
			if (int.TryParse(rawCount, out int count) && count > 0)
			{
				return count;
			}
			return 0;
		}

		/// <summary>
		/// 生成 Layer B 全部注入键值对：GIT_ASKPASS / GIT_TERMINAL_PROMPT /
		/// GIT_CONFIG_COUNT（新值）+ 两条 credential.helper 配置（KEY/VALUE_从 startIndex 编号）。
		/// 共 7 对（14 个字符串），Bt 场景直接平铺进 string[] env，psi 场景逐对写字典。
		/// </summary>
		public static (string, string)[] BuildAllPairs(int startIndex)
		{
			// helper 值带上字面引号 + EscapeSpaces 转义：git 对 credential.helper 值做
			// shell 词法分词，引号保护含空格的安装路径——与 App.OverrideCredentialHelper
			// 的 -c 形式保持同一转义约定（该约定已知瑕疵见设计文档"已知权衡"一节）。
			string helperValue = "\"" + PathHelper.NormalizeUnix(App.ForkCredentialHelperPath).EscapeSpaces() + "\"";
			return new (string, string)[7]
			{
				(GitAskPassKey, App.ForkCredentialHelperPath),
				(TerminalPromptKey, "0"),
				(ConfigCountKey, (startIndex + 2).ToString()),
				("GIT_CONFIG_KEY_" + startIndex, "credential.helper"),
				("GIT_CONFIG_VALUE_" + startIndex, ""),
				("GIT_CONFIG_KEY_" + (startIndex + 1), "credential.helper"),
				("GIT_CONFIG_VALUE_" + (startIndex + 1), helperValue)
			};
		}

		/// <summary>
		/// 把 Layer B 注入应用到 ProcessStartInfo 的环境字典。
		/// 读取时机很重要：必须在调用方 additionalEnv 应用之后调用，这样字典里
		/// GIT_CONFIG_COUNT 已是"父环境 + additionalEnv"合并后的值，我们的条目
		/// 顺延编号不覆盖既有注入。
		/// </summary>
		public static void ApplyToProcessStartInfo(ProcessStartInfo processStartInfo)
		{
			// 环境字典的索引器对缺失键抛 KeyNotFoundException——干净进程环境（CI runner
			// 即如此，无 GIT_CONFIG_COUNT）必须先 ContainsKey；ParseConfigCount(null) 视为 0。
			// 注：EnvironmentVariables 是 StringDictionary（老的非泛型类型，无 TryGetValue，
			// cb81dc1 的 CI 实证 CS1061），只能 ContainsKey + 索引器两段式。
			// 首次 CI 全量跑实证：此处的 KeyNotFoundException 会让所有 psi 路径的 git 请求
			// 整体失败，E2E 用例连锁红（109 项）。
			string rawCount = processStartInfo.EnvironmentVariables.ContainsKey(ConfigCountKey)
				? processStartInfo.EnvironmentVariables[ConfigCountKey]
				: null;
			int startIndex = ParseConfigCount(rawCount);
			(string, string)[] pairs = BuildAllPairs(startIndex);
			for (int i = 0; i < pairs.Length; i++)
			{
				processStartInfo.EnvironmentVariables[pairs[i].Item1] = pairs[i].Item2;
			}
		}

		/// <summary>
		/// 从 Bt 场景的平铺 env 键值数组（[k0,v0,k1,v1,...]）尾部扫描 GIT_CONFIG_COUNT。
		/// 数组是"父环境之上的增量覆盖集"，尾部（调用方 additionalEnv）优先生效；
		/// 数组里没有时回退到当前进程环境（父环境的基底）。
		/// </summary>
		public static int FindConfigCount(List<string> flatEnv)
		{
			for (int i = flatEnv.Count - 2; i >= 0; i -= 2)
			{
				if (flatEnv[i] == ConfigCountKey)
				{
					return ParseConfigCount(flatEnv[i + 1]);
				}
			}
			return ParseConfigCount(Environment.GetEnvironmentVariable(ConfigCountKey));
		}
	}
}
