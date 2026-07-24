using System;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class OpenFileInDefaultEditorCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, string filePath)
		{
			if (gitModule == null || filePath == null || !IsEditorAvailable(gitModule, filePath))
			{
				return;
			}
			try
			{
				string text = gitModule.MakePath(filePath);
				if (File.Exists(text))
				{
					ServiceLocator.Process.OpenFileInDefaultApplication(text);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open '" + filePath + "' in default editor", ex);
			}
		}

		public void Execute(GitModule gitModule, string sha, ChangedFile changedFile)
		{
			if (gitModule == null || sha == null || changedFile == null || !IsEditorAvailable(gitModule, changedFile.Path))
			{
				return;
			}
			try
			{
				string text = SaveToTempDestination(gitModule, sha, changedFile);
				if (text != null && File.Exists(text))
				{
					ServiceLocator.Process.OpenFileInDefaultApplication(text);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to save '" + changedFile.Path + "' to temporary location", ex);
			}
		}

		public bool IsEditorAvailable(GitModule gitModule, string filePath)
		{
			if (gitModule == null || filePath == null)
			{
				return false;
			}
			try
			{
				string text = gitModule.MakePath(filePath);
				Log.Info("Looking for an associated editor for '" + text + "'");
				string extension = Path.GetExtension(text);
				if (string.IsNullOrEmpty(extension))
				{
					return false;
				}
				bool available = ServiceLocator.FileAssociation.IsEditorAvailable(extension);
				if (!available)
				{
					Log.Info("Can't find editor for '" + text + "'");
				}
				else
				{
					Log.Info("File can be edited with an associated application");
				}
				return available;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get associated editor for '" + filePath + "'", ex);
				return false;
			}
		}

		// 阶段 3：TempFileManager 改由 IWindowManagerService.GetActiveRepositoryTempFileManager() 提供，
		// 不再直访 Application.Current.ActiveRepositoryUserControl()。
		[Null]
		private string SaveToTempDestination(GitModule gitModule, string sha, ChangedFile changedFile)
		{
			TempFileManager tempFileManager = ServiceLocator.WindowManager.GetActiveRepositoryTempFileManager();
			if (tempFileManager == null)
			{
				return null;
			}
			GitCommandResult<DiffContent> binaryContent = new GetRevisionFileChangesGitCommand().GetBinaryContent(gitModule, changedFile, sha, null);
			if (!binaryContent.Succeeded || !(binaryContent.Result is BinaryDiffContent binaryDiffContent))
			{
				Log.Error(binaryContent.Error.FriendlyDescription);
				return null;
			}
			return tempFileManager.CreateTemporaryFile(changedFile.Path, binaryDiffContent.DstData, sha.Substring(0, 7));
		}
	}
}
