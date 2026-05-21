using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class FileMergeControl : FileDiffControl
	{
		protected override void UpdateView(bool loadLargeDiff = false)
		{
			RepositoryUserControl repositoryUserControl = base.RepositoryUserControl;
			if (base.Content == null || !base.Content.Succeeded)
			{
				ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					h.Collapse();
				});
				return;
			}
			ChangedFile changedFile = base.Content.Result.ChangedFile;
			DiffContent result = base.Content.Result;
			UnmergedDiffContent unmergedDiffContent = result as UnmergedDiffContent;
			if (unmergedDiffContent != null)
			{
				switch (unmergedDiffContent.FileType)
				{
				case UnmergedDiffContent.ContentType.Lfs:
				{
					LfsPointer[] lfsFilePointers = GetFileChangesGitCommand.ParseLfsDiff(unmergedDiffContent.DiffString, merge: true);
					if (lfsFilePointers == null)
					{
						break;
					}
					ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
					{
						c.DiffImageSourceChanged += delegate(object s, bool diffImageExists)
						{
							h.HighlightPixelsToggleButtonEnabled = diffImageExists;
						};
						BinaryFileType binaryFileType = ((!PathHelper.IsImagePath(changedFile.Path)) ? BinaryFileType.LfsBinaryFile : BinaryFileType.LfsImage);
						LfsDiffContent diffContent = new LfsDiffContent(repositoryUserControl.GitModule, changedFile, binaryFileType, lfsFilePointers[0], lfsFilePointers[1]);
						c.UpdateDiff(repositoryUserControl, diffContent, showTitle: false);
						h.Hide();
					});
					break;
				}
				case UnmergedDiffContent.ContentType.Binary:
					if (PathHelper.IsImagePath(changedFile.Path))
					{
						_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading image content for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
						{
							GitCommandResult<BinaryDiffContent> binaryDiffContentResult = FileDiffControl.LoadUnmergedBinaryDiffContent(unmergedDiffContent.DiffString, repositoryUserControl.GitModule, changedFile, monitor);
							base.Dispatcher.Async(delegate
							{
								if (!monitor.IsCanceled)
								{
									_activeRefreshJob = null;
									if (!binaryDiffContentResult.Succeeded)
									{
										ShowErrorView(binaryDiffContentResult.Error);
									}
									else
									{
										BinaryDiffContent binaryDiffContent = binaryDiffContentResult.Result;
										ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
										{
											c.DiffImageSourceChanged += delegate(object s, bool diffImageExists)
											{
												h.HighlightPixelsToggleButtonEnabled = diffImageExists;
											};
											c.UpdateDiff(repositoryUserControl, binaryDiffContent, showTitle: false);
											ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Image);
										});
									}
								}
							});
						}, JobFlags.Hidden);
						break;
					}
					_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading binary info for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
					{
						GitCommandResult<UnknownBinaryDiffContent> unknownBinaryDiffContentResult = FileDiffControl.LoadUnmergedUnknownBinaryDiffContent(unmergedDiffContent.DiffString, unmergedDiffContent.GitModule, changedFile, new JobMonitor());
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								_activeRefreshJob = null;
								if (!unknownBinaryDiffContentResult.Succeeded)
								{
									ShowErrorView(unknownBinaryDiffContentResult.Error);
								}
								else
								{
									UnknownBinaryDiffContent unknownBinaryDiffContent2 = unknownBinaryDiffContentResult.Result;
									ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
									{
										c.UpdateDiff(repositoryUserControl, unknownBinaryDiffContent2, showTitle: false);
										h.Hide();
									});
								}
							}
						});
					}, JobFlags.Hidden);
					break;
				case UnmergedDiffContent.ContentType.Submodule:
				{
					SubmoduleChangedFile submoduleChangedFile = changedFile as SubmoduleChangedFile;
					if (submoduleChangedFile == null)
					{
						break;
					}
					_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading submodule content for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
					{
						GitCommandResult<SubmoduleDiffContent> submoduleDiffContentResult = FileDiffControl.LoadUnmergedSubmoduleDiffContent(unmergedDiffContent.DiffString, unmergedDiffContent.GitModule, submoduleChangedFile, monitor);
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								_activeRefreshJob = null;
								if (!submoduleDiffContentResult.Succeeded)
								{
									ShowErrorView(submoduleDiffContentResult.Error);
								}
								else
								{
									ShowSubView(() => new SubmoduleDiffUserControl(), delegate(SubmoduleDiffUserControl c, FileControlHeaderUserControl h)
									{
										if (base.SubControlMode)
										{
											c.RevisionListViewPreviewMouseWheel += base.SubmoduleDiffUserControl_RevisionListView_PreviewMouseWheel;
										}
										c.Update(repositoryUserControl, submoduleDiffContentResult.Result, ViewMode.Merge);
										h.Collapse();
									});
								}
							}
						});
					}, JobFlags.Hidden);
					break;
				}
				case UnmergedDiffContent.ContentType.Text:
					break;
				}
			}
			else
			{
				if (base.Content.Result is TextDiffContent)
				{
					return;
				}
				result = base.Content.Result;
				BinaryDiffContent imageDiffContent = result as BinaryDiffContent;
				if (imageDiffContent != null)
				{
					ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
					{
						c.UpdateDiff(repositoryUserControl, imageDiffContent, showTitle: false);
						h.Collapse();
					});
					return;
				}
				result = base.Content.Result;
				UnknownBinaryDiffContent unknownBinaryDiffContent = result as UnknownBinaryDiffContent;
				if (unknownBinaryDiffContent != null)
				{
					ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
					{
						c.UpdateDiff(repositoryUserControl, unknownBinaryDiffContent, showTitle: false);
						h.Collapse();
					});
					return;
				}
				result = base.Content.Result;
				LfsDiffContent lfsDiffContent = result as LfsDiffContent;
				if (lfsDiffContent != null)
				{
					ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
					{
						c.UpdateDiff(repositoryUserControl, lfsDiffContent, showTitle: false);
						h.Collapse();
					});
					return;
				}
				result = base.Content.Result;
				SubmoduleDiffContent submoduleContent = result as SubmoduleDiffContent;
				if (submoduleContent != null)
				{
					ShowSubView(() => new SubmoduleDiffUserControl(), delegate(SubmoduleDiffUserControl c, FileControlHeaderUserControl h)
					{
						c.Update(repositoryUserControl, submoduleContent, ViewMode.Merge);
						h.Collapse();
					});
				}
			}
		}
	}
}
