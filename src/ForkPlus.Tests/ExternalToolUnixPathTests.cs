// 回归测试（2026-09-07，全仓 .exe/Win32 硬编码专项审计——外部工具路径部分）：
// 根因：MergeTool/DiffTool/FileEditor 三组预置定义全部只有 Windows 安装路径（%env%\...exe）。
// - Unix 上 ExpandEnvironmentVariables 不认 %xx%，File.Exists 恒 false——Linux 上一个预置
//   工具都探测不到，首选项里只剩手填 Custom；
// - 通配符切分只认 "*\"（Windows 目录分隔符），"/opt/JetBrains/*/bin/goland.sh" 切不开，
//   整串当字面路径恒 false；
// - "~" 前缀（Toolbox 的 ~/.local/bin 命令行脚本）无人展开；
// - Unix 上 File.Exists 不校验可执行位——探测到"存在的文件"而非"能启动的工具"。
// 修复：三组数组追加 Unix 标准安装位；FindExistingInstance 支持 "*/" 切分、~ 展开、
// 可执行位校验（Windows 保持存在性判定，原版语义不变）。
//
// 测试策略（ShellToolUnixResolutionTests 同款约定，不污染进程环境变量）：
//  - 候选注入用 internal FindExistingInstance(string[]) 直传临时目录模式；
//  - "~" 用真实 UserProfile 下唯一临时子目录（不改 HOME，避免与并行测试竞态）；
//  - Windows 跳过 Unix 特有用例；"*\" 向后兼容分支在 Unix 上用目录名含反斜杠的
//    真实目录验证（Linux 反斜杠是合法文件名字符）。
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ExternalToolUnixPathTests
	{
		private static string MakeTempRoot()
		{
			return Path.Combine(Path.GetTempPath(), "fp_exttool_" + Guid.NewGuid().ToString("N"));
		}

		/// <summary>造一个带可执行位的假工具文件（Windows 上无该语义，仅创建普通文件）。</summary>
		private static string MakeExecutable(string dir, string name)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, name);
			File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
			if (!OperatingSystem.IsWindows())
			{
				File.SetUnixFileMode(path,
					System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite | System.IO.UnixFileMode.UserExecute);
			}
			return path;
		}

		/// <summary>造一个不可执行的普通文件（Unix 上不应被当作工具命中）。</summary>
		private static string MakeNonExecutable(string dir, string name)
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, name);
			File.WriteAllText(path, "not a tool");
			File.SetUnixFileMode(path, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite);
			return path;
		}

		[Fact]
		public void UnixAbsoluteCandidate_ResolvesExecutableFile()
		{
			string root = MakeTempRoot();
			try
			{
				string bin = Path.Combine(root, "bin");
				string tool = MakeExecutable(bin, "faketool");

				// 前两个候选不存在，第三个命中——数组顺序语义（Windows 路径在前、Unix 兜底在后）
				string resolved = ExternalToolManager.FindExistingInstance(new string[3]
				{
					"%ProgramW6432%\\Fake\\tool.exe",
					Path.Combine(root, "nonexistent", "tool"),
					tool
				});

				Assert.NotNull(resolved);
				Assert.Equal(tool, resolved);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void UnixCandidate_RequiresExecutableBit()
		{
			if (OperatingSystem.IsWindows())
			{
				return; // Windows 无可执行位语义，保持原版存在性判定
			}
			string root = MakeTempRoot();
			try
			{
				string plain = MakeNonExecutable(Path.Combine(root, "bin"), "faketool");

				// 存在但不可执行 → 不是"能启动的工具"（Process.Start execve 会 EACCES），不命中
				Assert.Null(ExternalToolManager.FindExistingInstance(new string[1] { plain }));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void UnixWildcard_ResolvesNewestVersionDirectory()
		{
			string root = MakeTempRoot();
			try
			{
				// "/opt/JetBrains/*/bin/goland.sh" 布局：多版本目录，取字典序最大（原版行为）
				string oldV = MakeExecutable(Path.Combine(root, "opt", "JetBrains", "GoLand-2024.1", "bin"), "goland.sh");
				string newV = MakeExecutable(Path.Combine(root, "opt", "JetBrains", "GoLand-2024.2", "bin"), "goland.sh");

				string resolved = ExternalToolManager.FindExistingInstance(new string[1]
				{
					Path.Combine(root, "opt", "JetBrains", "*", "bin", "goland.sh")
				});

				Assert.NotNull(resolved);
				Assert.Equal(newV, resolved);
				Assert.NotEqual(oldV, resolved);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void WindowsBackslashWildcard_StillSupported()
		{
			// 向后兼容：原版 "*\" 切分语义不能被 "*/" 改造破坏。
			// Unix 上反斜杠是合法文件名字符，用目录名字面含 "\" 的真实目录验证该分支仍工作：
			// 目录布局 root/a\b\v1\tool.exe（"a\b" 是单个目录名），模式 root/a\b/*\tool.exe。
			string root = MakeTempRoot();
			try
			{
				string dir = Path.Combine(root, "a\\b", "v1");
				string tool = MakeExecutable(dir, "tool.exe");

				// Path.Combine 拼接不改写字符：Linux 得 "root/a\b/*\tool.exe"，Windows 得 "root\a\b\*\tool.exe"
				string pattern = Path.Combine(root, "a\\b", "*\\tool.exe");
				string resolved = ExternalToolManager.FindExistingInstance(new string[1] { pattern });

				Assert.NotNull(resolved);
				Assert.Equal(tool, resolved);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void TildePrefix_ExpandsToUserProfileThroughResolution()
		{
			// "~/..." 是 FileEditorToolDefinitions 里 Toolbox 命令行脚本的标准前缀。
			// 不改 HOME 环境变量（与并行测试竞态），在真实 UserProfile 下建唯一临时目录。
			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string unique = Path.Combine(home, "fp-unittest-exttool-" + Guid.NewGuid().ToString("N"));
			try
			{
				string tool = MakeExecutable(unique, "zed");

				string resolved = ExternalToolManager.FindExistingInstance(new string[2]
				{
					"%localappdata%\\Programs\\Zed\\Zed.exe",
					"~/" + Path.GetFileName(unique) + "/zed"
				});

				Assert.NotNull(resolved);
				Assert.Equal(tool, resolved);
			}
			finally
			{
				if (Directory.Exists(unique))
				{
					Directory.Delete(unique, recursive: true);
				}
			}
		}

		[Fact]
		public void PredefinedDefinitions_ContainUnixCandidatesForCrossPlatformTools()
		{
			// 守卫"Unix 路径不被后续改动悄悄删掉"：跨平台存在的工具必须有 Unix 候选
			// （以 "/" 开头的绝对路径或 "~/" 开头的主目录相对路径）。
			// Windows-only 工具（AraxisMerge/VisualStudio/WinMerge）不在断言范围。
			Assert.Contains(ExternalToolManager.MergeToolDefinitions, d => d.Type == ToolType.VSCode && d.Paths.Any(p => p.StartsWith("/", StringComparison.Ordinal)));
			Assert.Contains(ExternalToolManager.MergeToolDefinitions, d => d.Type == ToolType.KDiff3 && d.Paths.Any(p => p.StartsWith("/", StringComparison.Ordinal)));
			Assert.Contains(ExternalToolManager.DiffToolDefinitions, d => d.Type == ToolType.BeyondCompare && d.Paths.Any(p => p.StartsWith("/", StringComparison.Ordinal)));
			Assert.Contains(ExternalToolManager.FileEditorToolDefinitions, d => d.Type == ToolType.SublimeText && d.Paths.Any(p => p.StartsWith("/", StringComparison.Ordinal)));
			Assert.Contains(ExternalToolManager.FileEditorToolDefinitions, d => d.Type == ToolType.GoLand && d.Paths.Any(p => p.StartsWith("/", StringComparison.Ordinal)));
			Assert.Contains(ExternalToolManager.FileEditorToolDefinitions, d => d.Type == ToolType.Zed && d.Paths.Any(p => p.StartsWith("~/", StringComparison.Ordinal) || p.StartsWith("/", StringComparison.Ordinal)));

			// UnityYAMLMerge 的 Unix 候选用 "~/" + "*/" 通配符（Hub 布局），同时守卫通配符写法
			Assert.Contains(ExternalToolManager.MergeToolDefinitions, d => d.Type == ToolType.Unity3d && d.Paths.Any(p => p.StartsWith("~/", StringComparison.Ordinal) && p.Contains("*/")));
		}

		[Fact]
		public void RevealAvailableFileEditorTools_NeverThrowsAcrossPlatforms()
		{
			// 全链路冒烟：三组预置数组 + ExpandToolPath/FindExistingInstance 在当前平台
			// 上遍历不抛异常（Windows 候选在 Unix 上安全跳过，反之亦然）。
			ForkPlus.ExternalTool[] tools = ExternalToolManager.RevealAvailableFileEditorTools(includeNonExistent: true);
			Assert.Equal(ExternalToolManager.FileEditorToolDefinitions.Length, tools.Length);
		}
	}
}
