// 回归测试（2026-09-03，"'在文件资源管理器中显示'一直打开文档目录"修复产物）：
// 根因：ShowFileInFileExplorerCommand 迁移时删掉了 WPF 原版的 .Replace("/", "\\")，
// 注释声称"Windows 分隔符交给 FileHelper 内部处理"，但 FileHelper 从未实现——
// git 相对路径恒为正斜杠，Path.Combine 后是混合分隔符（C:\repo\src/App.cs），
// .NET 的 File.Exists 接受正斜杠（守卫通过），但 explorer.exe 解析不了 /select
// 里的正斜杠，Windows 会忽略 /select 直接打开"文档"库。
// 修复：FileHelper.BuildWindowsExplorerArguments 在 Windows 分支统一规范化分隔符。
// Windows 分支本身无法在 Linux CI 执行，故抽出纯函数守卫参数构造契约。
using System;
using ForkPlus;
using Xunit;

namespace ForkPlus.Tests
{
	public class FileExplorerRevealTests
	{
		[Fact]
		public void MixedSeparatorFilePath_GetsNormalizedAndSelected()
		{
			// 用户报告场景：git 路径（正斜杠）+ 仓库根（反斜杠）经 Path.Combine 拼接。
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"C:\Users\me\Documents\repos\myrepo\src/App.cs", isFile: true);
			// 正斜杠必须转反斜杠——explorer.exe 解析不了 /select 里的正斜杠，
			// 否则 Windows 忽略 /select 直接打开"文档"库（回归即红）。
			Assert.Equal(@"/select,""C:\Users\me\Documents\repos\myrepo\src\App.cs""", arguments);
		}

		[Fact]
		public void PureBackslashPath_IsUnchanged()
		{
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"C:\repo\src\App.cs", isFile: true);
			Assert.Equal(@"/select,""C:\repo\src\App.cs""", arguments);
		}

		[Fact]
		public void DirectoryPath_GetsNormalizedWithoutSelect()
		{
			// 目录分支：直接打开目录本身，不带 /select（侧栏 submodule/worktree、文件树文件夹）。
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"C:\repo\extern/libfoo", isFile: false);
			Assert.Equal(@"C:\repo\extern\libfoo", arguments);
		}

		[Fact]
		public void SelectArgument_HasNoSpaceAfterComma()
		{
			// explorer /select 语法要求逗号后紧跟路径：新版 Windows 遇到空格会忽略
			// /select 直接打开"文档"库而非选中目标文件（FileHelper 历史注释记载的坑）。
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"C:\repo\src\App.cs", isFile: true);
			Assert.StartsWith("/select,\"", arguments, StringComparison.Ordinal);
			Assert.DoesNotContain("/select, \"", arguments, StringComparison.Ordinal);
		}

		[Fact]
		public void PathWithSpacesAndCjk_IsQuoted()
		{
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"C:\我的 仓库\新 文件.cs", isFile: true);
			Assert.Equal(@"/select,""C:\我的 仓库\新 文件.cs""", arguments);
		}

		[Fact]
		public void DeepMixedPath_AllForwardSlashesConverted()
		{
			// 文件树深层路径：多个正斜杠全部转换（漏一个 explorer 就解析失败）。
			string arguments = FileHelper.BuildWindowsExplorerArguments(
				@"D:\work\demo/sub dir/lib/util.cs", isFile: true);
			Assert.Equal(@"/select,""D:\work\demo\sub dir\lib\util.cs""", arguments);
			// /select 前缀自带一个 '/'，检查引号内的路径部分不再含正斜杠。
			Assert.DoesNotContain("/", arguments.Substring(arguments.IndexOf('"')), StringComparison.Ordinal);
		}
	}
}
