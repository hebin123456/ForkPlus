using System;
using System.Windows.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls.BinaryDiff;

namespace ForkPlus.UI.Controls
{
	public class CommitFileDiffControl : FileDiffControl
	{
		public event EventHandler<CommitCodeEditor> ToggleStage;

		public event EventHandler<CommitCodeEditor> Stage;

		public event EventHandler<CommitCodeEditor> UnStage;

		public event EventHandler<CommitCodeEditor> Discard;

		public CommitFileDiffControl()
		{
			base.Target = FileDiffControlTarget.Commit;
		}

		protected override void UpdateView(bool loadLargeDiff)
		{
			RepositoryUserControl repositoryUserControl = base.RepositoryUserControl;
			GitCommandResult<DiffContent> content = base.Content;
			if (base.Content == null)
			{
				ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					h.Collapse();
				});
				return;
			}
			if (!base.Content.Succeeded)
			{
				GitCommandError error2 = base.Content.Error;
				GitCommandError.ChangesAreTooLarge error = error2 as GitCommandError.ChangesAreTooLarge;
				if (error != null)
				{
					ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
					{
						c.ResetEvents();
						c.FallbackMessage = PreferencesLocalization.Current("Changes are too large (") + FileHelper.GetReadableFileSize(error.FileSize, addSizeInBytes: false) + ")";
						c.Button1Title = PreferencesLocalization.Current("Load Diff");
						c.Button1Click += delegate
						{
							ShowLargeUntrackedChanges?.Invoke(this, EventArgs.Empty);
						};
						h.Collapse();
					});
				}
				else
				{
					base.UpdateView();
				}
				return;
			}
			ChangedFile changedFile = base.Content.Result.ChangedFile;
			if (changedFile.ChangeType == ChangeType.Unmerged)
			{
				bool resolved = content.Result.IsConflictResolved();
				ShowSubView(() => new MergeConflictUserControl(), delegate(MergeConflictUserControl c, FileControlHeaderUserControl h)
				{
					c.SetConflict(repositoryUserControl, base.Content.Result, repositoryUserControl.RepositoryStatus?.RepositoryState, changedFile, resolved);
					if (resolved)
					{
						h.Collapse();
					}
					else
					{
						ShowHeaderIfAllowed(h, changedFile);
					}
				});
				return;
			}
			DiffContent result = base.Content.Result;
			ParsedDiffContent parsedDiffContent = result as ParsedDiffContent;
			if (parsedDiffContent != null)
			{
				Diff diff2 = parsedDiffContent.Diff;
				if (diff2 == null)
				{
					ShowSubView(delegate
					{
						TextDiffControl textDiffControl = new TextDiffControl(base.Target);
						if (base.SubControlMode)
						{
							textDiffControl.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
							textDiffControl.PreviewMouseWheel += base.DiffCodeEditor_PreviewMouseWheel;
						}
						textDiffControl.PositionCache = _positionCache;
						return textDiffControl;
					}, delegate(TextDiffControl c, FileControlHeaderUserControl h)
					{
						c.SetDiff(diff2, parsedDiffContent.TabWidth, parsedDiffContent.EntireFile, DiffLocation.Revision);
						ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
					});
				}
				else if (diff2.Type == Diff.FileType.Text)
				{
					if (changedFile is SubmoduleChangedFile)
					{
						base.UpdateView(loadLargeDiff);
						return;
					}
					if (FileDiffControl.IsLfsContent(diff2))
					{
						ParsedLfsDiff? parsedLfsDiff2 = FileDiffControl.AsLFSContent(diff2);
						if (parsedLfsDiff2.HasValue)
						{
							ParsedLfsDiff parsedLfsDiff = parsedLfsDiff2.GetValueOrDefault();
							ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
							{
								c.DiffImageSourceChanged += delegate(object s, bool diffImageExists)
								{
									h.HighlightPixelsToggleButtonEnabled = diffImageExists;
								};
								BinaryFileType binaryFileType = ((!PathHelper.IsImagePath(changedFile.Path)) ? BinaryFileType.LfsBinaryFile : BinaryFileType.LfsImage);
								LfsDiffContent diffContent = new LfsDiffContent(repositoryUserControl.GitModule, parsedDiffContent.ChangedFile, binaryFileType, parsedLfsDiff.SrcPointer, parsedLfsDiff.DstPointer);
								c.UpdateDiff(repositoryUserControl, diffContent);
								FileControlHeaderMode mode = (PathHelper.IsImagePath(changedFile.Path) ? FileControlHeaderMode.Image : FileControlHeaderMode.None);
								ShowHeaderIfAllowed(h, changedFile, mode);
							});
							return;
						}
					}
					if (!loadLargeDiff && IsLargeOrMinified(diff2))
					{
						ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
						{
							c.ResetEvents();
							c.FallbackMessage = PreferencesLocalization.Current("Changes are too large to display");
							c.Button1Title = PreferencesLocalization.Current("Load Diff");
							c.Button1Click += delegate
							{
								UpdateView(loadLargeDiff: true);
							};
							h.Collapse();
						});
						return;
					}
					DiffLocation location2 = (changedFile.Staged ? DiffLocation.Staged : DiffLocation.Unstaged);
					ShowSubView(delegate
					{
						CommitTextDiffControl commitTextDiffControl2 = new CommitTextDiffControl(base.Target);
						commitTextDiffControl2.PositionCache = _positionCache;
						commitTextDiffControl2.ToggleStage += delegate(object s, CommitCodeEditor editor)
						{
							this.ToggleStage?.Invoke(this, editor);
						};
						commitTextDiffControl2.Stage += delegate(object s, CommitCodeEditor editor)
						{
							this.Stage?.Invoke(this, editor);
						};
						commitTextDiffControl2.Unstage += delegate(object s, CommitCodeEditor editor)
						{
							this.UnStage?.Invoke(this, editor);
						};
						commitTextDiffControl2.Discard += delegate(object s, CommitCodeEditor editor)
						{
							this.Discard?.Invoke(this, editor);
						};
						return commitTextDiffControl2;
					}, delegate(CommitTextDiffControl c, FileControlHeaderUserControl h)
					{
						c.IsStaged = changedFile.Staged;
						c.IsNewOrUntracked = !changedFile.Tracked || changedFile.New;
						c.EditorContextMenuOpening += delegate(object s, ContextMenuEventArgs e)
						{
							DiffCodeEditor diffCodeEditor2 = e.Source as DiffCodeEditor;
							ContextMenu contextMenu2 = diffCodeEditor2.ContextMenu;
							contextMenu2.Items.Clear();
							FileDiffControl.Commands.OpenFileInExternalEditor.AddMenuItems(repositoryUserControl, diffCodeEditor2, contextMenu2, changedFile.Path);
							contextMenu2.Items.Add(new Separator());
							FileDiffControl.Commands.HunkHistory.AddMenuItems(repositoryUserControl, diffCodeEditor2, changedFile.Path, contextMenu2);
							contextMenu2.Items.Add(new Separator());
							FileDiffControl.Commands.Copy.AddMenuItems(diffCodeEditor2, contextMenu2);
							FileDiffControl.Commands.CopyAsPatch.AddMenuItems(diffCodeEditor2, contextMenu2);
						};
						c.SetDiff(diff2, parsedDiffContent.TabWidth, parsedDiffContent.EntireFile, location2);
						ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
					});
				}
				else
				{
					base.UpdateView(loadLargeDiff);
				}
				return;
			}
			result = base.Content.Result;
			TextDiffContent textContent = result as TextDiffContent;
			if (textContent != null)
			{
				DiffLocation location = (changedFile.Staged ? DiffLocation.Staged : DiffLocation.Unstaged);
				GitCommandResult<Patch> gitCommandResult = FileDiffControl.Parser.Parse(textContent.Text);
				if (!gitCommandResult.Succeeded)
				{
					return;
				}
				Diff diff = gitCommandResult.Result.Diffs.FirstItem();
				if (!loadLargeDiff && diff != null && (textContent.Text.Length > base.MaxDiffSize || diff.IsMinified))
				{
					ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
					{
						c.ResetEvents();
						c.FallbackMessage = PreferencesLocalization.Current("Changes are too large to display");
						c.Button1Title = PreferencesLocalization.Current("Load Diff");
						c.Button1Click += delegate
						{
							UpdateView(loadLargeDiff: true);
						};
						h.Collapse();
					});
					return;
				}
				ShowSubView(delegate
				{
					CommitTextDiffControl commitTextDiffControl = new CommitTextDiffControl(base.Target);
					commitTextDiffControl.PositionCache = _positionCache;
					commitTextDiffControl.ToggleStage += delegate(object s, CommitCodeEditor editor)
					{
						this.ToggleStage?.Invoke(this, editor);
					};
					commitTextDiffControl.Stage += delegate(object s, CommitCodeEditor editor)
					{
						this.Stage?.Invoke(this, editor);
					};
					commitTextDiffControl.Unstage += delegate(object s, CommitCodeEditor editor)
					{
						this.UnStage?.Invoke(this, editor);
					};
					commitTextDiffControl.Discard += delegate(object s, CommitCodeEditor editor)
					{
						this.Discard?.Invoke(this, editor);
					};
					return commitTextDiffControl;
				}, delegate(CommitTextDiffControl c, FileControlHeaderUserControl h)
				{
					c.IsStaged = changedFile.Staged;
					c.IsNewOrUntracked = !changedFile.Tracked || changedFile.New;
					c.EditorContextMenuOpening += delegate(object s, ContextMenuEventArgs e)
					{
						DiffCodeEditor diffCodeEditor = e.Source as DiffCodeEditor;
						ContextMenu contextMenu = diffCodeEditor.ContextMenu;
						contextMenu.Items.Clear();
						FileDiffControl.Commands.OpenFileInExternalEditor.AddMenuItems(repositoryUserControl, diffCodeEditor, contextMenu, changedFile.Path);
						FileDiffControl.Commands.Copy.AddMenuItems(diffCodeEditor, contextMenu);
						FileDiffControl.Commands.CopyAsPatch.AddMenuItems(diffCodeEditor, contextMenu);
					};
					c.SetDiff(diff, textContent.TabWidth, textContent.EntireFile, location);
					ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
				});
				return;
			}
			result = base.Content.Result;
			SubmoduleDiffContent submoduleContent = result as SubmoduleDiffContent;
			if (submoduleContent != null)
			{
				ShowSubView(() => new SubmoduleDiffUserControl(), delegate(SubmoduleDiffUserControl c, FileControlHeaderUserControl h)
				{
					c.Update(repositoryUserControl, submoduleContent, ViewMode.Commmit);
					ShowHeaderIfAllowed(h, changedFile);
				});
			}
			else
			{
				base.UpdateView();
			}
		}
	}
}
