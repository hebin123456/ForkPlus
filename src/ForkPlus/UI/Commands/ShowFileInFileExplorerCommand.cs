using System;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class ShowFileInFileExplorerCommand : IUICommand, IForkPlusCommand
	{
		// Migration note：平台化文案（macOS 用户期望 Finder 措辞；Linux 通用 File Manager）。
		public string Title => OperatingSystem.IsMacOS() ? "Show in Finder" : (OperatingSystem.IsWindows() ? "Show in File Explorer" : "Show in File Manager");

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] GitModule gitModule, [Null] string filePath)
		{
			if (gitModule != null && filePath != null)
			{
				// Migration note：git 内部路径恒为正斜杠；原 .Replace("/", "\\") 是 Windows 硬编码，
				// Unix 上会把路径改成反斜杠分隔导致 File.Exists 恒 false。Windows 分隔符
				// 交给 FileHelper 内部处理（FileHelper.BuildWindowsExplorerArguments，2026-09-03
				// 落实——此前缺失导致 explorer /select 解析不了混合分隔符路径，"在文件资源
				// 管理器中显示"一直打开"文档"库而非目标位置），这里只做拼接。
				FileHelper.OpenInWindowsExplorer(Path.Combine(gitModule.Path, filePath));
			}
		}
	}
}
