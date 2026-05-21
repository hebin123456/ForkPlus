using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Parsing;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Merge;
using ForkPlus.UI.UserControls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Dialogs
{
	public partial class SideBySideMergeWindow : ForkPlusDialogWindow
	{
		private enum MergeMode
		{
			Undefined,
			Text,
			Binary
		}

		private static readonly UnifiedMergeParser _mergeConflictParser = new UnifiedMergeParser();

		private readonly GitModule _gitModule;

		private readonly RepositoryState _repositoryState;

		private readonly ChangedFile _changedFile;

		private MergeConflict _mergeConflict;

		private bool _stopCheckBoxEvents;

		private bool _startUpFinished;

		private MergeMode _mergeMode;

		private DiffContent _fileContent;

		private DateTime _lastLastScrollTime;

		private MergeCodeEditor _lastUpdatedEditor;

		private bool _refreshInProgress;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (_mergeMode == MergeMode.Text)
				{
					if (_mergeConflict == null)
					{
						return false;
					}
					return _mergeConflict.IsResolved;
				}
				if (_mergeMode == MergeMode.Binary)
				{
					if (!AllLocalCheckBox.IsChecked.GetValueOrDefault() || AllRemoteCheckBox.IsChecked != false)
					{
						if (AllRemoteCheckBox.IsChecked.GetValueOrDefault())
						{
							return AllLocalCheckBox.IsChecked == false;
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}

		public SideBySideMergeWindow(RepositoryUserControl repositoryUserControl, RepositoryState repositoryState, ChangedFile changedFile)
		{
			_gitModule = repositoryUserControl.GitModule;
			_repositoryState = repositoryState;
			_changedFile = changedFile;
			base.ShowHeader = false;
			base.ShowLogo = false;
			InitializeComponent();
			base.SubmitButtonTitle = "Resolve";
			FileMergeControl.RepositoryUserControl = repositoryUserControl;
			LocalMergeEditor.ViewMode = MergeConflictPart.Local;
			RemoteMergeEditor.ViewMode = MergeConflictPart.Remote;
			MergedMergeEditor.ViewMode = MergeConflictPart.Merged;
			RemoteMergeEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
			base.SizeChanged += Window_SizeChanged;
			base.Activated += Window_Activated;
			RemoteMergeEditor.MergeLineAdded += MergeEditor_MergeLineAdded;
			RemoteMergeEditor.MergeLineRemoved += MergeEditor_MergeLineRemoved;
			RemoteMergeEditor.MergeChunkAdded += MergeEditor_MergeChunkAdded;
			RemoteMergeEditor.MergeChunkRemoved += MergeEditor_MergeChunkRemoved;
			LocalMergeEditor.MergeLineAdded += MergeEditor_MergeLineAdded;
			LocalMergeEditor.MergeLineRemoved += MergeEditor_MergeLineRemoved;
			LocalMergeEditor.MergeChunkAdded += MergeEditor_MergeChunkAdded;
			LocalMergeEditor.MergeChunkRemoved += MergeEditor_MergeChunkRemoved;
			RemoteMergeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(RemoteMergeEditor);
			};
			LocalMergeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(LocalMergeEditor);
			};
			MergedMergeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(MergedMergeEditor);
			};
			base.Loaded += delegate
			{
				Refresh();
			};
			MergedMergeEditor.IsReadOnly = false;
			MergedMergeEditor.Document.Changing += Document_Changing;
			WeakEventManager<NotificationCenter, EventArgs<double>>.AddHandler(NotificationCenter.Current, "CodeEditorFontSizeChanged", delegate
			{
				RefreshCodeEditorFontSize(ForkPlusSettings.Default.CodeEditorFontSize);
			});
			RefreshCodeEditorFontSize(ForkPlusSettings.Default.CodeEditorFontSize);
		}

		private void Document_Changing(object sender, DocumentChangeEventArgs e)
		{
			if (_refreshInProgress)
			{
				return;
			}
			MergeConflictView mergeConflictView = MergedMergeEditor.MergeConflictView;
			_ = MergedMergeEditor.TextArea.Caret.Location;
			bool containsNewLines = false;
			Range range = new Range(e.Offset, e.Offset + e.RemovalLength);
			if (mergeConflictView.RangeContainsAlignmentLines(range))
			{
				SystemSounds.Beep.Play();
			}
			else if (MergedMergeEditor.Text.Substring(e.Offset, e.RemovalLength) != mergeConflictView.StringValue.Substring(e.Offset, e.RemovalLength))
			{
				Log.Error("Recognized a merge content mismatch. Refresh merge editor content with the latest valid data");
				containsNewLines = true;
			}
			else
			{
				mergeConflictView.RemoveRange(range);
				mergeConflictView = MergeConflictView.Create(_mergeConflict, MergeConflictPart.Merged, formatting: true);
				mergeConflictView.Insert(e.Offset, e.InsertedText.Text);
				containsNewLines = e.InsertedText.Text.Contains("\n") || e.RemovedText.Text.Contains("\n");
			}
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				int offset = MergedMergeEditor.TextArea.Caret.Offset;
				RefreshMergeEditorViews(containsNewLines);
				if (offset <= MergedMergeEditor.Text.Length)
				{
					MergedMergeEditor.TextArea.Caret.Offset = offset;
				}
			});
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.SideBySideMergeWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetWindowLocationState(ForkPlusSettings.Default.SideBySideMergeWindowLocationState);
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.SideBySideMergeWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnSubmit()
		{
			if (_mergeMode == MergeMode.Text)
			{
				string stringValue = MergeConflictView.Create(_mergeConflict, MergeConflictPart.Merged, formatting: false).StringValue;
				GitCommandResult gitResult = new ResolveMergeConflictGitCommand().Execute(_gitModule, _changedFile, stringValue);
				Close(gitResult);
			}
			else if (_mergeMode == MergeMode.Binary)
			{
				if (AllLocalCheckBox.IsChecked.GetValueOrDefault())
				{
					GitCommandResult gitResult2 = ((!(_changedFile is SubmoduleChangedFile changedFile) || !(_fileContent is SubmoduleDiffContent submoduleDiffContent)) ? new ResolveConflictGitCommand().Execute(_gitModule, _changedFile, UnmergedFileVersionType.Local) : new ResolveConflictGitCommand().Execute(_gitModule, changedFile, submoduleDiffContent.DstSha));
					Close(gitResult2);
				}
				else if (AllRemoteCheckBox.IsChecked.GetValueOrDefault())
				{
					GitCommandResult gitResult3 = ((!(_changedFile is SubmoduleChangedFile changedFile2) || !(_fileContent is SubmoduleDiffContent submoduleDiffContent2)) ? new ResolveConflictGitCommand().Execute(_gitModule, _changedFile, UnmergedFileVersionType.Remote) : new ResolveConflictGitCommand().Execute(_gitModule, changedFile2, submoduleDiffContent2.SrcSha));
					Close(gitResult3);
				}
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			ForkPlusSettings.Default.Save();
			base.OnClosed(e);
		}

		private void RefreshCodeEditorFontSize(double codeEditorFontSize)
		{
			RemoteMergeEditor.FontSize = codeEditorFontSize;
			LocalMergeEditor.FontSize = codeEditorFontSize;
			MergedMergeEditor.FontSize = codeEditorFontSize;
		}

		private void SelectAll(bool select, MergeConflictPart origin)
		{
			if (_stopCheckBoxEvents)
			{
				return;
			}
			MergeConflictView mergeConflictView = origin switch
			{
				MergeConflictPart.Merged => throw new InvalidOperationException(), 
				MergeConflictPart.Local => LocalMergeEditor.MergeConflictView, 
				_ => RemoteMergeEditor.MergeConflictView, 
			};
			if (mergeConflictView == null)
			{
				return;
			}
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (chunk.Node is MergeConflict.ConflictChunk)
				{
					if (select)
					{
						DeselectAll(chunk, MergeConflictPart.Merged);
						SelectAll(chunk, origin);
					}
					else
					{
						DeselectAll(chunk, origin);
					}
				}
			}
			RemoteMergeEditor.InvalidateMargin();
			LocalMergeEditor.InvalidateMargin();
			RefreshCheckboxes();
			RefreshMergeEditorViews(refreshUI: true);
			UpdateSubmitButton();
		}

		private static void DeselectAll(MergeConflictView.Chunk chunk, MergeConflictPart origin)
		{
			chunk.DeselectAllLines();
		}

		private static void SelectAll(MergeConflictView.Chunk chunk, MergeConflictPart origin)
		{
			chunk.SelectAllLines();
		}

		private void OnScrollOffsetChanged(MergeCodeEditor editor)
		{
			if (DateTime.Now - _lastLastScrollTime < TimeSpan.FromMilliseconds(100.0) && editor != _lastUpdatedEditor)
			{
				return;
			}
			double verticalOffset = editor.TextArea.TextView.VerticalOffset;
			double horizontalOffset = editor.TextArea.TextView.HorizontalOffset;
			if (editor.IsVerticalOffsetWithinDocumentArea(verticalOffset))
			{
				if (editor != RemoteMergeEditor)
				{
					ScrollToVerticalOffset(RemoteMergeEditor, verticalOffset);
				}
				if (editor != LocalMergeEditor)
				{
					ScrollToVerticalOffset(LocalMergeEditor, verticalOffset);
				}
				if (editor != MergedMergeEditor)
				{
					ScrollToVerticalOffset(MergedMergeEditor, verticalOffset);
				}
			}
			if (editor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset))
			{
				if (editor != RemoteMergeEditor)
				{
					ScrollToHorizontalOffset(RemoteMergeEditor, horizontalOffset);
				}
				if (editor != LocalMergeEditor)
				{
					ScrollToHorizontalOffset(LocalMergeEditor, horizontalOffset);
				}
				if (editor != MergedMergeEditor)
				{
					ScrollToHorizontalOffset(MergedMergeEditor, horizontalOffset);
				}
			}
			_lastLastScrollTime = DateTime.Now;
			_lastUpdatedEditor = editor;
		}

		private static void ScrollToVerticalOffset(MergeCodeEditor editor, double offset)
		{
			if (editor.IsVerticalOffsetWithinDocumentArea(offset))
			{
				editor.ScrollToVerticalOffset(offset);
			}
		}

		private static void ScrollToHorizontalOffset(MergeCodeEditor editor, double offset)
		{
			if (editor.IsHorizontalOffsetWithinDocumentArea(offset))
			{
				editor.ScrollToHorizontalOffset(offset);
			}
		}

		private void AllRemoteCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (sender != AllRemoteCheckBox)
			{
				return;
			}
			if (_mergeMode == MergeMode.Text)
			{
				SelectAll(AllRemoteCheckBox.IsChecked.GetValueOrDefault(), MergeConflictPart.Remote);
			}
			else if (_mergeMode == MergeMode.Binary)
			{
				if (AllRemoteCheckBox.IsChecked.GetValueOrDefault())
				{
					AllLocalCheckBox.IsChecked = false;
				}
				UpdateSubmitButton();
			}
		}

		private void AllLocalCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (sender != AllLocalCheckBox)
			{
				return;
			}
			if (_mergeMode == MergeMode.Text)
			{
				SelectAll(AllLocalCheckBox.IsChecked.GetValueOrDefault(), MergeConflictPart.Local);
			}
			else if (_mergeMode == MergeMode.Binary)
			{
				if (AllLocalCheckBox.IsChecked.GetValueOrDefault())
				{
					AllRemoteCheckBox.IsChecked = false;
				}
				UpdateSubmitButton();
			}
		}

		private void MergeEditor_MergeLineAdded(object sender, EventArgs<int> args)
		{
			if (!(sender is MergeCodeEditor { MergeConflictView: { } mergeConflictView }))
			{
				return;
			}
			int value = args.Value;
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.LineRange.Contains(value))
				{
					continue;
				}
				MergeConflictView.Line[] lines = chunk.Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (line.LineNumber == value)
					{
						if (line.Node is MergeConflict.SelectableLine selectableLine)
						{
							selectableLine.Select();
							RefreshMergeEditorViews(refreshUI: true);
							UpdateSubmitButton();
							RefreshCheckboxes();
						}
						return;
					}
				}
			}
		}

		private void MergeEditor_MergeLineRemoved(object sender, EventArgs<int> args)
		{
			if (!(sender is MergeCodeEditor { MergeConflictView: { } mergeConflictView }))
			{
				return;
			}
			int value = args.Value;
			MergeConflictView.Chunk[] chunks = mergeConflictView.Chunks;
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (!chunk.LineRange.Contains(value))
				{
					continue;
				}
				MergeConflictView.Line[] lines = chunk.Lines;
				foreach (MergeConflictView.Line line in lines)
				{
					if (line.LineNumber == value)
					{
						if (line.Node is MergeConflict.SelectableLine selectableLine)
						{
							selectableLine.Deselect();
							RefreshMergeEditorViews(refreshUI: true);
							UpdateSubmitButton();
							RefreshCheckboxes();
						}
						return;
					}
				}
			}
		}

		private void MergeEditor_MergeChunkAdded(object sender, EventArgs<MergeConflictView.Chunk> e)
		{
			MergeCodeEditor mergeCodeEditor = sender as MergeCodeEditor;
			DeselectAll(e.Value, mergeCodeEditor.ViewMode);
			SelectAll(e.Value, mergeCodeEditor.ViewMode);
			mergeCodeEditor.InvalidateMargin();
			RefreshMergeEditorViews(refreshUI: true);
			UpdateSubmitButton();
			RefreshCheckboxes();
		}

		private void MergeEditor_MergeChunkRemoved(object sender, EventArgs<MergeConflictView.Chunk> e)
		{
			MergeCodeEditor mergeCodeEditor = sender as MergeCodeEditor;
			DeselectAll(e.Value, mergeCodeEditor.ViewMode);
			mergeCodeEditor.InvalidateMargin();
			RefreshMergeEditorViews(refreshUI: true);
			UpdateSubmitButton();
			RefreshCheckboxes();
		}

		private void Refresh()
		{
			RefreshTopCheckBoxButtons();
			RefreshHeader(_changedFile);
			MergeCodeEditorContainer.Collapse();
			FileMergeControlContainer.Collapse();
			NextPrevMergeButtonsContainer.Collapse();
			AllRemoteCheckBox.Disable();
			AllLocalCheckBox.Disable();
			ResolvedTextBlock.Collapse();
			LayoutOrientationToggleButton.Collapse();
			GitCommandResult<DiffContent> gitCommandResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(_gitModule, _changedFile, null, 3, _gitModule.Settings.TabWidth, ignoreWhitespaces: false, showEntireFile: true, loadLargeUntrackedFiles: false, resolvedConflict: false);
			if (!gitCommandResult.Succeeded)
			{
				Log.Error($"Cannot read merge conflict changes {gitCommandResult.Error}");
				return;
			}
			if (gitCommandResult.Result is UnmergedDiffContent unmergedDiffContent)
			{
				if (unmergedDiffContent.FileType == UnmergedDiffContent.ContentType.Text)
				{
					bool noNewLineAtEndOfFile = NoNewLineAtEndOfFile(_gitModule.MakePath(_changedFile.Path));
					if (!_mergeConflictParser.TryParse(_changedFile.Path, unmergedDiffContent.DiffString, noNewLineAtEndOfFile, out var result))
					{
						Log.Error("Cannot parse merge for '" + _changedFile.Path + "'");
						return;
					}
					_mergeMode = MergeMode.Text;
					MergeCodeEditorContainer.Show();
					NextPrevMergeButtonsContainer.Show();
					ResolvedTextBlock.Show();
					LayoutOrientationToggleButton.Show();
					MergerLayoutOrientation mergerLayoutOrientation = ForkPlusSettings.Default.MergerLayoutOrientation;
					LayoutOrientationToggleButton.IsChecked = mergerLayoutOrientation == MergerLayoutOrientation.Vertical;
					UpdateLayoutOrientation(mergerLayoutOrientation);
					_mergeConflict = result;
					RefreshMergeEditorViews(refreshUI: true);
					SelectFirstConflictedChunk();
				}
				else
				{
					_mergeMode = MergeMode.Binary;
					FileMergeControlContainer.Show();
					FileMergeControl.Content = gitCommandResult;
					_fileContent = gitCommandResult.Result;
				}
			}
			AllRemoteCheckBox.Enable();
			AllLocalCheckBox.Enable();
		}

		private void RefreshHeader(ChangedFile changedFile)
		{
			FileIcon.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(changedFile.Path));
			FilePathTextBlock.FilePath = changedFile.Path;
		}

		private void RefreshTopCheckBoxButtons()
		{
			IGitPoint remoteGitPoint = MergeConflictRepositoryStateHelper.GetRemoteGitPoint(_repositoryState);
			IGitPoint localGitPoint = MergeConflictRepositoryStateHelper.GetLocalGitPoint(_repositoryState);
			RemoteGitPointView.Value = remoteGitPoint;
			LocalGitPointView.Value = localGitPoint;
			if (remoteGitPoint.FriendlyName == MergeConflictRepositoryStateHelper.Stash)
			{
				StashTextBlock.Show();
			}
			else
			{
				StashTextBlock.Hide();
			}
			Task.Run(delegate
			{
				RevisionDetails localRevision = GetRevisionDetails(_gitModule, GetSha(localGitPoint), new JobMonitor());
				RevisionDetails remoteRevision = GetRevisionDetails(_gitModule, (_repositoryState as RepositoryState.RebaseInProgress)?.ActiveSha, new JobMonitor());
				base.Dispatcher.Invoke(delegate
				{
					UpdateTopCheckBoxButtonControls(localRevision, LocalSubjectTextBlock, LocalAuthorAvatarImage, LocalAuthorDateTextBlock, StashTextBlock);
					UpdateTopCheckBoxButtonControls(remoteRevision, RemoteSubjectTextBlock, RemoteAuthorAvatarImage, RemoteAuthorDateTextBlock, StashTextBlock);
				});
			});
		}

		private static Sha? GetSha(IGitPoint gitPoint)
		{
			if (gitPoint is ForkPlus.Git.Reference reference)
			{
				return reference.Sha;
			}
			return null;
		}

		[Null]
		private static RevisionDetails GetRevisionDetails(GitModule gitModule, Sha? sha, JobMonitor monitor)
		{
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				GitCommandResult<RevisionDetails> gitCommandResult = new GetRevisionDetailsGitCommand().Execute(gitModule, valueOrDefault, monitor);
				if (gitCommandResult.Succeeded)
				{
					return gitCommandResult.Result;
				}
				return null;
			}
			return null;
		}

		private static void UpdateTopCheckBoxButtonControls([Null] RevisionDetails revision, TextBlock subjectTextBlock, AvatarImage avatarImage, TextBlock authorDateTextBlock, TextBlock stashTextBlock)
		{
			if (revision != null)
			{
				avatarImage.UserIdentity = revision.Author;
				avatarImage.ToolTip = revision.Author.Name;
				revision.MessageParts(out var subject, out var _);
				subjectTextBlock.Text = subject;
				subjectTextBlock.ToolTip = revision.Message;
				authorDateTextBlock.Text = revision.AuthorDate.ToString(Consts.NormalDateTimeFormat);
				avatarImage.Show();
				subjectTextBlock.Show();
				authorDateTextBlock.Show();
			}
			else
			{
				avatarImage.Hide();
				subjectTextBlock.Hide();
				authorDateTextBlock.Hide();
				stashTextBlock.Hide();
			}
		}

		private void RefreshMergeEditorViews(bool refreshUI)
		{
			RemoteMergeEditor.SetMergeConflictView(MergeConflictView.Create(_mergeConflict, MergeConflictPart.Remote, formatting: true), refreshUI);
			LocalMergeEditor.SetMergeConflictView(MergeConflictView.Create(_mergeConflict, MergeConflictPart.Local, formatting: true), refreshUI, showScrollbarMap: true);
			RefreshMergedView();
			RefreshMergeStatusControls();
		}

		private void RefreshMergedView()
		{
			_refreshInProgress = true;
			MergedMergeEditor.SetMergeConflictView(MergeConflictView.Create(_mergeConflict, MergeConflictPart.Merged, formatting: true), refreshUI: true);
			_refreshInProgress = false;
		}

		private void RefreshMergeStatusControls()
		{
			ConflictStatus conflictStatus = _mergeConflict.ConflictStatus;
			int resolved = conflictStatus.Resolved;
			int total = conflictStatus.Total;
			MergeStatusTextBlock.Text = $"{resolved}/{total}";
			if (total == resolved)
			{
				MergeStatusBorder.Background = Theme.MergeStatusLabelBrushGreen;
				MergeStatusBorder.ToolTip = PreferencesLocalization.Current("All conflicts resolved");
			}
			else
			{
				MergeStatusBorder.Background = Theme.MergeStatusLabelBrushRed;
				_ = 1;
				MergeStatusBorder.ToolTip = PreferencesLocalization.FormatCurrent("Resolved {0} conflicts of {1}", resolved, total);
			}
		}

		private void RefreshCheckboxes()
		{
			if (_stopCheckBoxEvents)
			{
				return;
			}
			MergeConflict mergeConflict = _mergeConflict;
			if (mergeConflict == null)
			{
				return;
			}
			_stopCheckBoxEvents = true;
			bool flag = true;
			bool flag2 = true;
			bool flag3 = false;
			bool flag4 = false;
			MergeConflict.Chunk[] chunks = mergeConflict.Chunks;
			for (int i = 0; i < chunks.Length; i++)
			{
				if (!(chunks[i] is MergeConflict.ConflictChunk { RemoteLines: var remoteLines } conflictChunk))
				{
					continue;
				}
				for (int j = 0; j < remoteLines.Length; j++)
				{
					if (remoteLines[j].IsSelected)
					{
						flag3 = true;
					}
					else
					{
						flag = false;
					}
				}
				MergeConflict.SelectableLine[] localLines = conflictChunk.LocalLines;
				for (int j = 0; j < localLines.Length; j++)
				{
					if (localLines[j].IsSelected)
					{
						flag4 = true;
					}
					else
					{
						flag2 = false;
					}
				}
			}
			AllRemoteCheckBox.IsChecked = flag && !flag4;
			AllLocalCheckBox.IsChecked = flag2 && !flag3;
			_stopCheckBoxEvents = false;
		}

		private void SelectFirstConflictedChunk()
		{
			MergeConflictView.Chunk chunk = IReadOnlyListExtensions.FirstItem(LocalMergeEditor.MergeConflictView?.Chunks, (MergeConflictView.Chunk x) => x.Node is MergeConflict.ConflictChunk);
			if (chunk != null)
			{
				ScrollChunkIntoView(LocalMergeEditor, chunk);
			}
		}

		private void NextChunkButton_Click(object sender, RoutedEventArgs e)
		{
			Range? range = MiddleLine(LocalMergeEditor);
			if (!range.HasValue)
			{
				return;
			}
			Range valueOrDefault = range.GetValueOrDefault();
			MergeConflictView.Chunk[] array = LocalMergeEditor.MergeConflictView?.Chunks;
			if (array != null)
			{
				MergeConflictView.Chunk chunk = FindNextChunk(array, valueOrDefault);
				if (chunk != null)
				{
					ScrollChunkIntoView(LocalMergeEditor, chunk);
				}
			}
		}

		private void PreviousChunkButton_Click(object sender, RoutedEventArgs e)
		{
			Range? range = MiddleLine(LocalMergeEditor);
			if (!range.HasValue)
			{
				return;
			}
			Range valueOrDefault = range.GetValueOrDefault();
			MergeConflictView.Chunk[] array = LocalMergeEditor.MergeConflictView?.Chunks;
			if (array != null)
			{
				MergeConflictView.Chunk chunk = FindPreviousChunk(array, valueOrDefault);
				if (chunk != null)
				{
					ScrollChunkIntoView(LocalMergeEditor, chunk);
				}
			}
		}

		private void LayoutOrientationToggleButton_Changed(object sender, RoutedEventArgs e)
		{
			MergerLayoutOrientation mergerLayoutOrientation = (LayoutOrientationToggleButton.IsChecked.GetValueOrDefault() ? MergerLayoutOrientation.Vertical : MergerLayoutOrientation.Horizontal);
			LayoutOrientationToggleButtonImage.Source = (LayoutOrientationToggleButton.IsChecked.GetValueOrDefault() ? Theme.VerticalMergerIcon : Theme.HorizontalMergerIcon);
			UpdateLayoutOrientation(mergerLayoutOrientation);
			ForkPlusSettings.Default.MergerLayoutOrientation = mergerLayoutOrientation;
		}

		private void UpdateLayoutOrientation(MergerLayoutOrientation orientation)
		{
			switch (orientation)
			{
			case MergerLayoutOrientation.Vertical:
				Grid.SetColumn(MergedMergeEditor, 1);
				Grid.SetColumnSpan(MergedMergeEditor, 1);
				Grid.SetRow(MergedMergeEditor, 0);
				Grid.SetRowSpan(MergedMergeEditor, 2);
				Grid.SetRowSpan(MergedVerticalBorder, 2);
				Grid.SetRowSpan(RemoteMergeEditor, 2);
				Grid.SetRowSpan(LocalMergeEditor, 2);
				MergedMergeEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
				MergedHorizontalSeparator.Collapse();
				ResolvedTextBlock.Show();
				MergedVerticalBorder.BorderThickness = new Thickness(1.0, 0.0, 1.0, 0.0);
				MergeCodeEditorMiddleColumn.Width = new GridLength(1.0, GridUnitType.Star);
				TopCheckBoxMiddleColumn.Width = new GridLength(1.0, GridUnitType.Star);
				RemoteMergeEditor.BorderThickness = new Thickness(0.0, 0.0, 0.0, 0.0);
				break;
			case MergerLayoutOrientation.Horizontal:
				Grid.SetColumn(MergedMergeEditor, 0);
				Grid.SetColumnSpan(MergedMergeEditor, 3);
				Grid.SetRow(MergedMergeEditor, 1);
				Grid.SetRowSpan(MergedMergeEditor, 1);
				Grid.SetRowSpan(MergedVerticalBorder, 1);
				Grid.SetRowSpan(RemoteMergeEditor, 1);
				Grid.SetRowSpan(LocalMergeEditor, 1);
				MergedMergeEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
				MergedHorizontalSeparator.Show();
				ResolvedTextBlock.Collapse();
				MergedVerticalBorder.BorderThickness = new Thickness(0.0, 0.0, 1.0, 0.0);
				MergeCodeEditorMiddleColumn.Width = new GridLength(1.0);
				TopCheckBoxMiddleColumn.Width = new GridLength(0.0);
				RemoteMergeEditor.BorderThickness = new Thickness(1.0, 0.0, 0.0, 0.0);
				break;
			}
		}

		private MergeConflictView.Chunk FindPreviousChunk(MergeConflictView.Chunk[] chunks, Range middleLineRange)
		{
			for (int num = chunks.Length - 1; num >= 0; num--)
			{
				MergeConflictView.Chunk chunk = chunks[num];
				if (chunk.Node is MergeConflict.ConflictChunk && chunk.Range.End <= middleLineRange.Start)
				{
					return chunk;
				}
			}
			return null;
		}

		private MergeConflictView.Chunk FindNextChunk(MergeConflictView.Chunk[] chunks, Range middleLineRange)
		{
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (chunk.Node is MergeConflict.ConflictChunk && chunk.Range.Start > middleLineRange.End)
				{
					return chunk;
				}
			}
			return null;
		}

		private void ScrollChunkIntoView(MergeCodeEditor editor, MergeConflictView.Chunk chunk)
		{
			if (chunk.Lines.Length != 0)
			{
				int lineNumber = chunk.Lines[0].LineNumber;
				editor.ScrollToLine(lineNumber + 1);
			}
		}

		private static Range? MiddleLine(CodeEditor editor)
		{
			TextView textView = editor.TextArea.TextView;
			if (editor.Document == null || textView.ActualHeight <= 0.0)
			{
				return null;
			}
			double middleVisualTop = editor.GetScrollPosition() + textView.ActualHeight / 2.0;
			DocumentLine documentLineByVisualTop = textView.GetDocumentLineByVisualTop(middleVisualTop);
			if (documentLineByVisualTop == null)
			{
				return null;
			}
			return new Range(documentLineByVisualTop.Offset, documentLineByVisualTop.EndOffset);
		}

		private static bool NoNewLineAtEndOfFile(string path)
		{
			try
			{
				string text = File.ReadAllText(path);
				if (text.Length > 0)
				{
					return text[text.Length - 1] != '\n';
				}
			}
			catch (Exception arg)
			{
				Log.Error($"Cannot read '{path}': {arg}");
			}
			return false;
		}

	}
}
