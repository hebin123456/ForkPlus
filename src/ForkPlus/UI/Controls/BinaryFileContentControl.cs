using System;
using System.IO;
using System.Windows.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.BinaryDiff;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class BinaryFileContentControl : Grid
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		[Null]
		private GitModule _gitModule;

		[Null]
		private string _fileName;

		[Null]
		private LfsPointer _lfsPointer;

		[Null]
		private Job _activeSmudgeJob;

		private BinaryContentUserControl _binaryContentUserControl = new BinaryContentUserControl();

		public BinaryFileContentControl()
		{
			base.Children.Add(_binaryContentUserControl);
			BinaryContentUserControl binaryContentUserControl = _binaryContentUserControl;
			binaryContentUserControl.ShowLfsImageButtonClick = (EventHandler<EventArgs>)Delegate.Combine(binaryContentUserControl.ShowLfsImageButtonClick, (EventHandler<EventArgs>)delegate
			{
				GitModule gitModule2 = _gitModule;
				if (gitModule2 != null)
				{
					LfsPointer lfsPointer2 = _lfsPointer;
					if (lfsPointer2 != null)
					{
						_binaryContentUserControl.SetProgress(0.0);
						_activeSmudgeJob?.Monitor.Cancel();
						_activeSmudgeJob = StartSmudgeLfsImageJob(lfsPointer2, gitModule2, delegate(JobMonitor monitor)
						{
							_binaryContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
						}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
						{
							_activeSmudgeJob = null;
							_binaryContentUserControl.SetProgress(null);
							if (!imageDataResponse.Succeeded)
							{
								new ErrorWindow(null, imageDataResponse.Error).ShowDialog();
							}
							else
							{
								MemoryStream result = imageDataResponse.Result;
								if (Path.GetExtension(_fileName) == ".tga" && result != null)
								{
									GitCommandResult<MemoryStream> gitCommandResult = BinaryDiffUserControl.DecodeImageData(result.ToArray());
									if (gitCommandResult.Succeeded)
									{
										result = gitCommandResult.Result;
									}
									else
									{
										Log.Error(gitCommandResult.Error.FriendlyDescription);
									}
								}
								_binaryContentUserControl.SetLfsImageData(result);
							}
						});
					}
				}
			});
			BinaryContentUserControl binaryContentUserControl2 = _binaryContentUserControl;
			binaryContentUserControl2.CancelLfsButtonClick = (EventHandler<EventArgs>)Delegate.Combine(binaryContentUserControl2.CancelLfsButtonClick, (EventHandler<EventArgs>)delegate
			{
				_activeSmudgeJob?.Monitor.Cancel();
			});
			BinaryContentUserControl binaryContentUserControl3 = _binaryContentUserControl;
			binaryContentUserControl3.SaveAsMenuItemClick = (EventHandler<EventArgs>)Delegate.Combine(binaryContentUserControl3.SaveAsMenuItemClick, (EventHandler<EventArgs>)delegate
			{
				GitModule gitModule = _gitModule;
				if (gitModule != null)
				{
					LfsPointer lfsPointer = _lfsPointer;
					if (lfsPointer != null)
					{
						string fileName = _fileName;
						if (fileName != null && OpenDialog.SelectFileSaveLocation(null, "Select location", RepositoryManager.Instance.DefaultSourceDir(), fileName, out var directory))
						{
							_binaryContentUserControl.SetProgress(0.0);
							_activeSmudgeJob?.Monitor.Cancel();
							_activeSmudgeJob = StartSmudgeLfsImageJob(lfsPointer, gitModule, delegate(JobMonitor monitor)
							{
								_binaryContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
							}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
							{
								_activeSmudgeJob = null;
								_binaryContentUserControl.SetProgress(null);
								if (!imageDataResponse.Succeeded)
								{
									new ErrorWindow(null, imageDataResponse.Error).ShowDialog();
									return;
								}
								try
								{
									File.WriteAllBytes(directory, imageDataResponse.Result.ToArray());
								}
								catch (Exception ex)
								{
									Log.Error($"Cannot save LFS binary file: {ex}");
									new ErrorWindow(ex.ToString()).ShowDialog();
								}
							});
						}
					}
				}
			});
		}

		public void SetContent(GitModule gitModule, BinaryContent binaryContent)
		{
			_gitModule = gitModule;
			if (binaryContent is LfsContent lfsContent)
			{
				_fileName = Path.GetFileName(binaryContent.Path);
				_lfsPointer = lfsContent.LfsPointer;
			}
			_binaryContentUserControl.SetContent(binaryContent);
			if (!(binaryContent is LfsContent { BinaryFileType: BinaryFileType.LfsImage } lfsContent2))
			{
				return;
			}
			GitCommandResult<MemoryStream> gitCommandResult = new GitLfsGetCachedFileGitCommand().Execute(gitModule.CommonGitDir, lfsContent2.LfsPointer.Sha256String);
			if (!gitCommandResult.Succeeded)
			{
				return;
			}
			MemoryStream result = gitCommandResult.Result;
			if (Path.GetExtension(_fileName) == ".tga" && result != null)
			{
				GitCommandResult<MemoryStream> gitCommandResult2 = BinaryDiffUserControl.DecodeImageData(result.ToArray());
				if (gitCommandResult2.Succeeded)
				{
					result = gitCommandResult2.Result;
				}
				else
				{
					Log.Error(gitCommandResult2.Error.FriendlyDescription);
				}
			}
			_binaryContentUserControl.SetLfsImageData(result);
		}

		private Job StartSmudgeLfsImageJob(LfsPointer lfsPointer, GitModule gitModule, Action<JobMonitor> progressCallback, Action<GitCommandResult<MemoryStream>> completedCallback)
		{
			return _jobQueue.Add(PreferencesLocalization.Current("Smudge LFS image"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					monitor.SetProgressAction(delegate
					{
						base.Dispatcher.Async(delegate
						{
							progressCallback(monitor);
						});
					});
					GitCommandResult<MemoryStream> imageDataResponse = new SmudgeLfsFileCommand().Execute(gitModule, lfsPointer, monitor);
					monitor.SetProgressAction(null);
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							if (!imageDataResponse.Succeeded)
							{
								completedCallback(GitCommandResult<MemoryStream>.Failure(imageDataResponse.Error));
							}
							else
							{
								completedCallback(GitCommandResult<MemoryStream>.Success(imageDataResponse.Result));
							}
						}
					});
				}
			});
		}
	}
}
