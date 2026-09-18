using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 用配置的外部比对工具（Beyond Compare、WinMerge 等）比较两次提交的完整文件树：
	/// 分别导出两次提交的整棵文件树到临时目录，再把两个目录作为 $REMOTE/$LOCAL
	/// 传给比对工具（绝大多数目录级比对工具直接支持两个目录参数）。
	/// </summary>
	public class RunExternalTreeDiffToolCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Compare File Trees in External Tool";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Sha remoteSha, Sha localSha, ExternalTool diffTool)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			TempFileManager tempFileManager = repositoryUserControl.TempFileManager;
			if (tempFileManager == null)
			{
				return;
			}
			string externalDiffToolPath = Environment.ExpandEnvironmentVariables(diffTool.Path);
			if (!File.Exists(externalDiffToolPath))
			{
				Log.Error("Cannot find external diff tool at '" + externalDiffToolPath + "'");
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find external diff tool at '{0}'", externalDiffToolPath)).ShowDialog();
				return;
			}
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("External diff"), delegate(JobMonitor monitor)
			{
				string remoteDir = tempFileManager.GetTempDirectoryPath("tree_" + remoteSha.ToAbbreviatedString() + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				string localDir = tempFileManager.GetTempDirectoryPath("tree_" + localSha.ToAbbreviatedString() + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				GitCommandResult<string> remoteResult = new ExportRevisionTreeGitCommand().Execute(gitModule, remoteSha, remoteDir, monitor);
				if (!remoteResult.Succeeded)
				{
					ShowError(repositoryUserControl, monitor, remoteResult.Error);
					return;
				}
				if (monitor.IsCanceled)
				{
					return;
				}
				GitCommandResult<string> localResult = new ExportRevisionTreeGitCommand().Execute(gitModule, localSha, localDir, monitor);
				if (!localResult.Succeeded)
				{
					ShowError(repositoryUserControl, monitor, localResult.Error);
					return;
				}
				string arguments = string.Join(" ", diffTool.Arguments.Map((string x) => x.Replace("$REMOTE", remoteDir).Replace("$LOCAL", localDir)));
				Process process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = externalDiffToolPath,
						Arguments = arguments
					}
				};
				Log.Info("Running '" + externalDiffToolPath + " " + arguments + "'");
				monitor.AppendOutputLine("$ " + externalDiffToolPath + " " + arguments);
				try
				{
					process.Start();
				}
				catch (Exception ex)
				{
					Log.Error("Failed to start external diff tool '" + externalDiffToolPath + " " + arguments + "'", ex);
					repositoryUserControl.Dispatcher.Invoke(delegate
					{
						new ErrorWindow($"Cannot run '{externalDiffToolPath}'.\n{ex}").ShowDialog();
					});
				}
			});
		}

		private static void ShowError(RepositoryUserControl repositoryUserControl, JobMonitor monitor, GitCommandError error)
		{
			Log.Error(error.FriendlyDescription);
			repositoryUserControl.Dispatcher.Invoke(delegate
			{
				if (!monitor.IsCanceled)
				{
					new ErrorWindow(repositoryUserControl, error).ShowDialog();
				}
			});
		}
	}
}
