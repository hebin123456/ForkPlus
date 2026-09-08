// 回归测试（2026-09-07，"linux版本的控制台点了没反应"）：
// 根因链：原版 ShellTool.Default.ApplicationPath 返回 <git目录>/bash 并直接 Process.Start——
// Windows 上 GUI 进程启动控制台子系统的 bash.exe 会自动分配新控制台窗口，一切正常；
// Unix 上裸 bash 无 TTY（stdin 非 tty），读到 EOF 立即退出，用户看到"点了没反应"；
// 且 Linux 上 bash 与 git 常不同目录（自编译 git 在 /usr/local/bin，bash 在 /usr/bin），
// File.Exists 也会失败。修复：Unix 解析终端模拟器承载 shell（继承 WorkingDirectory）。
// 附带修复：App.ShellPath/BashPath 原硬编码 "sh.exe"/"bash.exe"（Linux 恒不存在，钩子/
// 自定义命令/discard 全挂），改为平台感知 + PATH 回退。
//
// 测试策略（TokeiResolutionTests 同款约定）：
//  - 解析逻辑用注入 PATH 参数（internal 重载），不改进程环境变量——xunit 跨 collection
//    并行，污染全局 PATH 有竞态风险；
//  - E2E 用例把"只含假终端的目录"前置到进程 PATH（前置只遮蔽同名文件，git/bash 等
//    仍从原 PATH 解析，并行测试 spawn git 不受影响），假终端以脚本回写 $PWD 标记文件，
//    断言命令在仓库目录把它拉起（WorkingDirectory 语义）。
using System;
using System.IO;
using System.Threading;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.Settings;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ShellToolUnixResolutionTests
	{
		private static string MakeTempRoot()
		{
			return Path.Combine(Path.GetTempPath(), "fp_terminal_" + Guid.NewGuid().ToString("N"));
		}

		/// <summary>在 dir 下造一个假终端（脚本），返回完整路径。脚本把 $PWD 写进 markerPath。</summary>
		private static string MakeFakeTerminal(string dir, string name, string markerPath)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, name);
			File.WriteAllText(path, "#!/bin/sh\nprintf '%s' \"$PWD\" > '" + markerPath.Replace("'", "'\\''") + "'\n");
			File.SetUnixFileMode(path,
				System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
			return path;
		}

		[Fact]
		public void UnixTerminalEmulator_ResolvesFromInjectedPath()
		{
			if (OperatingSystem.IsWindows())
			{
				return; // Windows 走 git 目录 bash.exe 原版行为，本用例不适用
			}
			string root = MakeTempRoot();
			try
			{
				string dir = Path.Combine(root, "bin");
				string fake = MakeFakeTerminal(dir, "gnome-terminal", Path.Combine(root, "marker"));

				string resolved = ShellTool.FindUnixTerminalEmulator(dir);

				Assert.NotNull(resolved);
				Assert.Equal(Path.GetFullPath(fake), Path.GetFullPath(resolved));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void UnixTerminalEmulator_CandidateOrderBeatsPathOrder()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = MakeTempRoot();
			try
			{
				// dirA 放低优先级候选 xterm；dirB 放高优先级候选 gnome-terminal。
				// 无论 PATH 里谁在前，xdg-terminal-exec > gnome-terminal > xterm 的
				// 候选序必须先扫完一个名字的所有 PATH 目录再换下一个（用户预期：装了
				// gnome 就用 gnome，PATH 里碰巧先出现 xterm 不改变选择）。
				string dirA = Path.Combine(root, "a");
				string dirB = Path.Combine(root, "b");
				string xterm = MakeFakeTerminal(dirA, "xterm", Path.Combine(root, "marker"));
				string gnome = MakeFakeTerminal(dirB, "gnome-terminal", Path.Combine(root, "marker"));

				string resolvedAFirst = ShellTool.FindUnixTerminalEmulator(dirA + Path.PathSeparator + dirB);
				Assert.Equal(Path.GetFullPath(gnome), Path.GetFullPath(resolvedAFirst));

				string resolvedBFirst = ShellTool.FindUnixTerminalEmulator(dirB + Path.PathSeparator + dirA);
				Assert.Equal(Path.GetFullPath(gnome), Path.GetFullPath(resolvedBFirst));

				// 只有 xterm 时才轮到它（候选列表兜底）
				string resolvedXtermOnly = ShellTool.FindUnixTerminalEmulator(dirA);
				Assert.Equal(Path.GetFullPath(xterm), Path.GetFullPath(resolvedXtermOnly));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void UnixTerminalEmulator_XdgTerminalExecHasHighestPriority()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = MakeTempRoot();
			try
			{
				string dirA = Path.Combine(root, "a");
				string dirB = Path.Combine(root, "b");
				string xdg = MakeFakeTerminal(dirA, "xdg-terminal-exec", Path.Combine(root, "marker"));
				MakeFakeTerminal(dirB, "gnome-terminal", Path.Combine(root, "marker"));

				// freedesktop 规范入口必须赢，即使 gnome-terminal 的目录在 PATH 更靠前
				string resolved = ShellTool.FindUnixTerminalEmulator(dirB + Path.PathSeparator + dirA);
				Assert.Equal(Path.GetFullPath(xdg), Path.GetFullPath(resolved));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void UnixTerminalEmulator_NothingFound_ReturnsNull()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = MakeTempRoot();
			try
			{
				Directory.CreateDirectory(root);
				// 空目录/不存在目录/空串 PATH 都必须安全返回 null（不抛异常）
				Assert.Null(ShellTool.FindUnixTerminalEmulator(root));
				Assert.Null(ShellTool.FindUnixTerminalEmulator(Path.Combine(root, "nonexistent")));
				Assert.Null(ShellTool.FindUnixTerminalEmulator(""));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		/// <summary>App.BashPath/ShellPath 回归：Unix 上不再硬编码 .exe 后缀。
		/// 沙盒布局（git 在 /usr/local/bin，bash 在 /usr/bin）正是"git 与 bash 不同目录"
		/// 的用户现场，同目录找不到必须回退 PATH。只读进程 PATH（不改环境），并行安全。</summary>
		[Fact]
		public void App_ShellAndBashPaths_ResolveExistingFile_OnUnix()
		{
			if (OperatingSystem.IsWindows())
			{
				return; // Windows 保持原版 git 同目录 name.exe 语义，本用例不适用
			}
			Assert.True(File.Exists(App.BashPath), "App.BashPath 应解析到存在的 bash（同目录或 PATH 回退），实际: " + App.BashPath);
			Assert.True(File.Exists(App.ShellPath), "App.ShellPath 应解析到存在的 sh（同目录或 PATH 回退），实际: " + App.ShellPath);
			Assert.DoesNotContain(".exe", App.BashPath);
			Assert.DoesNotContain(".exe", App.ShellPath);
		}

		/// <summary>E2E：控制台按钮命令在 Unix 上应拉起终端模拟器于仓库目录。
		/// 假终端目录前置到进程 PATH（只遮蔽同名文件，git/bash 不受影响），脚本回写
		/// $PWD 标记，验证 WorkingDirectory = 仓库路径的完整链路。原 bug 现场等价：
		/// 修复前 Default 解析到的是无 TTY 的裸 bash——本用例在 Linux 沙盒里红掉。</summary>
		[Fact]
		public void OpenRepositoryInShellTool_Unix_LaunchesTerminalAtRepositoryDirectory_E2E()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			string root = MakeTempRoot();
			string oldPath = Environment.GetEnvironmentVariable("PATH");
			ShellTool originalShellTool = ForkPlusSettings.Default.ShellTool;
			try
			{
				// 1) 仓库工作目录
				string repoDir = Path.Combine(root, "repo");
				Directory.CreateDirectory(repoDir);

				// 2) 假终端目录：放最高优先级的 xdg-terminal-exec（防止宿主机装了真
				//    gnome-terminal/konsole 抢先解析到真终端，测试就不可移植了）
				string fakeBin = Path.Combine(root, "fakebin");
				string marker = Path.Combine(root, "marker");
				MakeFakeTerminal(fakeBin, "xdg-terminal-exec", marker);

				// 3) 前置到进程 PATH（原 PATH 的超集语义：本目录只有 xdg-terminal-exec，
				//    并行测试 spawn 的 git/bash 等仍从原 PATH 解析，不受影响）
				Environment.SetEnvironmentVariable("PATH", fakeBin + Path.PathSeparator + oldPath);

				// 4) 走完整生产链路：设置 ShellTool=Default → Execute → 解析终端 → Process.Start
				ForkPlusSettings.Default.ShellTool = new ShellTool.Default();
				new OpenRepositoryInShellToolCommand().Execute(repoDir);

				// 5) 等标记文件出现（脚本进程启动有毫秒级延迟）
				bool markerAppeared = false;
				for (int i = 0; i < 100 && !markerAppeared; i++)
				{
					Thread.Sleep(50);
					markerAppeared = File.Exists(marker);
				}
				Assert.True(markerAppeared, "终端模拟器应被拉起并回写标记文件（修复前：裸 bash 无 TTY 静默退出，无任何反应）");

				string captured = File.ReadAllText(marker).Trim();
				Assert.Equal(Path.GetFullPath(repoDir), Path.GetFullPath(captured));
			}
			finally
			{
				Environment.SetEnvironmentVariable("PATH", oldPath);
				ForkPlusSettings.Default.ShellTool = originalShellTool;
				try
				{
					Directory.Delete(root, recursive: true);
				}
				catch
				{
					// 假终端脚本进程可能仍在写标记文件，删除失败不阻碍结果判定
				}
			}
		}
	}
}
