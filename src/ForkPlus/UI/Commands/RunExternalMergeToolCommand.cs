using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class RunExternalMergeToolCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "External Merge";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, string filePath, ExternalTool mergeTool)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			TempFileManager tempFileManager = repositoryUserControl.TempFileManager;
			if (tempFileManager != null)
			{
				Log.Info("Resolve conflict in '" + filePath + "' using merge tool");
				if (CheckoutConflictVersions(gitModule, tempFileManager, filePath, out var basePath, out var localPath, out var remotePath))
				{
					string merged = gitModule.MakePath(filePath);
					Execute(mergeTool, localPath, remotePath, basePath, merged);
				}
			}
		}

		public void Execute(ExternalTool mergeTool, string local, string remote, string @base, string merged)
		{
			string text = Environment.ExpandEnvironmentVariables(mergeTool.Path);
			if (!File.Exists(text))
			{
				Log.Error("Cannot find external merge tool at '" + text + "'");
				new ErrorWindow("Cannot find external merge tool at '" + text + "'").ShowDialog();
				return;
			}
			string text2 = string.Join(" ", mergeTool.Arguments.Map((string x) => x.Replace("$LOCAL", local).Replace("$REMOTE", remote).Replace("$BASE", @base)
				.Replace("$MERGED", merged)));
			Process process = new Process();
			process.StartInfo = new ProcessStartInfo
			{
				FileName = text,
				Arguments = text2
			};
			Log.Info("Running '" + text + " " + text2 + "'");
			try
			{
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to run external merge tool '{externalMergeToolPath} {argumentsString}'", ex);
				new ErrorWindow($"Cannot run '{text}'.\n{ex}").ShowDialog();
			}
		}

		private static bool CheckoutConflictVersions(GitModule gitModule, TempFileManager tempFileManager, string filepath, out string basePath, out string localPath, out string remotePath)
		{
			GitCommandResult<string> gitCommandResult = new CheckoutUnmergedFileGitCommand().Execute(gitModule, tempFileManager, filepath, UnmergedFileVersionType.Local);
			GitCommandResult<string> gitCommandResult2 = new CheckoutUnmergedFileGitCommand().Execute(gitModule, tempFileManager, filepath, UnmergedFileVersionType.Remote);
			if (!gitCommandResult.Succeeded || !gitCommandResult2.Succeeded)
			{
				basePath = null;
				localPath = null;
				remotePath = null;
				return false;
			}
			GitCommandResult<string> gitCommandResult3 = new CheckoutUnmergedFileGitCommand().Execute(gitModule, tempFileManager, filepath, UnmergedFileVersionType.Base);
			basePath = gitCommandResult3.Result;
			localPath = gitCommandResult.Result;
			remotePath = gitCommandResult2.Result;
			return true;
		}
	}
}
