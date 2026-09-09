using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// git 版本能力探测（按 git 路径进程内缓存，v4.0.5）。
	/// 背景：交互式变基硬编码 <c>--update-refs</c>（git 2.38 引入）、变基冲突预检走
	/// <c>git replay</c>（git 2.44 引入）——Ubuntu 22.04 LTS 自带 git 2.34、24.04 LTS
	/// 自带 2.43，均不满足；应用内置 git 实例缺失回退系统 git 时，变基功能在这类
	/// 主流发行版上整体报错不可用（"unknown option `update-refs'" / "git: 'replay'
	/// is not a git command"）。按版本探测能力，调用方降级而非硬失败。
	/// 版本探测失败（git 缺失/输出异常）按"不支持"处理——此时 git 命令本身也无法执行。
	/// </summary>
	public static class GitCapabilities
	{
		/// <summary>rebase --update-refs 引入版本（git 2.38，2022-10）。</summary>
		public static readonly Version UpdateRefsMinVersion = new Version(2, 38, 0);

		/// <summary>git replay 子命令引入版本（git 2.44，2024-02）。</summary>
		public static readonly Version ReplayMinVersion = new Version(2, 44, 0);

		// 按路径缓存：探测要 spawn 一次 git 进程（约 10ms），变基预检/提交路径在
		// UI 线程同步调用；缓存后仅首次付费。设置里换 git 实例路径后需重启应用
		// （GitVersionChecker 的启动检查同样按路径一次性探测，行为一致）。
		private static readonly Dictionary<string, Version> VersionCache = new Dictionary<string, Version>(StringComparer.Ordinal);
		private static readonly object CacheLock = new object();

		/// <summary>当前 git 实例（App.GitPath）版本；探测失败返回 null。</summary>
		public static Version GetVersion()
		{
			return GetVersion(App.GitPath);
		}

		/// <summary>指定 git 可执行文件版本；探测失败返回 null。结果按路径缓存。</summary>
		public static Version GetVersion(string gitPath)
		{
			if (string.IsNullOrWhiteSpace(gitPath))
			{
				return null;
			}
			lock (CacheLock)
			{
				if (VersionCache.TryGetValue(gitPath, out Version cached))
				{
					return cached;
				}
				Version version = GitVersionChecker.GetVersion(gitPath);
				VersionCache[gitPath] = version;
				return version;
			}
		}

		/// <summary>当前 git 是否支持 rebase --update-refs（≥2.38）；版本未知时保守返回 false。</summary>
		public static bool SupportsUpdateRefs()
		{
			Version version = GetVersion();
			return version != null && version >= UpdateRefsMinVersion;
		}

		/// <summary>当前 git 是否支持 git replay（≥2.44）；版本未知时保守返回 false。</summary>
		public static bool SupportsReplay()
		{
			Version version = GetVersion();
			return version != null && version >= ReplayMinVersion;
		}
	}
}
