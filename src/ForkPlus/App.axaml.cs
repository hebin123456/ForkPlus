using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
// Migration note：ContentPresenter / ScrollContentPresenter 在 Avalonia 位于 Controls.Presenters
//（WPF 在 Controls.Primitives），此处补 using 以修复 CS0246。
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.IO.Ipc;
using ForkPlus.Services;
using ForkPlus.Services.Wpf;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using Microsoft.Win32;
using NLog;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus
{
	public partial class App : Application
	{
		private class NativeMethods
		{
			[DllImport("shell32.dll", SetLastError = true)]
			private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

			public static void SetAppUserModelID(string appUserModelID)
			{
				try
				{
					SetCurrentProcessExplicitAppUserModelID(appUserModelID);
				}
				catch
				{
				}
			}
		}

		private enum SystemTheme
		{
			Light,
			Dark
		}

		public static readonly string ForkDirectoryPath;

		public static readonly string ForkDataDirectoryPath;

		private static readonly string LegacyForkDirectoryPath;

		private static readonly string LegacyForkDataDirectoryPath;

		public static readonly string RepositoriesFilePath;

		public static readonly string InstanceDirectory;

		public static readonly string ForkCredentialHelperPath;

		private static readonly string[] _overrideCredentialHelper;

		private static readonly string[] _overrideCredentialHelperBt;

		public static readonly string EnvironmentGitInstancePath;

		public static readonly string ForkGitInstancePath;

		public static readonly string AppName;

		public static readonly Version OSVersion;

		public static readonly CliArguments CliArguments;

		private static readonly string AppUserModelID;

		private static readonly string DefaultIpcPipe_StringSeparator;

		private static readonly string DefaultIpcPipe_CliRequest;

		private static readonly string DefaultIpcPipe_Handled;

		private static readonly SolidColorBrush _defaultWindowBorderLightBrush;

		private static readonly SolidColorBrush _defaultWindowBorderDarkBrush;

		private static Brush _windowBorderBrush;

		private static ResourceDictionary _windowsBorderResourceDictionary;

		private static SystemTheme _systemTheme;

		/// <summary>用户自定义颜色覆盖字典（动态构建，merge 到 MergedDictionaries 末尾覆盖预设皮肤颜色）。</summary>
		private static ResourceDictionary _customColorsResourceDictionary;

		private readonly IpcServer _askPassIpcServer;

		private readonly IpcServer _defaultIpcServer;

		private bool _loggedVisualParentingFirstChanceException;

		/// <summary>
		/// 凭据收编（Layer A）：无论账号管理器里有没有账号，都无条件注入覆盖链。
		/// 原先无账号时返回空数组、任由 git 走用户系统/全局配置里的 helper 链（GCM 等），
		/// 是原生凭据弹窗的泄露点之一；账号未命中时 AskPass helper 自会弹出 ForkPlus 自己的窗口。
		/// </summary>
		public static string[] OverrideCredentialHelperBt => _overrideCredentialHelperBt;

		/// <summary>
		/// 凭据收编（Layer A）：同 <see cref="OverrideCredentialHelperBt"/>，无条件注入覆盖链。
		/// </summary>
		public static string[] OverrideCredentialHelper => _overrideCredentialHelper;

		public static string GitPath => EnvironmentGitInstancePath ?? ForkPlusSettings.Default.GitInstancePath ?? ForkGitInstancePath;

		/// <summary>
		/// sh 路径（运行钩子/自定义命令 ${sh} 用）。Windows：Git for Windows 与 git.exe 同目录
		/// 自带 sh.exe（原版行为）；Unix：bash/sh 常与 git 不同目录（自编译 git 在
		/// /usr/local/bin，sh 在 /usr/bin），先查同目录再回退 PATH。
		/// </summary>
		public static string ShellPath => ResolveGitShellExecutable("sh");

		/// <summary>
		/// bash 路径（ShCustomCommandAction/DiscardFileChangesGitCommand 用）。解析规则同 <see cref="ShellPath"/>。
		/// </summary>
		public static string BashPath => ResolveGitShellExecutable("bash");

		/// <summary>
		/// 解析 git 附带的 shell 可执行文件路径。Windows 保持原版（git 同目录 name.exe，
		/// 不做存在性回退以免改变既有语义）；Unix 上 name 与 name.exe 都可能不在 git 目录旁，
		/// 同目录命中失败后回退 PATH 查找，再不行返回同目录拼接值（调用方报错可诊断）。
		/// </summary>
		private static string ResolveGitShellExecutable(string name)
		{
			string gitDirectory = Path.GetDirectoryName(GitPath);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return (gitDirectory != null) ? Path.Combine(gitDirectory, name + ".exe") : (name + ".exe");
			}
			if (gitDirectory != null)
			{
				string sibling = Path.Combine(gitDirectory, name);
				if (File.Exists(sibling))
				{
					return sibling;
				}
			}
			return FindExecutableInPath(name) ?? ((gitDirectory != null) ? Path.Combine(gitDirectory, name) : name);
		}

		/// <summary>
		/// PATH 查找 git-mm 的缓存。PATH 在运行时通常不变，缓存避免每次访问 GitMmPath 都遍历 PATH。
		/// </summary>
		private static string _cachedGitMmFromPath;
		private static bool _gitMmFromPathResolved;

		/// <summary>git-mm 可执行文件名（Migration note：原版硬编码 git-mm.exe，Unix 上无扩展名，
		/// 与 git-ai 同模式跨平台——2026-09-07 "GUI 报 git: 'mm' is not a git command" 修复的一部分）。</summary>
		public static string GitMmExecutableName => OperatingSystem.IsWindows() ? "git-mm.exe" : "git-mm";

		/// <summary>
		/// git-mm 可执行文件路径。优先使用用户在偏好设置中指定的路径；
		/// 否则在 PATH 环境变量中查找（Windows 为 <c>git-mm.exe</c>，Unix 为 <c>git-mm</c>）；
		/// 再否则在 git 可执行文件同目录查找；最后在系统位置查找（各 git 的 exec-path 与用户 bin，见
		/// <see cref="GitMmPathFromSystemLocations"/>）。四者都找不到返回 null。
		/// </summary>
		public static string GitMmPath => ResolveGitMmPath();

	/// <summary>
	/// 仅从 PATH 查找的 git-mm 路径（带缓存）。供偏好设置 UI 列出候选时使用，
	/// 避免直接调用 FindExecutableInPath 绕过缓存导致每次刷新都遍历 PATH。
	/// </summary>
	public static string GitMmPathFromPath
	{
		get
		{
			if (!_gitMmFromPathResolved)
			{
				_cachedGitMmFromPath = FindExecutableInPath(GitMmExecutableName);
				_gitMmFromPathResolved = true;
			}
			return _cachedGitMmFromPath;
		}
	}

	private static string ResolveGitMmPath()
	{
		string saved = ForkPlusSettings.Default.GitMmInstancePath;
		if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved))
		{
			return saved;
		}
		string fromPath = GitMmPathFromPath;
		if (fromPath != null)
		{
			return fromPath;
		}
		try
		{
			string gitDir = Path.GetDirectoryName(GitPath);
			if (gitDir != null)
			{
				string sibling = Path.Combine(gitDir, GitMmExecutableName);
				if (File.Exists(sibling))
				{
					return sibling;
				}
			}
		}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve git-mm path from git directory", ex);
			}
			// 系统位置兜底（系统 git exec-path / 用户 bin）——修复"命令行 git mm 可用、GUI 报
			// git: 'mm' is not a git command"（详见 GitMmPathFromSystemLocations 注释）。
			return GitMmPathFromSystemLocations;
		}

		/// <summary>
		/// 系统位置查找 git-mm 的缓存（进程生命周期内只探一次，内部会跑 git --exec-path 子进程）。
		/// </summary>
		private static string _cachedGitMmFromSystemLocations;
		private static bool _gitMmFromSystemLocationsResolved;

		/// <summary>
		/// 在"系统位置"找到的 git-mm 路径（带缓存）。系统位置 = PATH 中各 git 的 exec-path + 用户
		/// 私有 bin 目录（~/.local/bin、~/bin）+ 用户 shell 环境（.bashrc/.zshrc 等只在 shell 里
		/// 生效的 PATH 目录，见 <see cref="FindExecutableInShellEnvironment"/>）。供偏好设置 UI
		/// 列出候选，并作为 ResolveGitMmPath 的最后一步兜底。
		///
		/// 背景（2026-09-07 用户报告"命令行可以运行 git mm sync，GUI 报 git: 'mm' is not a git
		/// command"，沙盒实证复现）：git 查找自定义子命令沿"自身 exec-path + 进程 PATH"，而 GUI 与
		/// 命令行在两处都可能不同——① GUI 用自带 git 实例（gitInstance/2.50.1），exec-path 与系统
		/// git 不同：企业 git-mm 装在系统 git 的 git-core 目录时只有系统 git 找得到；② 桌面启动的
		/// GUI 进程 PATH 可能缺 ~/.local/bin 等用户 bin（只进 shell）；③ 首轮修复后用户实测仍复现
		/// ——git-mm 装在 nvm（~/.nvm/versions/node/*/bin）或 .bashrc 里 export 进 PATH 的 /opt
		/// 自定义目录、Homebrew shellenv 目录，exec-path 与用户 bin 都探测不到，只有 shell 初始化
		/// 文件里有。此探测找到后，配合 PrependGitMmDirectoryToPath 把目录注入 git 子进程 PATH 封堵。
		/// </summary>
		public static string GitMmPathFromSystemLocations
		{
			get
			{
				if (!_gitMmFromSystemLocationsResolved)
				{
					_cachedGitMmFromSystemLocations = FindGitMmInSystemLocations();
					_gitMmFromSystemLocationsResolved = true;
				}
				return _cachedGitMmFromSystemLocations;
			}
		}

		private static string FindGitMmInSystemLocations()
		{
			return FindExecutableInSystemLocations(GitMmExecutableName);
		}

		/// <summary>
		/// 在"系统位置"查找指定可执行文件（git-mm / git-ai 共用）。系统位置 = ① PATH 中各 git 的
		/// exec-path（git-core 目录，git 扩展的常见安装位）+ ② 用户私有 bin 目录（~/.local/bin、
		/// ~/bin）+ ③ 用户 shell 环境（.bashrc/.zshrc 等只在 shell 里生效的 PATH 目录，见
		/// <see cref="FindExecutableInShellEnvironment"/>）。找不到返回 null。
		/// </summary>
		private static string FindExecutableInSystemLocations(string executableName)
		{
			try
			{
				// 1. PATH 中各 git 的 exec-path（企业 git-mm 的常见安装位置——git-core 目录里，
				//    系统 git（命令行）能找到，GUI 的自带 git 实例 exec-path 不同而找不到）。
				foreach (string gitExe in FindGitExecutablesInPath())
				{
					string execPath = GetGitExecPath(gitExe);
					if (string.IsNullOrWhiteSpace(execPath))
					{
						continue;
					}
					string candidate = Path.Combine(execPath, executableName);
					if (File.Exists(candidate))
					{
						return Path.GetFullPath(candidate);
					}
				}
				// 2. 用户私有 bin 目录（桌面启动的 GUI 进程 PATH 可能不含——shell 里有、GUI 里没有）。
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				if (!string.IsNullOrEmpty(home))
				{
					string[] userBins = new string[2]
					{
						Path.Combine(home, ".local", "bin"),
						Path.Combine(home, "bin")
					};
					foreach (string bin in userBins)
					{
						string candidate = Path.Combine(bin, executableName);
						if (File.Exists(candidate))
						{
							return Path.GetFullPath(candidate);
						}
					}
				}
				// 3. 用户 shell 环境（残余盲区：nvm / .bashrc 里 export PATH 的 /opt 目录 /
				//    Homebrew shellenv——①②都探测不到，只有 shell 初始化文件里有，命令行可用）。
				string fromShell = FindExecutableInShellEnvironment(executableName);
				if (fromShell != null)
				{
					return fromShell;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to find '" + executableName + "' in system locations", ex);
			}
			return null;
		}

		/// <summary>
		/// 在用户 shell 环境中查找可执行文件——按"命令行会看到什么"的口径兜底。桌面启动的 GUI
		/// 进程不执行 shell 初始化文件（.bashrc/.zshrc/.profile 等），nvm（~/.nvm/versions/node/*/bin）、
		/// .bashrc 里 export 进 PATH 的 /opt 自定义目录、Homebrew shellenv 等只对 shell 可见——
		/// 这是"命令行可用、GUI 报 not a git command"在 exec-path / 用户 bin 探测之后的残余盲区
		/// （2026-09-07 首轮修复后用户实测仍复现的场景）。探测方式：依次运行用户 shell（$SHELL，
		/// 交互非登录模式最接近真实终端——桌面终端里的 bash/zsh 只读 .bashrc/.zshrc，登录模式
		/// 反而不读 .bashrc）与 /bin/bash、/bin/sh 兜底，执行
		/// <c>command -v &lt;name&gt;</c>；输出中存在"文件名匹配且真实存在"的绝对路径即采用
		/// （shell 别名/函数的输出非绝对路径，天然排除）。stdin 立即关闭（等效 &lt;/dev/null，
		/// 防 rc 里的 read/交互提示挂死）+ 2 秒超时兜底；仅在前两步都失败时执行，且结果进程级缓存
		/// （GitMmPathFromSystemLocations），不会反复拉起 shell。Windows 返回 null——GUI 与 shell
		/// 的 PATH 同源于注册表环境变量，无此盲区。
		/// </summary>
		private static string FindExecutableInShellEnvironment(string executableName)
		{
			return FindExecutableInShellEnvironment(executableName, BuildShellProbeCandidates());
		}

		/// <summary>
		/// 同 <see cref="FindExecutableInShellEnvironment(string)"/>，但允许注入 shell 候选列表。
		/// 测试用 fake shell 脚本验证探测与输出解析逻辑，不依赖测试机的真实 shell 配置。
		/// </summary>
		internal static string FindExecutableInShellEnvironment(string executableName, string[] shellCandidates)
		{
			if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executableName) || shellCandidates == null)
			{
				return null;
			}
			// 参数模式按"最接近用户真实终端"排序（bash 启动文件规则：登录读 .profile 系列，
			// 交互非登录读 .bashrc——两者互不覆盖，桌面终端里的 bash 是交互非登录形态）：
			// ① 交互非登录（-i -c）：.bashrc / .zshrc / config.fish——Linux 桌面终端的默认形态，
			//    nvm 等 hook 所在（沙盒实证：-l -i 不读 .bashrc，缺此模式用户场景探测不到）；
			// ② 登录+交互（-l -i -c）：.profile/.bash_profile/.zprofile + zsh 的 .zshrc
			//   （zsh 交互即读 .zshrc）——macOS Terminal 等 SSH 登录形态；
			// ③ 仅登录（-l -c）：.profile 系列——防个别 rc 对无 tty 的 -i 模式防御性早退。
			// 每个 shell 按序尝试，命中即返回。
			string[][] argumentModes = new string[3][]
			{
				new string[3] { "-i", "-c", "command -v " + executableName },
				new string[4] { "-l", "-i", "-c", "command -v " + executableName },
				new string[3] { "-l", "-c", "command -v " + executableName }
			};
			foreach (string shell in shellCandidates)
			{
				if (string.IsNullOrWhiteSpace(shell) || !File.Exists(shell))
				{
					continue;
				}
				foreach (string[] arguments in argumentModes)
				{
					string found = RunShellProbe(shell, arguments, executableName);
					if (found != null)
					{
						return found;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// shell 探测候选：$SHELL（GUI 进程从桌面会话继承，指向用户默认 shell）优先，
		/// /bin/bash、/bin/sh 兜底（$SHELL 未设置或指向不可用 shell 时）。按完整路径去重。
		/// </summary>
		private static string[] BuildShellProbeCandidates()
		{
			string[] shells = new string[3]
			{
				Environment.GetEnvironmentVariable("SHELL"),
				"/bin/bash",
				"/bin/sh"
			};
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			List<string> result = new List<string>(shells.Length);
			foreach (string shell in shells)
			{
				if (string.IsNullOrWhiteSpace(shell))
				{
					continue;
				}
				string full;
				try
				{
					full = Path.GetFullPath(shell.Trim());
				}
				catch
				{
					full = shell.Trim();
				}
				if (seen.Add(full))
				{
					result.Add(full);
				}
			}
			return result.ToArray();
		}

		/// <summary>
		/// 运行单个 shell 探测进程并解析输出。shell 无法启动/超时/输出无匹配均返回 null（换下个
		/// 候选继续）。stdout/stderr 均异步读取：rc 文件的杂音可能超过管道缓冲区，只同步读一侧会
		/// 被另一侧卡死；超时后 Kill，管道随进程关闭，读取任务自然结束。
		/// </summary>
		private static string RunShellProbe(string shell, string[] arguments, string executableName)
		{
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo
				{
					FileName = shell,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					RedirectStandardInput = true,
					CreateNoWindow = true
				};
				foreach (string argument in arguments)
				{
					processStartInfo.ArgumentList.Add(argument);
				}
				using (Process process = Process.Start(processStartInfo))
				{
					if (process == null)
					{
						return null;
					}
					// 立即关闭 stdin（等效 </dev/null）：rc 里的 read/交互提示立刻拿到 EOF 而非挂死
					process.StandardInput.Close();
					Task<string> stdoutTask = Task.Run(delegate
					{
						return process.StandardOutput.ReadToEnd();
					});
					Task<string> stderrTask = Task.Run(delegate
					{
						return process.StandardError.ReadToEnd();
					});
					if (!process.WaitForExit(2000))
					{
						try
						{
							process.Kill();
						}
						catch
						{
						}
						return null;
					}
					string stdout = null;
					try
					{
						if (stdoutTask.Wait(1000))
						{
							stdout = stdoutTask.Result;
						}
					}
					catch
					{
					}
					try
					{
						stderrTask.Wait(200);
					}
					catch
					{
					}
					return MatchExecutablePathInShellOutput(stdout, executableName);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Shell environment probe failed for '" + shell + "'", ex);
				return null;
			}
		}

		/// <summary>
		/// 从 shell 探测输出中解析目标可执行文件的绝对路径。rc 文件可能向 stdout 打印杂音
		/// （motd/fortune 等），逐行过滤：仅接受 Unix 绝对路径（以 / 开头）、文件名与目标一致
		/// 且真实存在的行；shell 别名/函数的输出（"alias git-mm=..." / 函数名）非绝对路径，
		/// 天然排除。未匹配返回 null。纯函数（输出校验除外），供单元测试直接覆盖。
		/// </summary>
		internal static string MatchExecutablePathInShellOutput([Null] string stdout, string executableName)
		{
			if (string.IsNullOrWhiteSpace(stdout) || string.IsNullOrEmpty(executableName))
			{
				return null;
			}
			string[] lines = stdout.Split('\n');
			foreach (string raw in lines)
			{
				string line = raw.TrimEnd('\r').Trim();
				if (line.Length == 0 || line[0] != '/')
				{
					continue;
				}
				if (!string.Equals(Path.GetFileName(line), executableName, StringComparison.Ordinal))
				{
					continue;
				}
				try
				{
					string full = Path.GetFullPath(line);
					if (File.Exists(full))
					{
						return full;
					}
				}
				catch
				{
				}
			}
			return null;
		}

		/// <summary>枚举 PATH 各目录中的 git 可执行文件（按完整路径去重）。</summary>
		private static List<string> FindGitExecutablesInPath()
		{
			List<string> result = new List<string>();
			try
			{
				string pathEnv = Environment.GetEnvironmentVariable("PATH");
				if (string.IsNullOrEmpty(pathEnv))
				{
					return result;
				}
				StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
				HashSet<string> seen = new HashSet<string>(comparer);
				string[] segments = pathEnv.Split(Path.PathSeparator);
				foreach (string raw in segments)
				{
					if (string.IsNullOrWhiteSpace(raw))
					{
						continue;
					}
					try
					{
						string candidate = Path.Combine(raw.Trim(), SystemEnvironment.GitExecutableName);
						if (File.Exists(candidate))
						{
							string full = Path.GetFullPath(candidate);
							if (seen.Add(full))
							{
								result.Add(full);
							}
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to check '" + raw + "' in PATH for git", ex);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to search PATH for git executables", ex);
			}
			return result;
		}

		/// <summary>
		/// 运行 <c>git --exec-path</c> 获取该 git 的子命令查找目录；失败返回 null。
		/// 本地纯计算（毫秒级），3 秒超时兜底防异常 git 挂死探测线程。
		/// </summary>
		private static string GetGitExecPath(string gitExe)
		{
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo
				{
					FileName = gitExe,
					Arguments = "--exec-path",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};
				using (Process process = Process.Start(processStartInfo))
				{
					if (process == null)
					{
						return null;
					}
					string stdout = process.StandardOutput.ReadToEnd();
					if (!process.WaitForExit(3000))
					{
						try
						{
							process.Kill();
						}
						catch
						{
						}
						return null;
					}
					string execPath = stdout.Trim();
					if (execPath.Length > 0 && Directory.Exists(execPath))
					{
						return execPath;
					}
					return null;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git exec-path from '" + gitExe + "'", ex);
				return null;
			}
		}

		/// <summary>
		/// 若已解析到 git-mm 且其目录不在 currentPath 中，返回前置该目录后的完整 PATH；否则返回 null
		/// （调用方无需修改）。供 GitRequest 两条执行路径注入 git 子进程 PATH：git 查找 mm 这类自定义
		/// 子命令走"自身 exec-path + 进程 PATH"，注入后无论 GUI 用哪个 git 实例、进程 PATH 初始如何，
		/// git mm 都能找到。已在 PATH 中时返回 null 保持幂等。
		///
		/// 安全性：git 执行 git-foo 时把自身 exec-path 前置到 PATH 再查找——自带实例的内置命令
		/// 优先级始终高于注入目录，对既有命令无行为变化；注入只让自带 exec-path 没有的外部子命令
		/// （git-mm）可见。每次 git 请求都会经过（热路径），全部走缓存/字符串操作，无子进程调用。
		/// </summary>
		public static string PrependGitMmDirectoryToPath(string currentPath)
		{
			try
			{
				string gitMmPath = GitMmPath;
				if (string.IsNullOrWhiteSpace(gitMmPath))
				{
					return null;
				}
				string gitMmDir = Path.GetDirectoryName(gitMmPath);
				if (string.IsNullOrEmpty(gitMmDir) || !Directory.Exists(gitMmDir))
				{
					return null;
				}
				string basePath = currentPath ?? "";
				// 段级相等比较（而非子串包含），避免目录名互为子串时的误判
				StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				string[] segments = basePath.Split(Path.PathSeparator);
				for (int i = 0; i < segments.Length; i++)
				{
					if (string.Equals(segments[i].Trim(), gitMmDir, comparison))
					{
						return null;
					}
				}
				if (basePath.Length == 0)
				{
					return gitMmDir;
				}
				return gitMmDir + Path.PathSeparator + basePath;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to prepend git-mm directory to PATH", ex);
				return null;
			}
		}

		/// <summary>
		/// PATH 查找 git-ai 可执行文件的缓存（与 git-mm 同模式）。
		/// </summary>
		private static string _cachedGitAiFromPath;
		private static bool _gitAiFromPathResolved;

		/// <summary>
		/// git-ai 可执行文件路径（https://github.com/git-ai-project/git-ai，AI 代码归属追踪扩展）。
		/// 优先使用用户在偏好设置中指定的路径；否则在 PATH 环境变量中查找 git-ai（Windows 为 git-ai.exe）；
		/// 再否则在 git 可执行文件同目录查找；最后在系统位置查找（各 git 的 exec-path 与用户 bin，
		/// 见 <see cref="GitAiPathFromSystemLocations"/>）。四者都找不到返回 null（AI 归属功能自动降级隐藏）。
		/// 解析结果文件名非标准时经 staging 符号链接中转（见 <see cref="EnsureGitAiExecutionPath"/>），
		/// 保证 git-ai 以正确文件名被执行。
		/// </summary>
		public static string GitAiPath => EnsureGitAiExecutionPath(GitAiResolvedPath, GitAiStagingDirectory);

		/// <summary>
		/// 解析链原样结果（不经过 staging 中转）：偏好设置 → PATH → git 同目录 → 系统位置。
		/// 供偏好设置 UI 匹配当前选中项（staging 链接路径对用户无意义）与日志显示。
		/// </summary>
		public static string GitAiResolvedPath => ResolveGitAiPath();

		/// <summary>git-ai staging 目录（非标准名可执行文件的符号链接中转，位于 ForkPlus 数据目录）。</summary>
		private static string GitAiStagingDirectory => Path.Combine(ForkDirectoryPath, "git-ai-staging");

		/// <summary>staging 符号链接上次指向的目标（幂等缓存：目标未变且链接有效则不重建）。</summary>
		private static string _stagedGitAiTarget;

		/// <summary>
		/// 仅从 PATH 查找的 git-ai 可执行文件路径（带缓存）。供偏好设置 UI 列出候选时使用。
		/// </summary>
		public static string GitAiPathFromPath
		{
			get
			{
				if (!_gitAiFromPathResolved)
				{
					_cachedGitAiFromPath = FindExecutableInPath(GitAiExecutableName);
					_gitAiFromPathResolved = true;
				}
				return _cachedGitAiFromPath;
			}
		}

		/// <summary>git-ai 可执行文件名（Migration note：原版 Windows 硬编码 git-ai.exe，此处跨平台）。</summary>
		internal static string GitAiExecutableName => OperatingSystem.IsWindows() ? "git-ai.exe" : "git-ai";

		private static string ResolveGitAiPath()
		{
			string saved = ForkPlusSettings.Default.GitAiInstancePath;
			if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved))
			{
				return saved;
			}
			string fromPath = GitAiPathFromPath;
			if (fromPath != null)
			{
				return fromPath;
			}
			try
			{
				string gitDir = Path.GetDirectoryName(GitPath);
				if (gitDir != null)
				{
					string sibling = Path.Combine(gitDir, GitAiExecutableName);
					if (File.Exists(sibling))
					{
						return sibling;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve git-ai path from git directory", ex);
			}
			// 系统位置兜底（各 git 的 exec-path / 用户 bin / 用户 shell 环境——与 git-mm 同模式）：
			// git-ai 官方 install.sh 装到 ~/.git-ai/bin 且只把 PATH 写进 shell rc（.bashrc/.zshrc），
			// 桌面启动的 GUI 进程不执行 shell 初始化文件，PATH/sibling 两步都可能落空——
			// 此处按"命令行会看到什么"的口径再探一轮，堵住"命令行可用、GUI 找不到"的场景。
			return GitAiPathFromSystemLocations;
		}

		/// <summary>git-ai 系统位置查找的缓存（与 GitMmPathFromSystemLocations 同模式：进程级缓存一次）。</summary>
		private static string _cachedGitAiFromSystemLocations;
		private static bool _gitAiFromSystemLocationsResolved;

		/// <summary>
		/// 在"系统位置"找到的 git-ai 路径（带缓存）。系统位置定义与 git-mm 相同（各 git 的
		/// exec-path + 用户 bin + 用户 shell 环境，见 <see cref="FindExecutableInSystemLocations"/>）。
		/// 供偏好设置 UI 列出候选，并作为 ResolveGitAiPath 的最后一步兜底。
		/// </summary>
		public static string GitAiPathFromSystemLocations
		{
			get
			{
				if (!_gitAiFromSystemLocationsResolved)
				{
					_cachedGitAiFromSystemLocations = FindExecutableInSystemLocations(GitAiExecutableName);
					_gitAiFromSystemLocationsResolved = true;
				}
				return _cachedGitAiFromSystemLocations;
			}
		}

		/// <summary>
		/// 保证 git-ai 以正确文件名被执行的路径（核心修复 2026-09-07 "git-ai stats 报
		/// git: 'stats' is not a git command"）。git-ai 二进制按 argv[0] 的文件名分发：
		/// 文件名为 git-ai（Windows 为 git-ai.exe）才走原生命令分支（stats/checkpoint/blame/diff），
		/// 其他任何文件名（手动下载的 git-ai-linux-x64、带版本号的副本、改名安装）一律进入
		/// git 透明代理模式，把参数原样转发给真 git——<c>git-ai stats 'a..b' --json</c> 于是变成
		/// <c>git stats 'a..b' --json</c>，报 git: 'stats' is not a git command（沙盒以 git-ai
		/// 1.3.0/1.7.1/1.7.2 实证：非标准名调用 --version 输出 "git version 2.50.1" 即代理铁证；
		/// 同一二进制以 git-ai 名字经符号链接调用则一切正常）。
		///
		/// 修复：解析到的路径文件名非标准时，在 staging 目录建标准名符号链接，返回链接路径执行
		/// （argv[0] 文件名 = git-ai → 原生命令分支）。幂等：目标未变且链接有效时零 IO 复用；
		/// 建链失败（如 Windows 无符号链接权限）降级返回原路径，行为与修复前一致（不会更糟）。
		/// </summary>
		internal static string EnsureGitAiExecutionPath([Null] string resolvedPath, string stagingDirectory)
		{
			if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
			{
				return resolvedPath;
			}
			string fileName = Path.GetFileName(resolvedPath);
			bool isStandardName = OperatingSystem.IsWindows()
				? string.Equals(fileName, "git-ai.exe", StringComparison.OrdinalIgnoreCase)
				: string.Equals(fileName, "git-ai", StringComparison.Ordinal);
			if (isStandardName)
			{
				return resolvedPath;
			}
			try
			{
				string linkPath = Path.Combine(stagingDirectory, GitAiExecutableName);
				// 幂等：目标未变且链接有效（File.Exists 对悬空链接返回 false，目标被删自动重建）
				if (string.Equals(_stagedGitAiTarget, resolvedPath, StringComparison.Ordinal) && File.Exists(linkPath))
				{
					return linkPath;
				}
				Directory.CreateDirectory(stagingDirectory);
				if (File.Exists(linkPath))
				{
					// 目标可能已变（用户换了自定义路径）——只删链接本身，不动目标文件
					File.Delete(linkPath);
				}
				File.CreateSymbolicLink(linkPath, resolvedPath);
				_stagedGitAiTarget = resolvedPath;
				return linkPath;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to stage git-ai execution path for '" + resolvedPath + "'", ex);
				return resolvedPath;
			}
		}

		/// <summary>
		/// 是否启用 AI 归属功能（Blame 徽标 / 统计）。要求 git-ai 已安装且用户未关闭开关。
		/// </summary>
		public static bool IsAiAttributionEnabled => ForkPlusSettings.Default.AiAttributionEnabled && GitAiPath != null;

		/// <summary>
		/// 是否把 ForkPlus 内置 AI（AI 开发 / AI 代码审查）的文件修改上报给 git-ai checkpoint。
		/// 需同时满足：总开关开启、checkpoint 上报开关开启、git-ai 可用。
		/// </summary>
		public static bool IsAiCheckpointReportingEnabled => IsAiAttributionEnabled && ForkPlusSettings.Default.AiCheckpointReportingEnabled;

		/// <summary>
		/// 在 PATH 环境变量中查找指定可执行文件，返回第一个匹配的完整路径；未找到返回 null。
		/// </summary>
		public static string FindExecutableInPath(string fileName)
		{
			return FindExecutableInPath(fileName, null);
		}

		/// <summary>
		/// 同 <see cref="FindExecutableInPath(string)"/>，但允许注入 PATH 值（测试用：
		/// xunit 跨 collection 并行，直接改进程级 PATH 环境变量有竞态风险，
		/// TokeiResolutionTests 同款"参数注入"先例）。
		/// </summary>
		internal static string FindExecutableInPath(string fileName, string pathEnvironmentOverride)
		{
			try
			{
				string pathEnv = pathEnvironmentOverride ?? Environment.GetEnvironmentVariable("PATH");
				if (string.IsNullOrEmpty(pathEnv))
				{
					return null;
				}
				string[] segments = pathEnv.Split(Path.PathSeparator);
				foreach (string raw in segments)
				{
					if (string.IsNullOrWhiteSpace(raw))
					{
						continue;
					}
					string dir = raw.Trim();
					try
					{
						string candidate = Path.Combine(dir, fileName);
						if (File.Exists(candidate))
						{
							return Path.GetFullPath(candidate);
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to check '" + dir + "' in PATH for '" + fileName + "'", ex);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to search PATH for '" + fileName + "'", ex);
			}
			return null;
		}

		public static int ProcessId { get; }

		public static string ProcessIdString { get; }

		public static string Version
		{
			get
			{
				AssemblyInformationalVersionAttribute informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
				{
					return informationalVersion.InformationalVersion;
				}
				Version version = Assembly.GetExecutingAssembly().GetName().Version;
				if (version != null)
				{
					return version.ToString();
				}
				return "0.0.0.0";
			}
		}

		public static string UserAgent => AppName + " " + Version;

		public static bool IsDebug => Debugger.IsAttached;

		static App()
		{
			string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			LegacyForkDirectoryPath = Path.Combine(localApplicationData, "Fork");
			LegacyForkDataDirectoryPath = Path.Combine(localApplicationData, "ForkData");
			ForkDirectoryPath = Path.Combine(localApplicationData, "ForkPlus");
			ForkDataDirectoryPath = Path.Combine(localApplicationData, "ForkPlusData");
			MigrateLegacyAppData();
			RepositoriesFilePath = Path.Combine(ForkDataDirectoryPath, "repositories.toml");
			InstanceDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			ForkCredentialHelperPath = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AskPassFilename);
			// 凭据收编（Layer A）：覆盖链显式清空系统/全局配置里的所有 helper 后只挂 ForkPlus 自带的
			// AskPass helper。链末端原先还显式保留 "manager"（GCM），账号未命中时 git 会继续遍历到它，
			// 弹出操作系统原生的凭据窗口——与"只感知 ForkPlus 自己的弹窗"目标冲突，故移除。
			// 无账号命中时 AskPass helper 会经 IPC 弹出 ForkPlus 自己的窗口向用户索要凭据。
			_overrideCredentialHelper = new string[4]
			{
				"-c",
				"credential.helper=\"\"",
				"-c",
				"credential.helper=\"" + PathHelper.NormalizeUnix(ForkCredentialHelperPath).EscapeSpaces() + "\""
			};
			_overrideCredentialHelperBt = new string[4]
			{
				"-c",
				"credential.helper=",
				"-c",
				"credential.helper=" + PathHelper.NormalizeUnix(ForkCredentialHelperPath).EscapeSpaces()
			};
			EnvironmentGitInstancePath = GetEnvironmentGitInstancePath();
			ForkGitInstancePath = GetForkGitInstancePath();
			AppName = Assembly.GetExecutingAssembly().GetName().Name;
			OSVersion = Environment.OSVersion.Version;
			CliArguments = new CliArguments();
			AppUserModelID = "com.squirrel.ForkPlus.ForkPlus";
			DefaultIpcPipe_StringSeparator = "!#±";
			DefaultIpcPipe_CliRequest = "cli-request";
			DefaultIpcPipe_Handled = "handled";
			_defaultWindowBorderLightBrush = new SolidColorBrush(Color.FromRgb(59, 172, 237));
			_defaultWindowBorderDarkBrush = new SolidColorBrush(Color.FromRgb(59, 172, 237));
			using (Process process = Process.GetCurrentProcess())
			{
				ProcessId = process.Id;
				ProcessIdString = process.Id.ToString();
				StartupTimeReporter.AppStarted(process.StartTime);
			}
			NativeMethods.SetAppUserModelID(AppUserModelID);
			RegisterScrollViewerContentTemplateGuard();
		}

		public App()
	{
		if (IsDebug)
		{
			LogManager.Configuration = new DebugLoggingConfiguration();
			// Migration note：Avalonia 无 PresentationTraceSources/DataBindingSource（WPF 绑定跟踪管道）；
			// 绑定错误改由 DebugLoggingConfiguration 记录，BindingErrorTraceListener 不再挂接到 TraceSource。
		}
		else
		{
			LogManager.Configuration = new ProductionLoggingConfiguration();
		}
		RegisterGlobalExceptionLogging();
		LogHelper.LogWelcome();
		_askPassIpcServer = new IpcServer(NamedPipeHelper.AskPassPipeName, AskPassIpcMessageHandler);
		_defaultIpcServer = new IpcServer(NamedPipeHelper.DefaultPipeName, DefaultIpcMessageHandler);
		HandleCommandLineArguments();
		// Migration note：App 类有自定义构造函数，Avalonia XAML 编译器要求显式调用
		// AvaloniaXamlLoader.Load(this)（否则 AVLN3000）；构建时该调用会被 IL 重写
		// 替换为编译好的资源填充（App.axaml 的 MergedDictionaries/Styles）。
		AvaloniaXamlLoader.Load(this);
	}

	private void RegisterGlobalExceptionLogging()
	{
		// Migration note：Avalonia Application 无 DispatcherUnhandledException 事件，
		// 等价物是 Dispatcher.UIThread.UnhandledException（参数同为 DispatcherUnhandledExceptionEventArgs）。
		Avalonia.Threading.Dispatcher.UIThread.UnhandledException += App_DispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
	}

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			// v4.0.5 兜底：UI 线程未处理异常此前只记日志、不置 Handled——异常继续上抛，
			// Dispatcher 直接终止进程（用户感知即"UI 无响应后崩溃退出"）。置 Handled=true
			// 让本帧中断、应用存活（用户可保存正在进行的合并/暂存状态）；完整现场已由
			// CrashDumper 独立落盘（crash-*.log），下次排障可直接取。
			CrashDumper.Dump("UI", e.Exception, "DispatcherUnhandledException (Handled=true, app continues)");
			e.Handled = true;
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// 终结性异常（进程必死，无法阻止）：崩溃转储必须先于 NLog 缓冲落盘，
			// 随后同步 flush 常规日志尾部再退出。
			CrashDumper.Dump(e.IsTerminating ? "AppDomain-fatal" : "AppDomain", e.ExceptionObject as Exception,
				"Unhandled AppDomain exception, IsTerminating=" + e.IsTerminating);
			CrashDumper.FlushLogs();
		}

		private void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
		{
			if (_loggedVisualParentingFirstChanceException || !IsVisualParentingArgumentException(e.Exception))
			{
				return;
			}
			_loggedVisualParentingFirstChanceException = true;
			string activeWindow = WpfApp.Windows.OfType<Window>().FirstOrDefault((Window x) => x.IsActive)?.GetType().FullName ?? "<none>";
			IInputElement focusedInputElement = Keyboard.FocusedElement;
			global::Avalonia.AvaloniaObject focusedDependencyObject = focusedInputElement as global::Avalonia.AvaloniaObject;
			string focusedElement = DescribeInputElement(focusedInputElement);
			string focusedElementAncestors = DescribeAncestors(focusedDependencyObject);
			string scrollContentPresenterDiagnostics = DescribeScrollContentPresenters(WpfApp.Windows.OfType<Window>().FirstOrDefault((Window x) => x.IsActive));
			string stackTrace = new StackTrace(1, fNeedFileInfo: true).ToString();
			Log.Warn("First-chance visual parenting exception" + Environment.NewLine + "ActiveWindow: " + activeWindow + Environment.NewLine + "FocusedElement: " + focusedElement + Environment.NewLine + "FocusedElementAncestors: " + focusedElementAncestors + Environment.NewLine + "ScrollContentPresenters:" + Environment.NewLine + scrollContentPresenterDiagnostics + Environment.NewLine + "CurrentStack:" + Environment.NewLine + stackTrace, e.Exception);
		}

		private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			// v4.0.5：未观测任务异常多数是后台刷新/IO 的噪声（.NET Core 起默认不再因此终止
			// 进程），但正是"UI 偶发无响应"排查时最需要的线索——落 crash 转储 + 显式置
			// Observed 防御任何遗留的 <ThrowUnobservedTaskExceptions> 配置。
			CrashDumper.Dump("Task-unobserved", e.Exception, "UnobservedTaskException (SetObserved=true)");
			e.SetObserved();
		}

		private static bool IsVisualParentingArgumentException(Exception ex)
		{
			ArgumentException argumentException = ex as ArgumentException;
			if (argumentException == null)
			{
				return false;
			}
			string message = argumentException.Message;
			if (string.IsNullOrEmpty(message))
			{
				return false;
			}
			return message.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void RegisterScrollViewerContentTemplateGuard()
		{
			// Migration note：WPF 侧通过 OverrideMetadata 拦截 ScrollViewer/ScrollContentPresenter 的
			// ContentTemplate 赋值来规避绑定报错；Avalonia 无 OverrideMetadata，且 ScrollViewer 模板
			// 机制不同，此防御逻辑整体降级为 no-op（相关诊断代码保留在 DescribeScrollContentPresenters）。
		}

		private static string DescribeInputElement(IInputElement element)
	{
		if (element == null)
		{
			return "<none>";
		}
		if (!(element is global::Avalonia.AvaloniaObject dependencyObject))
		{
			return element.GetType().FullName;
		}
		List<string> parts = new List<string>
		{
			VisualTreeAttachmentHelper.Describe(dependencyObject)
		};
		if (dependencyObject is global::Avalonia.Controls.Control frameworkElement)
		{
			parts.Add("DataContext=" + (frameworkElement.DataContext?.GetType().FullName ?? "<null>"));
			parts.Add("TemplatedParent=" + VisualTreeAttachmentHelper.Describe(frameworkElement.TemplatedParent));
		}
		// Migration note：WPF FrameworkContentElement 分支删除——Avalonia 无 ContentElement 体系，
		// 输入元素全部是 Control，上面的 Control 分支已覆盖。
		return string.Join(", ", parts);
	}

		private static string DescribeAncestors(global::Avalonia.AvaloniaObject dependencyObject, int maxDepth = 10)
		{
			if (dependencyObject == null)
			{
				return "<none>";
			}
			List<string> parts = new List<string>();
			global::Avalonia.AvaloniaObject dependencyObject2 = dependencyObject;
			for (int i = 0; dependencyObject2 != null && i < maxDepth; i++)
			{
				parts.Add(VisualTreeAttachmentHelper.Describe(dependencyObject2));
				dependencyObject2 = GetDebugParent(dependencyObject2);
			}
			if (dependencyObject2 != null)
			{
				parts.Add("...");
			}
			return string.Join(" -> ", parts);
		}

		private static global::Avalonia.AvaloniaObject GetDebugParent(global::Avalonia.AvaloniaObject child)
		{
			if (child == null)
			{
				return null;
			}
			// Migration note：Avalonia 逻辑父 = StyledElement.Parent（WpfCompat LogicalTreeHelper 垫片），
			// 视觉父 = VisualExtensions.GetVisualParent(Visual)，两者都要求目标类型，故先做模式匹配。
			if (child is global::Avalonia.StyledElement styledElement)
			{
				global::Avalonia.AvaloniaObject parent = LogicalTreeHelper.GetParent(styledElement);
				if (parent != null)
				{
					return parent;
				}
			}
			if (child is global::Avalonia.Visual visual)
			{
				return global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);
			}
			return null;
		}

		private static string DescribeScrollContentPresenters(global::Avalonia.AvaloniaObject root)
		{
			if (root == null)
			{
				return "<none>";
			}
			List<string> parts = new List<string>();
			CollectScrollContentPresenterDiagnostics(root, parts, 0);
			if (parts.Count == 0)
			{
				return "<none>";
			}
			return string.Join(Environment.NewLine, parts.Take(40));
		}

		private static void CollectScrollContentPresenterDiagnostics(global::Avalonia.AvaloniaObject item, List<string> parts, int depth)
	{
		if (item == null || depth > 80)
		{
			return;
		}
		// Migration note：WPF 该诊断同时走 Visual / Visual3D 两棵树；Avalonia 只有 Visual 一棵视觉树，
		// 且 VisualTreeHelper.GetChild/GetChildrenCount 只接受 Visual，非 Visual 的 AvaloniaObject 直接跳过。
		if (!(item is global::Avalonia.Visual visual))
		{
			return;
		}
		try
		{
			if (item is ScrollViewer scrollViewer && scrollViewer.ContentTemplate != null)
			{
				parts.Add("ScrollViewer " + VisualTreeAttachmentHelper.Describe(scrollViewer) + ", Content=" + DescribeObject(scrollViewer.Content) + ", ContentTemplate=" + DescribeObject(scrollViewer.ContentTemplate) + ", Ancestors=" + DescribeAncestors(scrollViewer, 8));
			}
			if (item is ScrollContentPresenter scrollContentPresenter && scrollContentPresenter.ContentTemplate != null)
			{
				parts.Add("ScrollContentPresenter " + VisualTreeAttachmentHelper.Describe(scrollContentPresenter) + ", Content=" + DescribeObject(scrollContentPresenter.Content) + ", ContentTemplate=" + DescribeObject(scrollContentPresenter.ContentTemplate) + ", Ancestors=" + DescribeAncestors(scrollContentPresenter, 8));
			}
			if (item is ContentPresenter contentPresenter && contentPresenter.ContentTemplate != null && item.GetType().Name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				parts.Add("Scroll-like ContentPresenter " + VisualTreeAttachmentHelper.Describe(contentPresenter) + ", Content=" + DescribeObject(contentPresenter.Content) + ", ContentTemplate=" + DescribeObject(contentPresenter.ContentTemplate) + ", Ancestors=" + DescribeAncestors(contentPresenter, 8));
			}
			int childrenCount = VisualTreeHelper.GetChildrenCount(visual);
			for (int i = 0; i < childrenCount; i++)
			{
				CollectScrollContentPresenterDiagnostics(VisualTreeHelper.GetChild(visual, i), parts, depth + 1);
			}
		}
		catch (Exception ex)
		{
			parts.Add("Diagnostics failed at " + VisualTreeAttachmentHelper.Describe(item) + ": " + ex.Message);
		}
	}

		private static string DescribeObject(object item)
		{
			if (item == null)
			{
				return "<null>";
			}
			if (item is global::Avalonia.AvaloniaObject dependencyObject)
			{
				return VisualTreeAttachmentHelper.Describe(dependencyObject);
			}
			return item.GetType().FullName;
		}

		public static void RefreshWindowBorderBrush()
		{
			// Bug 修复（2026-09-09，"彩色主题下弹窗外圈颜色仍不一致，Solarized 浅色却正常"的残余根因）：
			// 原逻辑在 Windows 注册表 HKCU\Software\Microsoft\Windows\DWM\ColorPrevalence > 0
			// （系统"在标题栏/窗口边框显示强调色"，Win10/11 常见开启）时，边框刷取
			// SystemAccentBrush——即【系统】强调色（常见默认蓝），而非当前【主题】的 AccentColor。
			// SolarizedLight 恰为蓝色系（#268BD2）故看不出异常；Purple/Green/Orange 等彩色主题下，
			// 弹窗四周 1px 边框浮出一圈系统蓝 → "外圈颜色不一致"。2026-09-08 修复只改了
			// ColorPrevalence 关闭的分支，开启的分支漏网。
			// 修复：跨平台移植后窗口边框【一律】跟随主题 AccentColor（每套 Colors.*.axaml 均定义），
			// 彻底移除系统强调色分支，保证任意主题下边框与主题配色统一；
			// 取不到 AccentColor（headless/启动极早期）时回退浅/深默认边框刷。
			// 注：Avalonia 12 中 Color 位于 Avalonia.Media（非 WPF 的直接命名空间）；
			// ImmutableSolidColorBrush 实现接口而非继承 Brush，无法赋给 Brush 类型字段，
			// 故用可变 SolidColorBrush（与 _defaultWindowBorder*Brush 同类型）。
			SolidColorBrush solidColorBrush = (ForkPlusSettings.Default.Theme.IsDarkBase() ? _defaultWindowBorderDarkBrush : _defaultWindowBorderLightBrush);
			Brush brush = solidColorBrush;
			if (global::Avalonia.Application.Current != null && global::Avalonia.Application.Current.TryGetResource("AccentColor", global::Avalonia.Application.Current.ActualThemeVariant, out var accentValue) && accentValue is global::Avalonia.Media.Color)
			{
				// 新建 SolidColorBrush 值快照，避免引用共享资源实例时
				// DynamicResource 未解析/实例相等误判。
				brush = new SolidColorBrush((global::Avalonia.Media.Color)accentValue);
			}
			if (brush != _windowBorderBrush)
			{
				_windowBorderBrush = brush;
				// Migration note：WPF Brush.Freeze()（把画刷冻结为不可变以提升共享性能）在 Avalonia 无对应方法；
				// Avalonia 用 ImmutableBrush 体系表达不可变，直接引用共享即可，故删除 Freeze 调用。
				ResourceDictionary resourceDictionary = new ResourceDictionary();
				resourceDictionary.Add("WindowBorderBrush", _windowBorderBrush);
				Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
				if (_windowsBorderResourceDictionary != null)
				{
					Application.Current.Resources.MergedDictionaries.Remove(_windowsBorderResourceDictionary);
				}
				_windowsBorderResourceDictionary = resourceDictionary;
				global::ForkPlus.UI.Theme.Refresh();
			}
		}

		// Migration note：WPF App.OnStartup(StartupEventArgs) 由 Application.Startup 事件驱动；
		// Avalonia 无该事件，启动入口是 OnFrameworkInitializationCompleted()（见文件末尾重写），
		// 主体逻辑保留在 RunStartup() 中由其调用。
		private void RunStartup()
		{
			ServiceLocator.Initialize(
				dispatcher: new WpfDispatcher(global::Avalonia.Threading.Dispatcher.UIThread),
				designMode: new WpfDesignModeService(),
				appContext: new WpfAppContext(),
				clipboard: new WpfClipboardService(),
				timer: new WpfTimerService(),
				toast: new WpfToastNotificationService(),
				windowManager: new WpfWindowManagerService()
			);
			_ = IsDebug;
			InitializeRenderMode();
			InitializeTheme();
			RefreshWindowBorderBrush();
			SubscribeToUserPreferences();
			if (!Environment.Is64BitOperatingSystem)
			{
				new ForkPlus.UI.Dialogs.MessageBoxWindow("Unsupported Platform", "Currently Fork doesn't support 32-bit Windows", "OK", showCancelButton: false, showWarningIcon: true).ShowDialog();
			}
			else if (IsDebug || InitializeForkInstance())
			{
				ConfigureThreadPool();
				// Migration note：WPF new MainWindow().Show()；Avalonia 需同时把主窗口赋给
				// IClassicDesktopStyleApplicationLifetime.MainWindow（lifetime 主窗口跟踪，
				// WpfApp.MainWindow / 关闭逻辑都依赖它）。
				MainWindow mainWindow = new MainWindow();
				if (global::ForkPlus.UI.WpfCompat.WpfApp.Lifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
				{
					desktop.MainWindow = mainWindow;
				}
				mainWindow.Show();
			}
		}

		// ===== 主题字典（Generic.{Skin}.axaml）加载/查找辅助 =====
		// Migration note：WPF 用 new ResourceDictionary { Source = new Uri("/ForkPlus;component/Theme/Generic.x.axaml") }
		// 合并外部主题字典、用 rd.Source 反查；Avalonia 的 ResourceDictionary 没有 Source，
		// 等价做法是 ResourceInclude(baseUri) { Source = avares URI } 放进 MergedDictionaries，
		// 反查时遍历 MergedDictionaries（IList<IResourceProvider>）按 ResourceInclude.Source 匹配。

		/// <summary>主题字典 URI 前缀（ThemeTypeExtensions.ResourceUri 生成的 avares 绝对地址）。</summary>
		private const string ThemeDictionaryUriPrefix = "avares://ForkPlus/Theme/Generic.";

		/// <summary>ResourceInclude 的 baseUri（解析相对 Source 用；Source 为绝对 avares URI 时仅作占位）。</summary>
		private static readonly Uri ThemeResourceIncludeBaseUri = new Uri("avares://ForkPlus/App.axaml", UriKind.Absolute);

		/// <summary>创建加载 Generic.{Skin}.axaml 的 ResourceInclude（WPF ResourceDictionary.Source 的等价物）。</summary>
		internal static ResourceInclude CreateThemeResourceInclude(Uri themeUri)
		{
			return new ResourceInclude(ThemeResourceIncludeBaseUri)
			{
				Source = themeUri
			};
		}

		/// <summary>
		/// 在 Application.Resources.MergedDictionaries 中查找当前主题字典。
		/// 保持 WPF"按 Uri 识别主题字典"语义：Source 匹配 avares://ForkPlus/Theme/Generic.{Skin}.axaml。
		/// </summary>
		internal static ResourceInclude FindThemeResourceInclude()
		{
			var merged = Application.Current?.Resources?.MergedDictionaries;
			if (merged == null)
			{
				return null;
			}
			// MergedDictionaries 是 IList<IResourceProvider>；元素可能为 ResourceDictionary（代码构建）
			// 或 ResourceInclude（axaml/外部加载），只按 ResourceInclude.Source 识别主题字典。
			foreach (var provider in merged)
			{
				if (provider is ResourceInclude include &&
				    include.Source != null &&
				    include.Source.OriginalString.StartsWith(ThemeDictionaryUriPrefix, StringComparison.OrdinalIgnoreCase))
				{
					return include;
				}
			}
			return null;
		}

		/// <summary>把 FluentTheme 的明暗变体（Application.RequestedThemeVariant）同步到当前皮肤的
	/// 基底明暗（2026-09-04，"切换主题时部分组件样式没包进去、突兀"根因修复）。
	///
	/// 根因：App.Styles 同时加载 FluentTheme + AvaloniaEdit(Fluent) + OxyPlot(Default) 三个
	/// 外来主题，其余自有控件主题全部走皮肤字典（DynamicResource 随换肤刷新）。但全仓库从未
	/// 设置 RequestedThemeVariant——Fluent 系控件只看这个变体选明暗（默认跟随操作系统，与
	/// 应用内皮肤选择完全脱钩）：用户切到 Dark/Monokai/Dracula 等暗皮肤时，走 Fluent 渲染的
	/// 控件（未被自有 ControlTheme 覆盖的兜底控件 + AvaloniaEdit 编辑器内部子控件如搜索面板/
	/// 滚动条 + OxyPlot 图表内部元素）仍是亮色外观，反之亦然——切换瞬间一块区域"亮岛"突兀。
	///
	/// 修复：皮肤加载/切换时按 IsDarkBase 设置变体，Fluent 系与皮肤字典同步明暗。
	/// 纯机制修复不涉及配色——Fluent 变体只有 Light/Dark 二元，22 皮肤按基底归类
	///（IsDarkBase，与 WebView2 PreferredColorScheme 同一分界）。</summary>
	internal static void SyncThemeVariant(global::ForkPlus.UI.ThemeType theme)
	{
		if (Application.Current == null)
		{
			return;
		}
		Application.Current.RequestedThemeVariant =
			theme.IsDarkBase() ? ThemeVariant.Dark : ThemeVariant.Light;
	}

	private void InitializeTheme()
		{
			if (ForkPlusSettings.Default.FollowSystemTheme)
			{
				_systemTheme = GetSystemTheme();
				// 跟随系统时只映射到基底 Light/Dark（系统只有明暗二元）
				ForkPlusSettings.Default.Theme = ((_systemTheme != 0) ? ThemeType.Dark : ThemeType.Light);
			}
			// 找到当前的 Generic.{Skin}.axaml 字典（ResourceInclude 且 Source 匹配主题前缀）
			ResourceInclude oldThemeInclude = FindThemeResourceInclude();
			ResourceInclude item = CreateThemeResourceInclude(ForkPlusSettings.Default.Theme.ResourceUri());
			Application.Current.Resources.MergedDictionaries.Add(item);
			if (oldThemeInclude != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(oldThemeInclude);
			}
			// 同步 FluentTheme/AvaloniaEdit/OxyPlot 的明暗变体（详见 SyncThemeVariant 注释）
			SyncThemeVariant(ForkPlusSettings.Default.Theme);
			global::ForkPlus.UI.Theme.SubscribeToSystemEvents();
			InitializeTextEditorContextMenuStyle();
			ApplyCustomColors();
		}

		/// <summary>根据 ForkPlusSettings.Default.CustomColors 构建动态 ResourceDictionary 并 merge 到
	/// MergedDictionaries 末尾。仅当 UseCustomColors=true 且 CustomColors 非空时才应用覆盖。
	///
	/// v2.1.2 关键修复：用户反馈"换色后主界面不刷新，必须重启才生效"。根因——
	/// 旧实现只在 MergedDictionaries 末尾 Add 一个含 29 个 Color key 的小 dict，依赖
	/// Brushes.xaml 中 SolidColorBrush.Color = {DynamicResource XXXColor} 的链式通知自动更新。
	/// 但 WPF 在 Style/Template 已实例化、控件已渲染后，对 MergedDictionaries 末尾 Add
	/// 同名 key 的覆盖不会可靠地触发所有 DynamicResource 重新解析——尤其是 Style 中
	/// Setter 引用的 Brush、ContextMenu/Popup 内的控件、已渲染过的 UserControl 等，
	/// 表现为"换色后只有部分 UI 刷新，主界面整体不变化"。
	///
	/// 对比主题切换（SwitchApplicationThemeCommand）能立即刷新——因为它**重新加载
	/// 整个 Generic.{Skin}.xaml 字典**（先 Add 新 dict → 后 Remove 旧 dict），这会强制
	/// WPF 让所有 DynamicResource 失效并重新解析，所有 SolidColorBrush 实例被重建，
	/// 所有引用 Brush 的控件（包括 Style/Popup/已渲染控件）都拿到新 Brush。
	///
	/// 修复策略：模仿主题切换的做法，在 ApplyCustomColors 末尾对当前 Generic 字典
	/// 做一次"Add 新 + Remove 旧"的等效刷新——重新加载同一份 Generic.{Skin}.xaml，
	/// 强制 WPF 全量失效所有 DynamicResource。然后再 Add 自定义颜色覆盖字典。
	/// 这样换色效果和主题切换一样立即生效，性能代价是重新加载一份 ~290 Color + 270 Brush
	/// 的字典（毫秒级，可接受）。
	///
	/// 末尾 raise ApplicationThemeChanged 事件，通知 18 个订阅控件（DiffEditor/Heatmap 等）
	/// 主动刷新缓存的 Color 值（这些控件缓存 Color 值类型，必须靠事件刷新）。</summary>
	public static void ApplyCustomColors()
	{
		// 移除旧的自定义颜色字典
		if (_customColorsResourceDictionary != null)
		{
			Application.Current.Resources.MergedDictionaries.Remove(_customColorsResourceDictionary);
			_customColorsResourceDictionary = null;
		}
		// 仅当用户启用自定义颜色且有自定义项时才应用覆盖；否则使用当前主题原色。
		Dictionary<string, string> customColors = ForkPlusSettings.Default.CustomColors;
		bool hasCustomColors = ForkPlusSettings.Default.UseCustomColors && customColors != null && customColors.Count > 0;

		// 关键：重新加载当前主题的 Generic.{Skin}.xaml 字典，模仿主题切换的强力刷新机制。
		// 这一步强制 WPF 让所有 DynamicResource 失效并重新解析，所有 SolidColorBrush 实例
		// 被重建，所有引用 Brush 的控件（含 Style/Popup/已渲染控件）都会刷新——
		// 这是"主题切换能立即生效"的根因，自定义颜色同样需要走这条路径。
		ReloadThemeDictionary();

		// 构建新的覆盖字典并 Add 到 MergedDictionaries 末尾
		if (hasCustomColors)
		{
			ResourceDictionary dict = new ResourceDictionary();
			foreach (KeyValuePair<string, string> kv in customColors)
			{
				try
				{
					string hex = kv.Value;
					Color color;
					if (hex.StartsWith("#") && hex.Length == 9)
						color = (Color)ColorConverter.ConvertFromString(hex);
					else if (hex.StartsWith("#") && hex.Length == 7)
						color = (Color)ColorConverter.ConvertFromString(hex);
					else
						color = (Color)ColorConverter.ConvertFromString("#" + hex);
					dict[kv.Key] = color;
				}
				catch (Exception ex)
				{
					Log.Warn("Invalid custom color value for key '" + kv.Key + "': " + kv.Value, ex);
				}
			}
			if (dict.Count > 0)
			{
				Application.Current.Resources.MergedDictionaries.Add(dict);
				_customColorsResourceDictionary = dict;
			}
		}
		global::ForkPlus.UI.Theme.Refresh();
		// raise 事件让订阅者刷新缓存的颜色/画刷，实现自定义颜色实时生效。
		NotificationCenter.Current.RaiseApplicationThemeChanged(Application.Current, ForkPlusSettings.Default.Theme);
	}

	/// <summary>重新加载当前主题的 Generic.{Skin}.xaml 字典：先 Add 新 dict → 后 Remove 旧 dict。
	/// 这是 SwitchApplicationThemeCommand 主题切换能立即刷新所有 UI 的核心机制——
	/// 通过替换整个 Generic 字典让 WPF 强制让所有 DynamicResource 失效并重新解析，
	/// 所有 SolidColorBrush 实例被重建，所有引用 Brush 的控件（含 Style/Popup/已渲染控件）
	/// 都拿到新 Brush。自定义颜色变化时同样调用此方法，让换色效果像主题切换一样即时生效。</summary>
	private static void ReloadThemeDictionary()
	{
		try
		{
			// Bug 修复（2026-09-07，"切换主题就有可能导致 UI 崩溃"）：换字典前先释放 popup 托管的
			// 孤儿 ItemsPresenter（自定义颜色路径同样替换 Generic 字典 → 全量模板重建，与主题切换
			// 同一竞态）。详见 PopupItemsPresenterRelease 类注释。
			global::ForkPlus.UI.PopupItemsPresenterRelease.ReleaseOrphaned();

			// 找到当前的 Generic.{Skin}.axaml 字典（ResourceInclude 且 Source 匹配 avares://ForkPlus/Theme/Generic.*.axaml）
			// Migration note：原 WPF 代码 foreach(ResourceDictionary rd in MergedDictionaries) + rd.Source 在
			// Avalonia 报 CS1061（MergedDictionaries 元素是 IResourceProvider，且 ResourceDictionary 无 Source）；
			// 改为 FindThemeResourceInclude()：遍历并按 ResourceInclude.Source 识别主题字典。
			ResourceInclude oldThemeInclude = FindThemeResourceInclude();
			if (oldThemeInclude == null)
				return;  // 未找到主题字典（启动早期或异常状态），跳过刷新

			// 先 Add 新 dict（同一 Source 重新加载），后 Remove 旧 dict——
			// 这个顺序与 SwitchApplicationThemeCommand 一致，确保资源查找不出现空窗。
			ResourceInclude newThemeInclude = CreateThemeResourceInclude(ForkPlusSettings.Default.Theme.ResourceUri());
			Application.Current.Resources.MergedDictionaries.Add(newThemeInclude);
			Application.Current.Resources.MergedDictionaries.Remove(oldThemeInclude);
		}
		catch (Exception ex)
		{
			Log.Warn("ReloadThemeDictionary failed: " + ex.Message, ex);
		}
	}

		private void InitializeTextEditorContextMenuStyle()
		{
			try
			{
				Type nestedType = typeof(TextElement).Assembly.GetType("System.Windows.Documents.TextEditorContextMenu").GetNestedType("EditorContextMenu", BindingFlags.NonPublic);
				Style value = Application.Current.Resources[typeof(ContextMenu)] as Style;
				Application.Current.Resources.Add(nestedType, value);
			}
			catch (Exception ex)
			{
				Log.Error("Cannot initialize TextEditorContextMenu style: " + ex.Message);
			}
		}

		private void InitializeRenderMode()
		{
			if (ForkPlusSettings.Default.DisableHardwareAcceleration)
			{
				RenderOptionsShim.ProcessRenderMode = RenderMode.SoftwareOnly;
			}
		}

		private void SubscribeToUserPreferences()
		{
			try
			{
				SystemEvents.UserPreferenceChanged += delegate(object s, UserPreferenceChangedEventArgs e)
				{
					Log.Info($"System event: UserPreferenceChanged ({e.Category})");
					if (e.Category == UserPreferenceCategory.General)
					{
						RefreshWindowBorderBrush();
						RefreshTheme();
					}
				};
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
		}

		private void RefreshTheme()
		{
			if ((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow != null)
			{
				SystemTheme systemTheme = GetSystemTheme();
				if (systemTheme != _systemTheme)
				{
					_systemTheme = systemTheme;
					// 跟随系统变化时映射到基底 Light/Dark
					ThemeType newTheme = ((_systemTheme != 0) ? ThemeType.Dark : ThemeType.Light);
					ForkPlus.UI.MainWindow.Commands.SwitchApplicationTheme.Execute(newTheme, followSystemTheme: true);
				}
			}
		}

		private bool InitializeForkInstance()
		{
			ForkPlusSettings @default = ForkPlusSettings.Default;
			if (!IsGitInstanceAvailable())
			{
				if (!new ConfigureGitInstanceWindow().ShowDialog().GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			WarnIfGitVersionUnsupported(GitPath);
			if (string.IsNullOrEmpty(@default.Guid))
			{
				if (!new WelcomeWindow().ShowDialog().GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			@default.MigratedToFork2_10_3 = true;
			return true;
		}

		public static bool IsGitInstanceAvailable()
		{
			// 仅检查 git.exe 路径是否存在；版本检测由 WarnIfGitVersionUnsupported 统一完成，
			// 避免每次启动重复启动 git version 子进程（原实现会执行 2 次子进程）。
			string gitPath = GitPath;
			return !string.IsNullOrWhiteSpace(gitPath) && File.Exists(gitPath);
		}

		/// <summary>
		/// 检测当前 git 版本，过低时弹警告（不阻止启动）。
		/// </summary>
		private static void WarnIfGitVersionUnsupported(string gitPath)
		{
			try
			{
				GitVersionCheckResult result = GitVersionChecker.Check(gitPath);
				if (result.Status == GitVersionStatus.Unsupported)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string minText = GitVersionChecker.MinimumRequiredVersion.ToString(2);
					string msg = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is older than the required {1}. Some features (diff, status, empty-changes detection) may not work correctly. Please upgrade git.",
						versionText, minText);
					new ForkPlus.UI.Dialogs.MessageBoxWindow(ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version too old"), msg, "OK", showCancelButton: false, showWarningIcon: true).ShowDialog();
				}
				else if (result.Status == GitVersionStatus.Outdated)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string recText = GitVersionChecker.RecommendedVersion.ToString(2);
					string msg = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
						versionText, recText);
					new ForkPlus.UI.Dialogs.MessageBoxWindow(ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version outdated"), msg, "OK", showCancelButton: false).ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git version", ex);
			}
		}

		// Migration note：WPF App.OnExit(ExitEventArgs) 由 Application.Exit 事件驱动；
		// Avalonia 挂 IClassicDesktopStyleApplicationLifetime.ShutdownRequested（见文件末尾）。
		private void RunExit()
		{
			ForkPlusSettings.Default.Save();
			_askPassIpcServer.Dispose();
			_defaultIpcServer.Dispose();
		}

		private void DoShutdown()
	{
		// Migration note：WPF Application.Shutdown() 在 Startup 事件里调用合法（消息循环已运行）；
		// Avalonia 12 的 OnFrameworkInitializationCompleted 发生在 lifetime.Start 进
		// MainLoop 之前，此刻直接 Lifetime.Shutdown 会关闭 Dispatcher，随后的
		// Dispatcher.UIThread.MainLoop 抛 "InvalidOperationException: Dispatcher shut down"
		// （首启取消路径实证：Welcome 对话框返回 false → DoShutdown → MainLoop 崩）。
		// 改为 Post 延迟到主循环运行后执行；调用方均为 DoShutdown+return false 形态，
		// 无同步停止依赖，语义等价。
		global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
		{
			global::ForkPlus.UI.WpfCompat.WpfApp.Shutdown();
		});
	}

		private static string GetEnvironmentGitInstancePath()
		{
			try
			{
				string environmentVariable = Environment.GetEnvironmentVariable(Consts.ForkPlus.GitInstanceEnvVariable);
				if (environmentVariable != null)
				{
					// Migration note：git 二进制名跨平台（Unix 无 .exe）。变量可能直接指向 git 路径或其所在目录。
					if (SystemEnvironment.IsGitExecutable(environmentVariable) && File.Exists(environmentVariable))
					{
						return environmentVariable;
					}
					string text = Path.Combine(environmentVariable, "bin", SystemEnvironment.GitExecutableName);
					if (File.Exists(text))
					{
						return text;
					}
					// Migration note：Unix 上 git 通常在目录本身（/usr/bin 形式传入时下面直接拼名字）。
					string text2 = Path.Combine(environmentVariable, SystemEnvironment.GitExecutableName);
					if (File.Exists(text2))
					{
						return text2;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static string GetForkGitInstancePath()
		{
			// Migration note：git 二进制名跨平台。
			return Path.Combine(ForkDirectoryPath, "gitInstance", "2.50.1", "bin", SystemEnvironment.GitExecutableName);
		}

		private static void MigrateLegacyAppData()
		{
			MigrateDirectoryIfNeeded(LegacyForkDirectoryPath, ForkDirectoryPath);
			MigrateDirectoryIfNeeded(LegacyForkDataDirectoryPath, ForkDataDirectoryPath);
		}

		private static void MigrateDirectoryIfNeeded(string sourceDirectory, string destinationDirectory)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(sourceDirectory))
				{
					return;
				}
				CopyDirectory(sourceDirectory, destinationDirectory);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to migrate legacy app data from '" + sourceDirectory + "' to '" + destinationDirectory + "'", ex);
			}
		}

		private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
		{
			Directory.CreateDirectory(destinationDirectory);
			foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(directory.Replace(sourceDirectory, destinationDirectory));
			}
			foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string destinationFile = file.Replace(sourceDirectory, destinationDirectory);
				if (!File.Exists(destinationFile))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
					File.Copy(file, destinationFile);
				}
			}
		}

		private void HandleCommandLineArguments()
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			if (commandLineArgs.Length <= 1)
			{
				return;
			}
			Process currentProcess = Process.GetCurrentProcess();
			Process process = IReadOnlyListExtensions.FirstItem(Process.GetProcessesByName(currentProcess.ProcessName), (Process x) => x.Id != currentProcess.Id);
			if (process == null)
			{
				return;
			}
			NamedPipeClientStream namedPipeClientStream = NamedPipeHelper.CreatePipeClient(NamedPipeHelper.DefaultPipeName, process);
			string currentDirectory = Directory.GetCurrentDirectory();
			try
			{
				namedPipeClientStream.Connect(100);
				namedPipeClientStream.WriteString(DefaultIpcPipe_CliRequest);
				namedPipeClientStream.WriteString(currentDirectory);
				namedPipeClientStream.WriteString(string.Join(DefaultIpcPipe_StringSeparator, commandLineArgs));
				string text = namedPipeClientStream.ReadString();
				namedPipeClientStream.Close();
				if (text == DefaultIpcPipe_Handled)
				{
					Environment.Exit(0);
				}
			}
			catch (Exception arg)
			{
				Log.Warn($"Can't connect to other Fork process pipe {process.Id.ToString()}. {arg}");
			}
		}

		private void AskPassIpcMessageHandler(NamedPipeServerStream pipeServer)
		{
			string text = ReadStringFromPipe(pipeServer);
			if (text == null)
			{
				return;
			}
			string[] array = text.Split(new char[1], 3);
			string text2 = array[0];
			string repositoryPath = array[1];
			string request = array[2];
			bool noPrompt = text2 == "1" || text2 == "3";
			if (text2 == "2" || text2 == "3")
			{
				// 凭据收编（Layer C）get：账号命中回填；未命中查 GCM 兼容键
				// （GCM 存量凭据无痛迁移）；仍无 → 空响应，git 回落 askpass 链弹 AskPassWindow。
				CredentialHelperArguments credentialHelperArguments = CredentialHelperArguments.Parse(request);
				if (credentialHelperArguments != null)
				{
					Account account = AccountManager.Current.FindAccount(credentialHelperArguments.Host, credentialHelperArguments.Username);
					if (account != null)
					{
						credentialHelperArguments.Username = account.Username;
						credentialHelperArguments.Password = account.Service.Connection.Authentication.GetHttpsPassword();
						pipeServer.WriteString(credentialHelperArguments.Export());
						return;
					}
					if (GcmCompatibleStore.TryQuery(credentialHelperArguments.Protocol, credentialHelperArguments.Host, credentialHelperArguments.Username, out string storedUsername, out string storedPassword))
				{
					credentialHelperArguments.Username = storedUsername ?? credentialHelperArguments.Username;
					credentialHelperArguments.Password = storedPassword;
					pipeServer.WriteString(credentialHelperArguments.Export());
					return;
				}
				// 凭据记忆（Layer D）第三档（记住密码 + 不再弹出）：静默回填（跨平台，
				// 非 Windows 上 GCM 兼容键为空操作——这里是 Linux/macOS 不弹窗的主路径）。
				// 第二档（记住密码未开不再弹出）不命中——回落 askpass 弹窗（密码框预填）。
				if (SavedCredentialStore.Current.TryGetSilentCredential(credentialHelperArguments.Host, out string rememberedUsername, out string rememberedPassword))
				{
					credentialHelperArguments.Username = rememberedUsername ?? credentialHelperArguments.Username;
					credentialHelperArguments.Password = rememberedPassword;
					pipeServer.WriteString(credentialHelperArguments.Export());
					return;
				}
				}
				pipeServer.WriteString(string.Empty);
			}
			else if (text2 == "4")
			{
				// 凭据收编（Layer C）store：认证成功的凭据写 GCM 兼容键，下次 get 静默命中
				// （GCM 外部读写互通；非 Windows 为空操作，见设计文档权衡一节）。
				CredentialHelperArguments credentialHelperArguments2 = CredentialHelperArguments.Parse(request);
				if (credentialHelperArguments2 != null)
				{
					GcmCompatibleStore.Store(credentialHelperArguments2.Protocol, credentialHelperArguments2.Host, credentialHelperArguments2.Username, credentialHelperArguments2.Password);
				}
				pipeServer.WriteString(string.Empty);
			}
			else if (text2 == "5")
			{
				// 凭据收编（Layer C）erase：认证失败时 git 通知抹除，删 GCM 兼容键。
				CredentialHelperArguments credentialHelperArguments3 = CredentialHelperArguments.Parse(request);
				if (credentialHelperArguments3 != null)
				{
					GcmCompatibleStore.Erase(credentialHelperArguments3.Protocol, credentialHelperArguments3.Host, credentialHelperArguments3.Username);
					// 凭据记忆（Layer D）联动：密码已失效——清"记住的密码"防死循环
					// （否则 get 永远命中旧密码、永远认证失败），保留账号记忆与"不再询问"标记。
					SavedCredentialStore.Current.ForgetPassword(credentialHelperArguments3.Host);
				}
				pipeServer.WriteString(string.Empty);
			}
			else
			{
				string askPassResult = string.Empty;
				global::Avalonia.Threading.Dispatcher.UIThread.Sync(delegate
				{
					ForkPlus.UI.MainWindow.Commands.ShowAskPassWindow.Execute(request, noPrompt, repositoryPath, out askPassResult);
				});
				pipeServer.WriteString(askPassResult ?? string.Empty);
			}
		}

		private void DefaultIpcMessageHandler(NamedPipeServerStream pipeServer)
		{
			string text = ReadStringFromPipe(pipeServer);
			if (text == null)
			{
				Log.Error("Cannot read ipcMessage from pipe");
			}
			else if (text == DefaultIpcPipe_CliRequest)
			{
				string workingDirectory = ReadStringFromPipe(pipeServer);
				if (workingDirectory == null)
				{
					Log.Error("Cannot read workingDirectory from pipe");
					return;
				}
				string text2 = ReadStringFromPipe(pipeServer);
				if (text2 == null)
				{
					Log.Error("Cannot read cliRequest from pipe");
					return;
				}
				string[] args = text2.Split(new string[1] { DefaultIpcPipe_StringSeparator }, StringSplitOptions.None);
				base.Dispatcher.Sync(delegate
				{
					CliCommand.CreateCliCommand(args)?.Run(workingDirectory);
				});
				if (WriteStringToPipe(pipeServer, DefaultIpcPipe_Handled) != -1)
				{
					Log.Error("Cannot read cliRequest from pipe");
				}
				base.Dispatcher.Post(delegate
				{
					Window mainWindow = global::ForkPlus.UI.WpfCompat.WpfApp.MainWindow;
					if (mainWindow != null)
					{
						mainWindow.Activate();
						mainWindow.Topmost = true;
						mainWindow.Topmost = false;
					}
				});
			}
			else
			{
				Log.Error("Unknown IPC message '" + text + "'");
			}
		}

		private static void ConfigureThreadPool()
		{
			ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
			ThreadPool.SetMinThreads(Math.Max(workerThreads, 10), completionPortThreads);
		}

		// Migration note（2026-09-09 删除）：IsSystemAccentBrushEnabled（读 HKCU\...\DWM\ColorPrevalence）
		// 原用于决定窗口边框是否跟随系统强调色；跨平台移植后边框一律跟随主题 AccentColor
		// （见 RefreshWindowBorderBrush 的修复注释），该分支已删除，方法随之移除。

		private static SystemTheme GetSystemTheme()
		{
			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
				object obj = registryKey?.GetValue("AppsUseLightTheme");
				if (obj != null)
				{
					return ((int)obj <= 0) ? SystemTheme.Dark : SystemTheme.Light;
				}
				return (ForkPlusSettings.Default.Theme != 0) ? SystemTheme.Dark : SystemTheme.Light;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read system theme from Windows registry", ex);
				return SystemTheme.Light;
			}
		}

		[Null]
		private string ReadStringFromPipe(PipeStream pipeStream)
		{
			try
			{
				return pipeStream.ReadString();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read string from pipe", ex);
				return null;
			}
		}

		private int WriteStringToPipe(PipeStream pipeStream, string stringToWrite)
		{
			try
			{
				return pipeStream.WriteString(stringToWrite);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write string to pipe", ex);
				return -1;
			}
		}
        // Migration note：WPF Application 的 Startup/Exit 生命周期事件在 Avalonia 不存在。
        // 启动入口 = OnFrameworkInitializationCompleted（RunStartup 即原 OnStartup 主体：
        // ServiceLocator/主题/边框画刷/首选项订阅/git 实例校验/主窗口显示）；
        // 退出 = desktop.ShutdownRequested（RunExit 即原 OnExit 主体：保存设置 + 释放 IPC）。
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownRequested += delegate (object sender, global::Avalonia.Controls.ApplicationLifetimes.ShutdownRequestedEventArgs e)
                {
                    RunExit();
                };
                RunStartup();
            }

            base.OnFrameworkInitializationCompleted();
        }
	}
}
