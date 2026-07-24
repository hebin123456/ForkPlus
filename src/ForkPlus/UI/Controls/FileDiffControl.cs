// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF DependencyProperty → Avalonia StyledProperty。
// WPF DependencyPropertyChangedEventArgs → AvaloniaPropertyChangedEventArgs。
// WPF Key/Keyboard → Avalonia.Input.Key/FocusManager（Keyboard.FocusedElement 暂保留 + TODO）。
// WPF MouseWheelEventArgs → PointerWheelEventArgs。WPF PreviewMouseWheel → PointerWheelChanged。
// WPF FrameworkElement/UIElement → Avalonia.Controls.Control。
// WPF ContextMenu/Separator → Avalonia.Controls.ContextMenu/Separator。
// WPF RaiseEvent(MouseWheelEvent) 事件转发在 Avalonia 无等价物，标记 TODO(4.5)。
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using ForkPlus.Services;

namespace ForkPlus.UI.Controls
{
	public class FileDiffControl : DiffControlContainer
	{
		protected struct ParsedLfsDiff
		{
			[Null]
			public LfsPointer SrcPointer;

			[Null]
			public LfsPointer DstPointer;

			public ParsedLfsDiff(LfsPointer srcPointer, LfsPointer dstPointer)
			{
				SrcPointer = srcPointer;
				DstPointer = dstPointer;
			}
		}

		public EventHandler ShowLargeUntrackedChanges;

		public static readonly FileDiffControlCommands Commands = new FileDiffControlCommands();

		protected static readonly PatchParser Parser = new PatchParser();

		protected readonly CodeEditorScrollPositionCache _positionCache = new CodeEditorScrollPositionCache();

		[Null]
		protected Job _activeRefreshJob;

		// 阶段 4.5：WPF DependencyProperty.Register → Avalonia StyledProperty + AvaloniaProperty.Register<TOwner, TType>。
		public static readonly StyledProperty<RepositoryUserControl> RepositoryUserControlProperty =
			AvaloniaProperty.Register<FileDiffControl, RepositoryUserControl>(nameof(RepositoryUserControl));

		public static readonly StyledProperty<FileDiffControlTarget> TargetProperty =
			AvaloniaProperty.Register<FileDiffControl, FileDiffControlTarget>(nameof(Target), FileDiffControlTarget.Revision);

		public static readonly StyledProperty<bool> SubControlModeProperty =
			AvaloniaProperty.Register<FileDiffControl, bool>(nameof(SubControlMode), false);

		public static readonly StyledProperty<GitCommandResult<DiffContent>> ContentProperty =
			AvaloniaProperty.Register<FileDiffControl, GitCommandResult<DiffContent>>(nameof(Content));

		private static readonly Regex SubmoduleChangesMergeRegEx = new Regex("(\\b[0-9a-f]{40}),(\\b[0-9a-f]{40})", RegexOptions.Multiline | RegexOptions.Compiled);

		protected int MaxDiffSize
		{
			get
			{
				if (!SubControlMode)
				{
					return 1048576;
				}
				return 102400;
			}
		}

		public RepositoryUserControl RepositoryUserControl
		{
			get
			{
				return (RepositoryUserControl)GetValue(RepositoryUserControlProperty);
			}
			set
			{
				SetValue(RepositoryUserControlProperty, value);
			}
		}

		public FileDiffControlTarget Target
		{
			get
			{
				return (FileDiffControlTarget)GetValue(TargetProperty);
			}
			set
			{
				SetValue(TargetProperty, value);
			}
		}

		public bool SubControlMode
		{
			get
			{
				return (bool)GetValue(SubControlModeProperty);
			}
			set
			{
				SetValue(SubControlModeProperty, value);
			}
		}

		public GitCommandResult<DiffContent> Content
		{
			get
			{
				return (GitCommandResult<DiffContent>)GetValue(ContentProperty);
			}
			set
			{
				SetValue(ContentProperty, value);
			}
		}

		// 阶段 4.5：WPF OnPreviewKeyDown (tunneling) → Avalonia OnKeyDown (bubbling)。
		// 通过 e.Handled = true 阻止后续处理，达到 Preview 的效果。
		protected override void OnKeyDown(KeyEventArgs e)
		{
			// TODO(4.5): WPF Keyboard.FocusedElement → Avalonia FocusManager.GetFocusedElement()，需验证
			if (e.Key == Key.V && KeyboardHelper.IsCtrlDown && !(Keyboard.FocusedElement is TextBox))
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					string text = ServiceLocator.Clipboard.GetText();
					if (text != null && (text.StartsWith("diff ") || text.StartsWith("From ")))
					{
						e.Handled = true;
						byte[] bytes = Encoding.UTF8.GetBytes(text);
						new ShowApplyPatchWindowCommand().Execute(repositoryUserControl, bytes);
						return;
					}
				}
			}
			base.OnKeyDown(e);
		}

		// 阶段 4.5：WPF DependencyPropertyChangedEventArgs → Avalonia AvaloniaPropertyChangedEventArgs。
		// Avalonia 11 中 OnPropertyChanged 签名为 (AvaloniaPropertyChangedEventArgs change)，e.NewValue/e.Property 兼容。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == ContentProperty)
			{
				Content = e.NewValue as GitCommandResult<DiffContent>;
				UpdateView();
			}
			else if (e.Property == TargetProperty)
			{
				base.Header.Target = (FileDiffControlTarget)e.NewValue;
			}
		}

		protected virtual void UpdateView(bool loadLargeDiff = false)
		{
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			_activeRefreshJob?.Monitor.Cancel();
			_activeRefreshJob = null;
			if (Content == null)
			{
				ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					h.Collapse();
				});
				return;
			}
			if (!Content.Succeeded)
			{
				ShowErrorView(Content.Error);
				return;
			}
			ChangedFile changedFile = Content.Result.ChangedFile;
			DiffContent result = Content.Result;
			ParsedDiffContent parsedDiffContent = result as ParsedDiffContent;
			if (parsedDiffContent != null)
			{
				Diff diff2 = parsedDiffContent.Diff;
				if (diff2 == null)
				{
					ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
					{
						h.Collapse();
						c.FallbackMessage = PreferencesLocalization.Current("File has no changes");
					});
				}
				else if (diff2.Type == Diff.FileType.Text)
				{
					SubmoduleChangedFile submoduleChangedFile2 = changedFile as SubmoduleChangedFile;
					if (submoduleChangedFile2 != null)
					{
						_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading submodule content for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
						{
							GitCommandResult<SubmoduleDiffContent> submoduleDiffContentResult2 = LoadSubmoduleDiffContent(diff2, parsedDiffContent.GitModule, submoduleChangedFile2, monitor);
							base.Dispatcher.Async(delegate
							{
								if (!monitor.IsCanceled)
								{
									_activeRefreshJob = null;
									if (!submoduleDiffContentResult2.Succeeded)
									{
										ShowErrorView(submoduleDiffContentResult2.Error);
									}
									else
									{
										ShowSubView(() => new SubmoduleDiffUserControl(), delegate(SubmoduleDiffUserControl c, FileControlHeaderUserControl h)
										{
											if (SubControlMode)
											{
												c.RevisionListViewPreviewMouseWheel += SubmoduleDiffUserControl_RevisionListView_PreviewMouseWheel;
											}
											c.Update(repositoryUserControl, submoduleDiffContentResult2.Result);
											ShowHeaderIfAllowed(h, changedFile);
										});
									}
								}
							});
						}, JobFlags.Hidden);
						return;
					}
					if (IsLfsContent(diff2))
					{
						ParsedLfsDiff? parsedLfsDiff2 = AsLFSContent(diff2);
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
					ShowSubView(delegate
					{
						TextDiffControl textDiffControl3 = new TextDiffControl(Target);
						if (SubControlMode)
						{
							textDiffControl3.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
							textDiffControl3.PointerWheelChanged += DiffCodeEditor_PreviewMouseWheel;
						}
						textDiffControl3.PositionCache = _positionCache;
						return textDiffControl3;
					}, delegate(TextDiffControl c, FileControlHeaderUserControl h)
					{
						c.EditorContextMenuOpening += delegate(object s, ContextMenuEventArgs e)
						{
							DiffCodeEditor diffCodeEditor = e.Source as DiffCodeEditor;
							ContextMenu contextMenu = diffCodeEditor.ContextMenu;
							contextMenu.Items.Clear();
							Commands.OpenFileInExternalEditor.AddMenuItems(repositoryUserControl, diffCodeEditor, contextMenu, changedFile.Path);
							contextMenu.Items.Add(new Separator());
							Commands.HunkHistory.AddMenuItems(repositoryUserControl, diffCodeEditor, changedFile.Path, contextMenu);
							contextMenu.Items.Add(new Separator());
							Commands.Copy.AddMenuItems(diffCodeEditor, contextMenu);
							Commands.CopyAsPatch.AddMenuItems(diffCodeEditor, contextMenu);
						};
						c.SetDiff(diff2, parsedDiffContent.TabWidth, parsedDiffContent.EntireFile, DiffLocation.Revision);
						ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
					});
				}
				else if (diff2.Type == Diff.FileType.Binary)
				{
					if (PathHelper.IsImagePath(changedFile.Path))
					{
						_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading image content for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
						{
							GitCommandResult<BinaryDiffContent> binaryDiffContentResult = LoadBinaryDiffContent(diff2, repositoryUserControl.GitModule, changedFile, monitor);
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
											c.UpdateDiff(repositoryUserControl, binaryDiffContent);
											ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Image);
										});
									}
								}
							});
						}, JobFlags.Hidden);
						return;
					}
					_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading binary info for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
				{
					GitCommandResult<UnknownBinaryDiffContent> unknownBinaryDiffContentResult = LoadUnknownBinaryDiffContent(diff2, parsedDiffContent.GitModule, changedFile, new JobMonitor());
					// v3.1.0：当两侧大小均不超过阈值时，额外加载字节用于 Hex Diff 视图
					GitCommandResult<HexDiffContent> hexDiffContentResult = null;
					if (unknownBinaryDiffContentResult.Succeeded && CanLoadHexDiff(unknownBinaryDiffContentResult.Result))
					{
						hexDiffContentResult = LoadHexDiffContent(diff2, parsedDiffContent.GitModule, changedFile, monitor);
					}
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							_activeRefreshJob = null;
							if (!unknownBinaryDiffContentResult.Succeeded)
							{
								ShowErrorView(unknownBinaryDiffContentResult.Error);
							}
							else if (hexDiffContentResult != null && hexDiffContentResult.Succeeded)
							{
								// v3.1.0：Hex Diff 视图（side-by-side 字节比较）
								HexDiffContent hexDiffContent = hexDiffContentResult.Result;
								ShowSubView(() => new HexDiffUserControl(), delegate(HexDiffUserControl c, FileControlHeaderUserControl h)
								{
									c.SetContent(hexDiffContent);
									ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Hex);
								});
							}
							else
							{
								UnknownBinaryDiffContent unknownBinaryDiffContent2 = unknownBinaryDiffContentResult.Result;
								ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
								{
									c.UpdateDiff(repositoryUserControl, unknownBinaryDiffContent2);
									ShowHeaderIfAllowed(h, changedFile);
								});
							}
						}
					});
				}, JobFlags.Hidden);
				}
				else
				{
					if (diff2.Type != Diff.FileType.Submodule)
					{
						return;
					}
					SubmoduleChangedFile submoduleChangedFile = changedFile as SubmoduleChangedFile;
					if (submoduleChangedFile != null)
					{
						_activeRefreshJob = repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Loading submodule content for '{0}'", changedFile.Path), delegate(JobMonitor monitor)
						{
							GitCommandResult<SubmoduleDiffContent> submoduleDiffContentResult = LoadSubmoduleDiffContent(diff2, parsedDiffContent.GitModule, submoduleChangedFile, monitor);
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
											if (SubControlMode)
											{
												c.RevisionListViewPreviewMouseWheel += SubmoduleDiffUserControl_RevisionListView_PreviewMouseWheel;
											}
											c.Update(repositoryUserControl, submoduleDiffContentResult.Result);
											ShowHeaderIfAllowed(h, changedFile);
										});
									}
								}
							});
						}, JobFlags.Hidden);
						return;
					}
					ShowSubView(delegate
					{
						TextDiffControl textDiffControl2 = new TextDiffControl(Target);
						if (SubControlMode)
						{
							textDiffControl2.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
							textDiffControl2.PointerWheelChanged += DiffCodeEditor_PreviewMouseWheel;
						}
						textDiffControl2.PositionCache = _positionCache;
						return textDiffControl2;
					}, delegate(TextDiffControl c, FileControlHeaderUserControl h)
					{
						c.SetDiff(diff2, parsedDiffContent.TabWidth, parsedDiffContent.EntireFile, DiffLocation.Revision);
						ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
					});
				}
				return;
			}
			result = Content.Result;
			TextDiffContent textContent = result as TextDiffContent;
			if (textContent != null)
			{
				GitCommandResult<Patch> gitCommandResult = Parser.Parse(textContent.Text);
				if (!gitCommandResult.Succeeded)
				{
					return;
				}
				Diff diff = gitCommandResult.Result.Diffs.FirstItem();
				if (!loadLargeDiff && (textContent.Text.Length > MaxDiffSize || diff.IsMinified))
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
					TextDiffControl textDiffControl = new TextDiffControl(Target);
					if (SubControlMode)
					{
						textDiffControl.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
						textDiffControl.PointerWheelChanged += DiffCodeEditor_PreviewMouseWheel;
					}
					textDiffControl.PositionCache = _positionCache;
					return textDiffControl;
				}, delegate(TextDiffControl c, FileControlHeaderUserControl h)
				{
					c.SetDiff(diff, textContent.TabWidth, textContent.EntireFile, DiffLocation.Revision);
					ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Text);
				});
				return;
			}
			result = Content.Result;
			BinaryDiffContent imageDiffContent = result as BinaryDiffContent;
			if (imageDiffContent != null)
			{
				ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
				{
					c.DiffImageSourceChanged += delegate(object s, bool diffImageExists)
					{
						h.HighlightPixelsToggleButtonEnabled = diffImageExists;
					};
					c.UpdateDiff(repositoryUserControl, imageDiffContent);
					ShowHeaderIfAllowed(h, changedFile, FileControlHeaderMode.Image);
				});
				return;
			}
			result = Content.Result;
			UnknownBinaryDiffContent unknownBinaryDiffContent = result as UnknownBinaryDiffContent;
			if (unknownBinaryDiffContent != null)
			{
				ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
				{
					c.UpdateDiff(repositoryUserControl, unknownBinaryDiffContent);
					ShowHeaderIfAllowed(h, changedFile);
				});
				return;
			}
			result = Content.Result;
			LfsDiffContent lfsDiffContent = result as LfsDiffContent;
			if (lfsDiffContent != null)
			{
				ShowSubView(() => new BinaryDiffUserControl(), delegate(BinaryDiffUserControl c, FileControlHeaderUserControl h)
				{
					c.DiffImageSourceChanged += delegate(object s, bool diffImageExists)
					{
						h.HighlightPixelsToggleButtonEnabled = diffImageExists;
					};
					c.UpdateDiff(repositoryUserControl, lfsDiffContent);
					PathHelper.IsImagePath(changedFile.Path);
					ShowHeaderIfAllowed(h, changedFile);
				});
				return;
			}
			result = Content.Result;
			SubmoduleDiffContent submoduleContent = result as SubmoduleDiffContent;
			if (submoduleContent != null)
			{
				ShowSubView(() => new SubmoduleDiffUserControl(), delegate(SubmoduleDiffUserControl c, FileControlHeaderUserControl h)
				{
					c.Update(repositoryUserControl, submoduleContent);
					ShowHeaderIfAllowed(h, changedFile);
				});
			}
		}

		protected void ShowErrorView(GitCommandError error)
		{
			ShowSubView(() => new FallbackUserControl(), delegate(FallbackUserControl c, FileControlHeaderUserControl h)
			{
				c.FallbackTitle = PreferencesLocalization.Current("Error");
				c.FallbackMessage = error.FriendlyDescription;
				h.Collapse();
			});
		}

		private static GitCommandResult<BinaryDiffContent> LoadBinaryDiffContent(Diff diff, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			string srcObject = diff.SrcObject;
			if (srcObject != null)
			{
				Sha? sha = Sha.Parse(srcObject);
				if (sha.HasValue)
				{
					Sha valueOrDefault = sha.GetValueOrDefault();
					string dstObject = diff.DstObject;
					if (dstObject != null)
					{
						sha = Sha.Parse(dstObject);
						if (sha.HasValue)
						{
							Sha valueOrDefault2 = sha.GetValueOrDefault();
							return LoadBinaryDiffContent(valueOrDefault, valueOrDefault2, gitModule, changedFile, monitor);
						}
					}
				}
			}
			return GitCommandResult<BinaryDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in binary diff"));
		}

		protected static GitCommandResult<BinaryDiffContent> LoadUnmergedBinaryDiffContent(string diffString, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			MatchCollection matchCollection = GetFileChangesGitCommand.BinaryFileMergeRegEx.Matches(diffString);
			if (matchCollection.Count == 1)
			{
				string value = matchCollection[0].Groups[1].Value;
				if (value != null)
				{
					Sha? sha = Sha.Parse(value);
					if (sha.HasValue)
					{
						Sha valueOrDefault = sha.GetValueOrDefault();
						string value2 = matchCollection[0].Groups[2].Value;
						if (value2 != null)
						{
							sha = Sha.Parse(value2);
							if (sha.HasValue)
							{
								Sha valueOrDefault2 = sha.GetValueOrDefault();
								return LoadBinaryDiffContent(valueOrDefault2, valueOrDefault, gitModule, changedFile, monitor);
							}
						}
					}
				}
			}
			return GitCommandResult<BinaryDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in binary merge diff"));
		}

		private static GitCommandResult<BinaryDiffContent> LoadBinaryDiffContent(Sha srcSha, Sha dstSha, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				return GitCommandResult<BinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			GitCommandResult<MemoryStream> gitCommandResult = new GetBlobGitCommand().Execute(gitModule, new BlobTarget.Blob(srcSha));
			if (monitor.IsCanceled)
			{
				return GitCommandResult<BinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<BinaryDiffContent>.Failure(gitCommandResult.Error);
			}
			BlobTarget target = ((!changedFile.Tracked || !changedFile.Staged) ? ((BlobTarget)new BlobTarget.Unstaged(changedFile.Path)) : ((BlobTarget)new BlobTarget.Blob(dstSha)));
			GitCommandResult<MemoryStream> gitCommandResult2 = new GetBlobGitCommand().Execute(gitModule, target);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<BinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<BinaryDiffContent>.Failure(gitCommandResult2.Error);
			}
			return GitCommandResult<BinaryDiffContent>.Success(new BinaryDiffContent(changedFile, gitCommandResult.Result, gitCommandResult2.Result));
		}

		private static GitCommandResult<UnknownBinaryDiffContent> LoadUnknownBinaryDiffContent(Diff diff, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			string srcObject = diff.SrcObject;
			if (srcObject != null)
			{
				Sha? sha = Sha.Parse(srcObject);
				if (sha.HasValue)
				{
					Sha valueOrDefault = sha.GetValueOrDefault();
					string dstObject = diff.DstObject;
					if (dstObject != null)
					{
						sha = Sha.Parse(dstObject);
						if (sha.HasValue)
						{
							Sha valueOrDefault2 = sha.GetValueOrDefault();
							return LoadUnknownBinaryDiffContent(valueOrDefault, valueOrDefault2, gitModule, changedFile, monitor);
						}
					}
				}
			}
			return GitCommandResult<UnknownBinaryDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in binary diff"));
		}

		protected static GitCommandResult<UnknownBinaryDiffContent> LoadUnmergedUnknownBinaryDiffContent(string diffString, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			MatchCollection matchCollection = GetFileChangesGitCommand.BinaryFileMergeRegEx.Matches(diffString);
			if (matchCollection.Count == 1)
			{
				string value = matchCollection[0].Groups[1].Value;
				if (value != null)
				{
					Sha? sha = Sha.Parse(value);
					if (sha.HasValue)
					{
						Sha valueOrDefault = sha.GetValueOrDefault();
						string value2 = matchCollection[0].Groups[2].Value;
						if (value2 != null)
						{
							sha = Sha.Parse(value2);
							if (sha.HasValue)
							{
								Sha valueOrDefault2 = sha.GetValueOrDefault();
								return LoadUnknownBinaryDiffContent(valueOrDefault2, valueOrDefault, gitModule, changedFile, monitor);
							}
						}
					}
				}
			}
			return GitCommandResult<UnknownBinaryDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in binary merge diff"));
		}

		private static GitCommandResult<UnknownBinaryDiffContent> LoadUnknownBinaryDiffContent(Sha srcSha, Sha dstSha, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				return GitCommandResult<UnknownBinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			GitCommandResult<long?> gitCommandResult = new GetBlobSizeGitCommand().Execute(gitModule, new BlobTarget.Blob(srcSha));
			if (monitor.IsCanceled)
			{
				return GitCommandResult<UnknownBinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<UnknownBinaryDiffContent>.Failure(gitCommandResult.Error);
			}
			BlobTarget target = ((!changedFile.Tracked || !changedFile.Staged) ? ((BlobTarget)new BlobTarget.Unstaged(changedFile.Path)) : ((BlobTarget)new BlobTarget.Blob(dstSha)));
			GitCommandResult<long?> gitCommandResult2 = new GetBlobSizeGitCommand().Execute(gitModule, target);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<UnknownBinaryDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<UnknownBinaryDiffContent>.Failure(gitCommandResult2.Error);
			}
			return GitCommandResult<UnknownBinaryDiffContent>.Success(new UnknownBinaryDiffContent(changedFile, gitCommandResult.Result, gitCommandResult2.Result));
	}

	/// <summary>v3.1.0：Hex Diff 自动加载阈值。两侧 blob 大小均不超过此值时自动加载字节并启用 Hex Diff 视图。</summary>
	private const long MaxHexDiffSize = 10 * 1024 * 1024;

	/// <summary>v3.1.0：判断 UnknownBinaryDiffContent 是否可以升级为 HexDiffContent（两侧大小均不超过阈值且非空）。</summary>
	private static bool CanLoadHexDiff(UnknownBinaryDiffContent content)
	{
		if (content == null) return false;
		long? src = content.SrcSize;
		long? dst = content.DstSize;
		// 纯新增/纯删除文件（一侧为 0 或 null）也允许，另一侧只要不超过阈值即可
		if (src.HasValue && src.Value > MaxHexDiffSize) return false;
		if (dst.HasValue && dst.Value > MaxHexDiffSize) return false;
		return true;
	}

	/// <summary>v3.1.0：加载两侧 blob 字节，构造 HexDiffContent。
	/// 复用 LoadUnknownBinaryDiffContent 里 src/dst BlobTarget 的判定逻辑。</summary>
	private static GitCommandResult<HexDiffContent> LoadHexDiffContent(Diff diff, GitModule gitModule, ChangedFile changedFile, JobMonitor monitor)
	{
		string srcObject = diff.SrcObject;
		string dstObject = diff.DstObject;
		Sha? srcSha = srcObject != null ? Sha.Parse(srcObject) : (Sha?)null;
		Sha? dstSha = dstObject != null ? Sha.Parse(dstObject) : (Sha?)null;
		if (srcSha == null && dstSha == null)
		{
			return GitCommandResult<HexDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in binary diff for hex view"));
		}

		MemoryStream srcData = null;
		MemoryStream dstData = null;
		try
		{
			// src 侧：通过 BlobTarget.Blob(srcSha) 加载；srcSha 为 null 时（纯新增）跳过
			if (srcSha.HasValue && srcSha.GetValueOrDefault() != Sha.Zero)
			{
				if (monitor.IsCanceled) return GitCommandResult<HexDiffContent>.Failure(new GitCommandError.Cancelled());
				GitCommandResult<MemoryStream> srcResult = new GetBlobGitCommand().Execute(gitModule, new BlobTarget.Blob(srcSha.GetValueOrDefault()));
				if (!srcResult.Succeeded) return GitCommandResult<HexDiffContent>.Failure(srcResult.Error);
				srcData = srcResult.Result;
			}

			// dst 侧：复用 LoadUnknownBinaryDiffContent 的 BlobTarget 选择逻辑
			if (dstSha.HasValue)
			{
				if (monitor.IsCanceled) return GitCommandResult<HexDiffContent>.Failure(new GitCommandError.Cancelled());
				BlobTarget dstTarget = (!changedFile.Tracked || !changedFile.Staged)
					? (BlobTarget)new BlobTarget.Unstaged(changedFile.Path)
					: (BlobTarget)new BlobTarget.Blob(dstSha.GetValueOrDefault());
				GitCommandResult<MemoryStream> dstResult = new GetBlobGitCommand().Execute(gitModule, dstTarget);
				if (!dstResult.Succeeded) return GitCommandResult<HexDiffContent>.Failure(dstResult.Error);
				dstData = dstResult.Result;
			}
			return GitCommandResult<HexDiffContent>.Success(new HexDiffContent(changedFile, srcData, dstData));
		}
		catch (Exception ex)
		{
			srcData?.Dispose();
			dstData?.Dispose();
			return GitCommandResult<HexDiffContent>.Failure(ex);
		}
	}

	private static GitCommandResult<SubmoduleDiffContent> LoadSubmoduleDiffContent(Diff diff, GitModule gitModule, SubmoduleChangedFile submoduleChangedFile, JobMonitor monitor)
		{
			string[] lines = diff.Lines;
			GitCommandResult<Sha?> gitCommandResult = ParseSubmoduleSha(diff, diff.Chunks.FirstItem()?.SubChunks.FirstItem()?.Deleted);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult.Error);
			}
			Sha sha = gitCommandResult.Result ?? Sha.Zero;
			GitCommandResult<Sha?> gitCommandResult2 = ParseSubmoduleSha(diff, diff.Chunks.FirstItem()?.SubChunks.FirstItem()?.Added);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult.Error);
			}
			Sha sha2 = gitCommandResult2.Result ?? Sha.Zero;
			if (sha == Sha.Zero && sha2 == Sha.Zero)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.ParseError("Cannot find chunk ranges in submodule diff"));
			}
			bool isSubmoduleWorkingDirectoryDirty = false;
			Range? range = diff.Chunks.FirstItem()?.SubChunks.FirstItem()?.Added;
			if (range.HasValue)
			{
				Range valueOrDefault = range.GetValueOrDefault();
				if (!valueOrDefault.IsEmpty)
				{
					isSubmoduleWorkingDirectoryDirty = lines[valueOrDefault.Start].TrimEnd().EndsWith("-dirty");
				}
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			GitCommandResult<GitModule> gitCommandResult3 = new OpenGitRepositoryGitCommand().Execute(gitModule, submoduleChangedFile.Submodule);
			if (gitCommandResult3.Succeeded)
			{
				GitModule result = gitCommandResult3.Result;
				if (result != null)
				{
					GitCommandResult<SubmoduleDiffContent> gitCommandResult4 = new GetSubmoduleDiffContentGitCommand().Execute(result, gitModule, sha, sha2, isSubmoduleWorkingDirectoryDirty, submoduleChangedFile, monitor);
					if (monitor.IsCanceled)
					{
						return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
					}
					if (gitCommandResult4.Succeeded)
					{
						SubmoduleDiffContent result2 = gitCommandResult4.Result;
						if (result2 != null)
						{
							return GitCommandResult<SubmoduleDiffContent>.Success(result2);
						}
					}
					Log.Warn("Can't read submodule diff content '" + submoduleChangedFile.Submodule.Path + "'");
					return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult4.Error);
				}
			}
			Log.Warn("Can't open submodule '" + submoduleChangedFile.Submodule.Path + "'");
			return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult3.Error);
		}

		private static GitCommandResult<Sha?> ParseSubmoduleSha(Diff diff, Range? lineRange)
		{
			if (lineRange.HasValue)
			{
				Range valueOrDefault = lineRange.GetValueOrDefault();
				if (valueOrDefault.IsEmpty)
				{
					return GitCommandResult<Sha?>.Success(null);
				}
				string text = "Subproject commit ";
				if (!diff.Lines[valueOrDefault.Start].StartsWith(text))
				{
					return GitCommandResult<Sha?>.Failure(new GitCommandError.ParseError("Cannot find src/dst in submodule diff"));
				}
				string text2 = diff.Lines[valueOrDefault.Start].Substring(text.Length);
				if (text2.Length >= 40)
				{
					Sha? sha = Sha.Parse(text2.Substring(0, 40));
					if (sha.HasValue)
					{
						Sha valueOrDefault2 = sha.GetValueOrDefault();
						return GitCommandResult<Sha?>.Success(valueOrDefault2);
					}
				}
				return GitCommandResult<Sha?>.Failure(new GitCommandError.ParseError("Can not parse src/dst in submodule diff"));
			}
			return GitCommandResult<Sha?>.Success(null);
		}

		protected static GitCommandResult<SubmoduleDiffContent> LoadUnmergedSubmoduleDiffContent(string diffString, GitModule gitModule, SubmoduleChangedFile submoduleChangedFile, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			if (!TryParseMergeSubmoduleChange(diffString, out var src, out var dst))
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.ParseError("Can not find src/dst in submodule merge diff"));
			}
			GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(gitModule, submoduleChangedFile.Submodule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult.Error);
			}
			return new GetSubmoduleDiffContentGitCommand().Execute(gitCommandResult.Result, gitModule, dst, src, isSubmoduleWorkingDirectoryDirty: false, submoduleChangedFile, new JobMonitor());
		}

		private static bool TryParseMergeSubmoduleChange(string output, out Sha src, out Sha dst)
		{
			MatchCollection matchCollection = SubmoduleChangesMergeRegEx.Matches(output);
			if (matchCollection.Count != 1)
			{
				src = (dst = Sha.Zero);
				return false;
			}
			if (!Sha.TryParse(matchCollection[0].Groups[1].Value, out var result) || !Sha.TryParse(matchCollection[0].Groups[2].Value, out var result2))
			{
				src = (dst = Sha.Zero);
				return false;
			}
			src = result;
			dst = result2;
			return true;
		}

		protected void ShowHeaderIfAllowed(FileControlHeaderUserControl header, ChangedFile changedFile, FileControlHeaderMode mode = FileControlHeaderMode.None)
		{
			if (SubControlMode)
			{
				header.Collapse();
			}
			else
			{
				header.Show(changedFile.Path, changedFile.OldPath, mode);
			}
		}

		// 阶段 4.5：WPF MouseWheelEventArgs → Avalonia PointerWheelEventArgs。
		// WPF 通过 new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) + RaiseEvent(MouseWheelEvent)
		// 将滚轮事件转发给父级控件。Avalonia PointerWheelEventArgs 无法外部构造后 RaiseEvent 重新派发，
		// 需运行时验证：改为调用父级 ((Control)sender).Parent 的程序化滚动 API（参考 FloatingButton.cs）。
		protected void DiffCodeEditor_PreviewMouseWheel(object sender, PointerWheelEventArgs e)
		{
			if (sender is TextDiffControl && !e.Handled)
			{
				e.Handled = true;
				// TODO(4.5): WPF MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) + RaiseEvent 转发模式
				// 在 Avalonia 无等价物，需运行时验证。原 WPF 代码（已注释）：
				// MouseWheelEventArgs mouseWheelEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
				// mouseWheelEventArgs.RoutedEvent = UIElement.MouseWheelEvent;
				// mouseWheelEventArgs.Source = sender;
				// (((FrameworkElement)sender).Parent as FrameworkElement)?.RaiseEvent(mouseWheelEventArgs);
			}
		}

		// 阶段 4.5：WPF MouseWheelEventArgs → Avalonia PointerWheelEventArgs（同上）。
		protected void SubmoduleDiffUserControl_RevisionListView_PreviewMouseWheel(object sender, PointerWheelEventArgs e)
		{
			if (sender is NoUIAutomationListView && !e.Handled)
			{
				e.Handled = true;
				// TODO(4.5): WPF MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) + RaiseEvent 转发模式
				// 在 Avalonia 无等价物，需运行时验证。原 WPF 代码（已注释）：
				// MouseWheelEventArgs mouseWheelEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
				// mouseWheelEventArgs.RoutedEvent = UIElement.MouseWheelEvent;
				// mouseWheelEventArgs.Source = sender;
				// (((FrameworkElement)sender).Parent as FrameworkElement)?.RaiseEvent(mouseWheelEventArgs);
			}
		}

		protected bool IsLargeOrMinified(Diff diff)
		{
			if (diff.IsMinified)
			{
				return true;
			}
			int num = 0;
			string[] lines = diff.Lines;
			foreach (string text in lines)
			{
				num += text.Length;
			}
			return num > MaxDiffSize;
		}

		protected static bool IsLfsContent(Diff diff)
		{
			if (diff.Lines.Length == 0 || diff.Lines.Length > 6)
			{
				return false;
			}
			if (!diff.Lines[0].StartsWith("version https://git-lfs.github.com/spec/v1"))
			{
				return false;
			}
			return true;
		}

		[Null]
		protected static ParsedLfsDiff? AsLFSContent(Diff diff)
		{
			SubChunk subChunk = diff.Chunks.FirstItem()?.SubChunks.FirstItem();
			if (subChunk == null)
			{
				Log.Warn("Cannot parse LFS diff. Diff has no subchunks");
				return null;
			}
			LfsPointer srcPointer = null;
			LfsPointer dstPointer = null;
			if (subChunk.PreContext.Length != 0 && subChunk.Deleted.Length == 2 && subChunk.Added.Length == 2)
			{
				srcPointer = LfsPointer.Parse(diff.Lines[subChunk.Deleted.Start], diff.Lines[subChunk.Deleted.Start + 1]);
				dstPointer = LfsPointer.Parse(diff.Lines[subChunk.Added.Start], diff.Lines[subChunk.Added.Start + 1]);
			}
			else if (subChunk.PreContext.Length != 0 && subChunk.Deleted.Length == 1 && subChunk.Added.Length == 1 && subChunk.PostContext.Length == 1)
			{
				srcPointer = LfsPointer.Parse(diff.Lines[subChunk.Deleted.Start], diff.Lines[subChunk.PostContext.Start]);
				dstPointer = LfsPointer.Parse(diff.Lines[subChunk.Added.Start], diff.Lines[subChunk.PostContext.Start]);
			}
			else if (subChunk.Added.Length == 3)
			{
				dstPointer = LfsPointer.Parse(diff.Lines[subChunk.Added.Start + 1], diff.Lines[subChunk.Added.Start + 2]);
			}
			else
			{
				if (subChunk.Deleted.Length != 3)
				{
					Log.Error($"Cannot parse LFS diff. {subChunk.PreContext.Length} -{subChunk.Deleted.Length} +{subChunk.Added.Length}");
					return null;
				}
				srcPointer = LfsPointer.Parse(diff.Lines[subChunk.Deleted.Start + 1], diff.Lines[subChunk.Deleted.Start + 2]);
			}
			return new ParsedLfsDiff(srcPointer, dstPointer);
		}
	}
}
