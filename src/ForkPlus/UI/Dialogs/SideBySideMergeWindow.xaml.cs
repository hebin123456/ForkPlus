using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using ForkPlus.Accounts.AiServices;
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
using ForkPlus.Utils.Http;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ForkPlus.UI.Helpers;

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

		// AI 解决冲突进行中标志，避免重复触发
		private bool _aiResolving;

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
			base.SubmitButtonTitle = PreferencesLocalization.Current("Resolve");
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

		/// <summary>
		/// AI 解决合并冲突：读取磁盘上带冲突标记的原始文件内容，发送给 AI，
		/// AI 返回去冲突标记的合并文本，用户确认后通过 ResolveMergeConflictGitCommand 写回。
		/// </summary>
		private async void AiResolveButton_Click(object sender, RoutedEventArgs e)
		{
			if (_aiResolving)
			{
				return;
			}
			if (_mergeMode != MergeMode.Text || _mergeConflict == null)
			{
				return;
			}
			if (!OpenAiService.IsAiReviewConfigured())
			{
				MessageBox.Show(
					PreferencesLocalization.Current("AI is not configured. Please configure AI review settings in Preferences first."),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			string filePath;
			try
			{
				filePath = _gitModule.MakePath(_changedFile.Path);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to resolve file path: " + ex.Message);
				return;
			}

			string conflictedContent;
			try
			{
				conflictedContent = File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to read conflict file: " + ex.Message);
				MessageBox.Show(
					PreferencesLocalization.FormatCurrent("Failed to read conflict file: {0}", ex.Message),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return;
			}

			if (string.IsNullOrEmpty(conflictedContent)
				|| !conflictedContent.Contains("<<<<<<<") || !conflictedContent.Contains(">>>>>>>"))
			{
				MessageBox.Show(
					PreferencesLocalization.Current("No conflict markers found in the file."),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Information);
				return;
			}

			_aiResolving = true;
			AiResolveButton.IsEnabled = false;
			string originalToolTip = AiResolveButton.ToolTip?.ToString();
			AiResolveButton.ToolTip = PreferencesLocalization.Current("AI is resolving conflicts...");

			string fileName = Path.GetFileName(_changedFile.Path);
			string prompt = BuildAiResolvePrompt(fileName, conflictedContent);

			StringBuilder responseBuilder = new StringBuilder();
			Exception requestError = null;
			bool canceled = false;

			await Task.Run(delegate
			{
				try
				{
					OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
					JobMonitor monitor = new JobMonitor();
					ServiceResult<OpenAiResponse> result = aiService.OpenAiRequestStreamingWithRetry(
						prompt,
						monitor,
						delegate(string delta)
						{
							if (string.IsNullOrEmpty(delta))
							{
								return;
							}
							lock (responseBuilder)
							{
								responseBuilder.Append(delta);
							}
						});
					if (monitor.IsCanceled)
					{
						canceled = true;
						return;
					}
					if (!result.Succeeded)
					{
						requestError = new Exception(result.Error?.FriendlyMessage ?? "Unknown error");
					}
				}
				catch (Exception ex)
				{
					requestError = ex;
				}
			}).ConfigureAwait(true);

			AiResolveButton.IsEnabled = true;
			AiResolveButton.ToolTip = originalToolTip ?? "Use AI to resolve all conflicts";
			_aiResolving = false;

			if (canceled)
			{
				return;
			}
			if (requestError != null)
			{
				Log.Error("AI Resolve failed: " + requestError.Message);
				MessageBox.Show(
					PreferencesLocalization.FormatCurrent("AI resolve failed: {0}", requestError.Message),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return;
			}

			string resolved;
			lock (responseBuilder)
			{
				resolved = responseBuilder.ToString();
			}
			resolved = StripCodeFences(resolved);
			if (string.IsNullOrWhiteSpace(resolved))
			{
				MessageBox.Show(
					PreferencesLocalization.Current("AI returned empty content. Aborting."),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// 残留冲突标记检测：若 AI 输出仍包含冲突标记，说明没解决干净，提示用户
			if (resolved.Contains("<<<<<<<") || resolved.Contains(">>>>>>>") || resolved.Contains("======="))
			{
				MessageBox.Show(
					PreferencesLocalization.Current("AI output still contains conflict markers. Please review and try again, or resolve manually."),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			MessageBoxResult confirm = MessageBox.Show(
				PreferencesLocalization.Current("AI resolved all conflicts. Apply the resolved content and close?"),
				PreferencesLocalization.Current("AI Resolve"),
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);
			if (confirm != MessageBoxResult.Yes)
			{
				return;
			}

			try
			{
				GitCommandResult gitResult = new ResolveMergeConflictGitCommand().Execute(_gitModule, _changedFile, resolved);
				Close(gitResult);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to write back: " + ex.Message);
				MessageBox.Show(
					PreferencesLocalization.FormatCurrent("Failed to apply resolved content: {0}", ex.Message),
					PreferencesLocalization.Current("AI Resolve"),
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

		/// <summary>构造 AI 解决冲突的 prompt：要求 AI 合并两侧变更、保留非冲突上下文、不输出解释。</summary>
		private static string BuildAiResolvePrompt(string fileName, string conflictedContent)
		{
			return "You are an expert at resolving Git merge conflicts.\n"
				+ "The file below contains conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`).\n"
				+ "Resolve ALL conflicts intelligently:\n"
				+ "- Combine changes from both sides when they don't overlap.\n"
				+ "- When both sides modify the same region, merge them preserving intent; prefer keeping both sets of changes if compatible.\n"
				+ "- Keep the non-conflicting context intact.\n"
				+ "- Preserve the file's overall structure, imports, and syntax.\n"
				+ "Return ONLY the final merged file content with NO conflict markers, NO explanations, NO markdown code fences, NO surrounding prose.\n"
				+ "Do not wrap the output in ``` code blocks.\n\n"
				+ "File: " + fileName + "\n\n"
				+ conflictedContent;
		}

		/// <summary>剥离 AI 输出可能包含的 markdown 代码围栏（```lang ... ```）。</summary>
		private static string StripCodeFences(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}
			string trimmed = text.Trim();
			if (trimmed.StartsWith("```"))
			{
				int firstNewLine = trimmed.IndexOf('\n');
				if (firstNewLine >= 0)
				{
					trimmed = trimmed.Substring(firstNewLine + 1);
				}
				else
				{
					trimmed = trimmed.Substring(3);
				}
				if (trimmed.EndsWith("```"))
				{
					trimmed = trimmed.Substring(0, trimmed.Length - 3);
				}
				return trimmed;
			}
			return text;
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
			// AI 解决按钮默认隐藏，仅在 Text 模式下显示
			AiResolveButton.Collapse();
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
					// 仅在 Text 模式且 AI 已配置时显示 AI 解决按钮
					if (OpenAiService.IsAiReviewConfigured())
					{
						AiResolveButton.Show();
					}
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
