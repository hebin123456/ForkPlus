using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ForkPlus.UI
{
	public abstract class ShellTool
	{
		/// <summary>
		/// Unix 下按优先级探测的终端模拟器候选（名称即可，经 PATH 解析）。
		/// xdg-terminal-exec 是 freedesktop 的"启动默认终端"规范入口，优先；
		/// 之后按各桌面环境常见终端排序；末尾 x-terminal-emulator 是 Debian
		/// alternatives 体系的替代符号链接。这些终端在无参数启动时默认继承
		/// 当前工作目录（Process.Start 的 WorkingDirectory = 仓库路径），
		/// 与 Windows 版"控制台按钮打开 git-bash 于仓库目录"语义一致。
		/// </summary>
		public static readonly string[] UnixTerminalEmulatorCandidates = new string[]
		{
			"xdg-terminal-exec",
			"gnome-terminal",
			"konsole",
			"xfce4-terminal",
			"mate-terminal",
			"lxterminal",
			"kitty",
			"alacritty",
			"foot",
			"wezterm",
			"terminator",
			"tilix",
			"guake",
			"io.elementary.terminal",
			"deepin-terminal",
			"urxvt",
			"st",
			"xterm",
			"x-terminal-emulator"
		};

		/// <summary>
		/// 在 PATH 里查找第一个存在的终端模拟器，返回完整路径；找不到返回 null。
		/// Linux 控制台按钮依赖它：原版实现直接启动 git 同目录的 bash——Windows 上
		/// GUI 进程启动 bash.exe 会自动分配新的控制台窗口；但 Unix 上 bash 无 TTY
		/// 时立即退出（stdin EOF），点了没有任何反应（用户报告的 bug）。必须由
		/// 终端模拟器承载 shell。
		/// </summary>
		public static string FindUnixTerminalEmulator()
		{
			return FindUnixTerminalEmulator(null);
		}

		/// <summary>
		/// 同 <see cref="FindUnixTerminalEmulator()"/>，允许注入 PATH 值（测试用：
		/// xunit 跨 collection 并行，改进程级 PATH 环境变量有竞态风险——参数注入，
		/// TokeiResolutionTests / App.FindExecutableInPath(string,string) 同款先例）。
		/// </summary>
		internal static string FindUnixTerminalEmulator(string pathEnvironmentOverride)
		{
			foreach (string name in UnixTerminalEmulatorCandidates)
			{
				try
				{
					string path = App.FindExecutableInPath(name, pathEnvironmentOverride);
					if (!string.IsNullOrEmpty(path))
					{
						return path;
					}
				}
				catch (Exception ex)
				{
					Log.Error("Failed to locate terminal emulator '" + name + "'", ex);
				}
			}
			return null;
		}

		public class Default : ShellTool
		{
			public override string Type => DefaultType;

			public override string DisplayName => "Console";

			public override string Arguments => null;

			public override string ApplicationPath
			{
				get
				{
					// Unix migration note（Linux 控制台点击无反应的根因）：
					// 原版返回 <git目录>/bash 并直接 Process.Start。Windows 上 GUI
					// 进程启动控制台子系统的 bash.exe 会自动分配新控制台窗口，一切正常；
					// Unix 上裸 bash 没有 TTY（stdin 非 tty），读到 EOF 立即退出，
					// 用户看到的是"点了没反应"。此外 Linux 上 bash 与 git 常不同目录
					//（如自编译 git 在 /usr/local/bin，bash 在 /usr/bin），File.Exists
					// 也会失败。Unix 改为解析终端模拟器（默认继承 WorkingDirectory），
					// 终端内运行用户登录 shell，语义等价于 Windows 的"打开控制台"。
					if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						return FindUnixTerminalEmulator();
					}
					string directoryName = Path.GetDirectoryName(App.GitPath);
					if (directoryName == null)
					{
						Log.Error("Cannot find git directory '" + directoryName + "'");
						return null;
					}
					return Path.Combine(directoryName, Consts.ForkPlus.BashFilename);
				}
			}
		}

		public class Custom : ShellTool
		{
			public override string Type => CustomType;

			public override string DisplayName => "Shell";

			public override string Arguments { get; }

			public override string ApplicationPath { get; }

			public Custom(string applicationPath, [Null] string arguments)
			{
				ApplicationPath = applicationPath;
				Arguments = arguments;
			}
		}

		public class WindowsTerminal : ShellTool
		{
			public override string Type => WindowsTerminalType;

			public override string DisplayName => "Terminal";

			public override string ApplicationPath { get; }

			public override string Arguments => "-d .";

			public WindowsTerminal(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%localappdata%\\Microsoft\\WindowsApps\\wt.exe" });
			}
		}

		public class CommandPrompt : ShellTool
		{
			public override string Type => CommandPromptType;

			public override string DisplayName => "Console";

			public override string ApplicationPath { get; }

			public override string Arguments => null;

			public CommandPrompt(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%windir%\\System32\\cmd.exe" });
			}
		}

		public class PowerShell : ShellTool
		{
			public override string Type => PowerShellType;

			public override string DisplayName => "PowerShell";

			public override string ApplicationPath { get; }

			public override string Arguments => null;

			public PowerShell(string applicationPath)
			{
				ApplicationPath = applicationPath;
			}

			public static string TryFindInstance()
			{
				return FindExistingInstance(new string[1] { "%windir%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" });
			}
		}

		public static readonly string DefaultType = "Default";

		public static readonly string CustomType = "Custom";

		public static readonly string WindowsTerminalType = "WindowsTerminal";

		public static readonly string CommandPromptType = "CommandPrompt";

		public static readonly string PowerShellType = "PowerShell";

		public abstract string Type { get; }

		public abstract string DisplayName { get; }

		public abstract string ApplicationPath { get; }

		public abstract string Arguments { get; }

		protected static string FindExistingInstance(string[] possiblePaths)
		{
			foreach (string text in possiblePaths)
			{
				try
				{
					string text2 = Environment.ExpandEnvironmentVariables(text);
					if (File.Exists(text2))
					{
						return text2;
					}
				}
				catch (Exception ex)
				{
					Log.Error("Failed to check if '" + text + "' exists", ex);
				}
			}
			return null;
		}
	}
}
