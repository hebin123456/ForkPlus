using System;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class SaveFileCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save as...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, [Null] ChangedFile changedFile, [Null] string sha)
		{
			if (sha == null || changedFile == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string initialDirectory = ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			string directoryName = Path.GetDirectoryName(gitModule.MakePath(changedFile.Path));
			if (Directory.Exists(directoryName))
			{
				initialDirectory = directoryName;
			}
			if (OpenDialog.SelectFileSaveLocation(MainWindow.Instance, "Select location", initialDirectory, Path.GetFileName(changedFile.Path), out var resultFilePath))
			{
				if (changedFile.ChangeType == ChangeType.Deleted)
				{
					sha += "^";
				}
				GitCommandResult<DiffContent> binaryContent = new GetRevisionFileChangesGitCommand().GetBinaryContent(gitModule, changedFile, sha, null);
				if (!binaryContent.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, binaryContent.Error).ShowDialog();
				}
				else if (binaryContent.Result is BinaryDiffContent binaryDiffContent)
				{
					SaveFile(resultFilePath, binaryDiffContent.DstData);
				}
				else if (binaryContent.Result is LfsDiffContent lfsDiffContent)
				{
					DownloadAndSaveLfsBinaryFile(repositoryUserControl, lfsDiffContent.Dst, resultFilePath);
				}
			}
		}

		private void DownloadAndSaveLfsBinaryFile(RepositoryUserControl repositoryUserControl, LfsPointer filePointer, string filePath)
		{
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Downloading {0}", Path.GetFileName(filePath)), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					GitCommandResult<MemoryStream> lfsFileDataResponse = new SmudgeLfsFileCommand().Execute(repositoryUserControl.GitModule, filePointer, monitor);
					repositoryUserControl.Dispatcher.Invoke(delegate
					{
						if (!lfsFileDataResponse.Succeeded)
						{
							if (!monitor.IsCanceled)
							{
								new ErrorWindow(repositoryUserControl, lfsFileDataResponse.Error).ShowDialog();
							}
						}
						else
						{
							SaveFile(filePath, lfsFileDataResponse.Result);
						}
					});
				}
			}, JobFlags.SaveToLog);
		}

		private void SaveFile(string filePath, [Null] MemoryStream data)
		{
			byte[] array = data?.ToArray();
			if (array == null)
			{
				return;
			}
			try
			{
				File.WriteAllBytes(filePath, array);
			}
			catch (Exception ex)
			{
				Log.Error($"Cannot save file: {ex}");
				new ErrorWindow(ex.ToString()).ShowDialog();
			}
		}
	}
}
