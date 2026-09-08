using System;
using System.IO;

namespace ForkPlus
{
	public static class SystemEnvironment
	{
		/// <summary>
		/// Migration note：跨平台用户主目录。原代码到处用 SystemEnvironment.UserProfileDirectory，
		/// 该 %VAR% 语法仅在 Windows 展开；Linux/macOS 上原样返回字面量 "%userprofile%"，导致
		/// WelcomeWindow 的 IsSubmitAllowed（目录必须存在）永远为 false、"完成"按钮禁用。
		/// 改用 Environment.GetFolderPath(UserProfile)（Windows=C:\Users\x，Unix=$HOME），
		/// 失败时回退 Windows 变量展开，再回退 HOME 环境变量，保证总有可用路径。
		/// </summary>
		public static string UserProfileDirectory
		{
			get
			{
				try
				{
					string text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					if (!string.IsNullOrEmpty(text))
					{
						return text;
					}
				}
				catch
				{
				}
				try
			{
				string text2 = Environment.ExpandEnvironmentVariables("%userprofile%");
				if (!string.IsNullOrEmpty(text2) && text2.IndexOf('%') < 0)
				{
					return text2;
				}
			}
			catch
			{
			}
				try
				{
					string text3 = Environment.GetEnvironmentVariable("HOME");
					if (!string.IsNullOrEmpty(text3))
					{
						return text3;
					}
				}
				catch
				{
				}
				return AppContext.BaseDirectory;
			}
		}

		[Null]
		public static string LocalSSHDirectory
		{
			get
			{
				try
				{
					string text = UserProfileDirectory;
					if (Directory.Exists(text))
					{
						return Path.Combine(text, ".ssh");
					}
				}
				catch
				{
				}
				return null;
			}
		}

		/// <summary>
		/// Migration note：跨平台 Git 可执行文件名。原代码全仓硬编码 "git.exe"（Windows 惯例），
		/// Linux/macOS 上 git 二进制名是 "git"（无扩展名），文件名校验永不通过，
		/// 导致 ConfigureGitInstanceWindow 的"继续"按钮永远禁用。
		/// </summary>
		public static string GitExecutableName => OperatingSystem.IsWindows() ? "git.exe" : "git";

		/// <summary>
		/// Migration note：判断路径是否为 git 可执行文件（Windows 接受 git.exe，Unix 接受 git 与 git.exe）。
		/// 替代原 `Path.GetFileName(p) == "git.exe"` 的跨平台版本。
		/// </summary>
		public static bool IsGitExecutable(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}
			string fileName = Path.GetFileName(path);
			if (OperatingSystem.IsWindows())
			{
				return string.Equals(fileName, "git.exe", StringComparison.OrdinalIgnoreCase);
			}
			return string.Equals(fileName, "git", StringComparison.Ordinal)
				|| string.Equals(fileName, "git.exe", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Migration note：Unix 系统常见 git 安装路径（用于 ConfigureGitInstanceWindow 候选探测）。
		/// Windows 走原 %programfiles% 系列候选，返回空数组。
		/// </summary>
		public static string[] GetUnixCommonGitPaths()
		{
			if (OperatingSystem.IsWindows())
			{
				return Array.Empty<string>();
			}
			string[] candidates =
			{
				"/usr/bin/git",
				"/usr/local/bin/git",
				"/opt/homebrew/bin/git",
				"/opt/git/bin/git",
				"/usr/local/git/bin/git",
				"/snap/bin/git"
			};
			return candidates;
		}

		/// <summary>
		/// Migration note：在系统 PATH 里探测可执行文件（Unix bash/sh 配套校验用）。
		/// </summary>
		public static bool ExistsOnPath(string fileName)
		{
			try
			{
				string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
				foreach (string directory in pathVariable.Split(Path.PathSeparator))
				{
					if (string.IsNullOrWhiteSpace(directory))
					{
						continue;
					}
					if (File.Exists(Path.Combine(directory.Trim(), fileName)))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		/// <summary>
		/// Migration note：在系统 PATH 里查找可执行文件并返回完整路径（找不到返回 null）。
		/// </summary>
		[Null]
		public static string FindOnPath(string fileName)
		{
			try
			{
				string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
				foreach (string directory in pathVariable.Split(Path.PathSeparator))
				{
					if (string.IsNullOrWhiteSpace(directory))
					{
						continue;
					}
					string candidate = Path.Combine(directory.Trim(), fileName);
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		/// <summary>
		/// Migration note：ssh-keygen 可执行文件路径探测（跨平台）。原代码硬拼
		/// &lt;gitInstance&gt;/usr/bin/ssh-keygen.exe——Windows 的 Git 布局成立，但 Unix 内置
		/// git 实例布局只有 bin/git（无 usr/bin），SSH 密钥生成/验证在 Unix 全坏（探针实证：
		/// gitInstance/2.50.1/ 下仅 bin 目录）。候选链：git 实例 usr/bin/ssh-keygen.exe（原版
		/// Windows 布局优先）→ usr/bin/ssh-keygen → bin/ssh-keygen.exe → bin/ssh-keygen →
		/// 系统 PATH ssh-keygen / ssh-keygen.exe。全缺失时返回原版路径（保持可诊断的失败）。
		/// </summary>
		public static string GetSshKeygenPath()
		{
			string legacyPath = null;
			try
			{
				string gitRoot = Path.GetDirectoryName(Path.GetDirectoryName(App.GitPath));
				string[] candidates =
				{
					Path.Combine(gitRoot, "usr", "bin", "ssh-keygen.exe"),
					Path.Combine(gitRoot, "usr", "bin", "ssh-keygen"),
					Path.Combine(gitRoot, "bin", "ssh-keygen.exe"),
					Path.Combine(gitRoot, "bin", "ssh-keygen")
				};
				legacyPath = candidates[0];
				foreach (string candidate in candidates)
				{
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve ssh-keygen path", ex);
			}
			string fromPath = FindOnPath("ssh-keygen") ?? FindOnPath("ssh-keygen.exe");
			if (fromPath != null)
			{
				return fromPath;
			}
			return legacyPath ?? Path.Combine(Path.Combine("usr", "bin"), "ssh-keygen.exe");
		}
	}
}
