using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryInShellToolCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open In " + ForkPlusSettings.Default.ShellTool.DisplayName;

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, global::Avalonia.Input.KeyModifiers.Alt | global::Avalonia.Input.KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public static CommandDescriptor[] PublicCommands => new CommandDescriptor[1]
		{
			new CommandDescriptor("Open In " + ForkPlusSettings.Default.ShellTool.DisplayName, new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					MainWindow.Commands.OpenRepositoryInShellTool.Execute(gitModule);
				}
			})
		};

		public void Execute(GitModule gitModule)
		{
			Execute(gitModule.Path);
		}

		public void Execute(string path)
		{
			ShellTool shellTool = ForkPlusSettings.Default.ShellTool;
			string applicationPath = shellTool.ApplicationPath;
			// Unix 上 ApplicationPath 可能为 null（找不到任何终端模拟器）；
			// 空路径拼进错误提示会显示 "Cannot find shellToolPath at ''"，无诊断价值，
			// 回退为候选终端列表，用户能看出该装哪个。
			string notFoundDisplay = applicationPath ?? string.Join("/", ShellTool.UnixTerminalEmulatorCandidates);
			if (string.IsNullOrEmpty(applicationPath) || !File.Exists(applicationPath))
			{
				Log.Error("Cannot find shellToolPath at '" + notFoundDisplay + "'");
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find shellToolPath at '{0}'", notFoundDisplay)).ShowDialog();
				return;
			}
			Process process = new Process
			{
				StartInfo = new ProcessStartInfo(applicationPath)
				{
					WorkingDirectory = path,
					Arguments = shellTool.Arguments
				}
			};
			try
			{
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Cannot start '" + notFoundDisplay + "'", ex);
				new ErrorWindow(ex.Message).ShowDialog();
			}
		}
	}
}
