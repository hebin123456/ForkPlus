using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInFileExplorerCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Open In File Explorer", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(gitModule);
				}
			})
		};

		public string Title => "Open In File Explorer";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, KeyModifiers.Alt | KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] string repositoryPath)
		{
			// NOTE(4.5): WPF Keyboard.IsKeyDown(Key.RightAlt) 在 Avalonia 无等价物（KeyModifiers.Alt
			// 不区分左右 Alt）。本命令快捷键为 Alt+Ctrl+O，若用 KeyboardHelper.IsAltDown 会在快捷键
			// 触发时误返回（Alt 必然按下），故此处置 false 保留主路径行为。
			if (false || string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
			{
				return;
			}
			Log.Info("Open in file explorer '" + repositoryPath + "'");
			try
			{
				// .NET 10 起 UseShellExecute 默认从 true 改为 false，传文件夹路径作 FileName
				// 会因"非可执行文件"抛 Win32Exception，必须显式置 true 才能走 Shell 打开文件夹。
				Process process = new Process();
				ProcessStartInfo startInfo = new ProcessStartInfo(repositoryPath) { UseShellExecute = true };
				process.StartInfo = startInfo;
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to open '" + repositoryPath + "' in file explorer", ex);
			}
		}

		public void Execute(GitModule gitModule)
		{
			Execute(gitModule.Path);
		}
	}
}
