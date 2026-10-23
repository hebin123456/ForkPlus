using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Git.Interaction
{
	/// <summary>git 可执行文件 shim 解析（性能优化，2026-10 首屏加载审计）。
	/// Git for Windows 的 &lt;root&gt;\bin\git.exe 与 &lt;root&gt;\cmd\git.exe 都是薄壳 shim，
	/// 启动后再拉起真实进程 &lt;root&gt;\mingw64\bin\git.exe——每次 git 调用付两次进程创建成本。
	/// 在进程创建被终端安全软件拦截的机器上实测：shim 路径 ≈ 2.1s/次、直连 mingw64 ≈ 1.2s/次，
	/// 首屏/status/各 git 操作每次多付 ~1s。这里在 spawn 前把 shim 解析为真实 exe。
	/// 仅当文件名恰为 git.exe 且父目录为 bin/cmd、同发行版布局的 mingw64\bin\git.exe 存在时改写；
	/// git-mm.exe / 用户自定义实例 / Unix 布局原样返回。
	/// 直连安全性：mingw64\bin\git.exe 即 Git Bash 内实际执行的 git，自行定位 exec-path 与
	/// hooks 所需的 sh（已实测剥离 PATH 后 pre-commit hook 正常运行）；真实 exe 不依赖
	/// shim 注入的任何环境。
	/// 结果按输入路径缓存——进程生命周期内文件布局不变；Preferences 切换 git 实例后
	/// App.GitPath 变化，缓存键随之变化，无失效问题。</summary>
	internal static class GitExecutableResolver
	{
		private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(4);

		public static string Resolve(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return path;
			}
			lock (_cache)
			{
				if (_cache.TryGetValue(path, out string cached))
				{
					return cached;
				}
			}
			string resolved = ResolveCore(path);
			lock (_cache)
			{
				_cache[path] = resolved;
			}
			return resolved;
		}

		private static string ResolveCore(string path)
		{
			try
			{
				if (!OperatingSystem.IsWindows())
				{
					return path;
				}
				if (!string.Equals(Path.GetFileName(path), "git.exe", StringComparison.OrdinalIgnoreCase))
				{
					return path;
				}
				string parentDirectory = Path.GetDirectoryName(path);
				if (string.IsNullOrEmpty(parentDirectory))
				{
					return path;
				}
				string parentName = Path.GetFileName(parentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				if (!string.Equals(parentName, "bin", StringComparison.OrdinalIgnoreCase) && !string.Equals(parentName, "cmd", StringComparison.OrdinalIgnoreCase))
				{
					return path;
				}
				string root = Path.GetDirectoryName(parentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				if (string.IsNullOrEmpty(root))
				{
					return path;
				}
				string realGitPath = Path.Combine(root, "mingw64", "bin", "git.exe");
				return File.Exists(realGitPath) ? realGitPath : path;
			}
			catch
			{
				return path;
			}
		}
	}
}
