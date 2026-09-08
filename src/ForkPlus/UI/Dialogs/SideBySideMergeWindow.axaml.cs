using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup;
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
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

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

		// Migration note：Avalonia NameGenerator 只为 StyledElement 生成 x:Name 字段，
		// ColumnDefinition 非 StyledElement，手动声明（构造函数里按结构索引赋值）。
		internal global::Avalonia.Controls.ColumnDefinition MergeCodeEditorMiddleColumn;

		internal global::Avalonia.Controls.ColumnDefinition TopCheckBoxMiddleColumn;

		private MergeConflict _mergeConflict;

		private bool _stopCheckBoxEvents;

		private bool _startUpFinished;

		private MergeMode _mergeMode;

		private DiffContent _fileContent;

		// 修复（2026-09-05，"点击横向滚动条界面弹动"）：
		// 垂直/水平滚动分别防抖；同步前检查差值，避免联动循环。
		private DateTime _lastVerticalScrollTime;
		private DateTime _lastHorizontalScrollTime;
		private MergeCodeEditor _lastVerticalEditor;
		private MergeCodeEditor _lastHorizontalEditor;

		// 修复（2026-09-09，"merge 弹窗一打开就卡住→软件 UI 崩溃"根因）：
		// 三个编辑器各自的 MergeConflictView 行数不同（local/remote/merged 视图含各自的
		// 对齐行与冲突行）→ TextView Extent（滚动范围）不同 → 纯事件驱动的滚动同步在
		// 钳制边界互相拉扯：A 滚到 X → 同步 B/C → B 的 ScrollViewer 把 X 钳到自己的
		// max（≠X）→ B 的 ScrollOffsetChanged 再同步 A/C → A 再钳 → …… 100ms 防抖
		// 每到期一轮放行一轮，3 编辑器全量重排 ~10 次/秒；大文件/高 DPI 下布局永远
		// 追不上 → UI 永久卡死（Windows 上即"打开即卡住 → 崩溃"）。
		// 三重防线：
		// 1) 回声断路器 _scrollSyncInProgress：同步写入引发的连锁 ScrollOffsetChanged
		//    直接忽略（Offset 赋值后 TextView 于布局期回调，Background 优先级低于布局，
		//    清旗动作排在布局回调之后，回声必被旗拦下）；
		// 2) 熔断器：2s 窗口内同步超过 40 次（任何残余风暴）→ 暂停联动 5s，UI 永不卡死；
		// 3) 初始定位与前后冲突块导航不再依赖事件链：按 Chunk.Node 匹配，
		//    直接把三个编辑器各自滚到同一冲突块。
		private bool _scrollSyncInProgress;
		private DateTime _syncBurstWindowStart = DateTime.MinValue;
		private int _syncBurstCount;
		private DateTime _syncSuspendedUntil = DateTime.MinValue;

		/// <summary>同步写入期间的回声断路：设旗 + 熔断计数 + 布局期之后延迟清旗。</summary>
		private void ArmScrollSyncGuard()
		{
			DateTime now = DateTime.Now;
			// 熔断计数（2s 滚动窗口）
			if (now - _syncBurstWindowStart > TimeSpan.FromSeconds(2.0))
			{
				_syncBurstWindowStart = now;
				_syncBurstCount = 0;
			}
			if (++_syncBurstCount > 40)
			{
				// 风暴：挂起联动 5s（断路器失效时的兜底，保证永不因同步卡死）
				_syncSuspendedUntil = now + TimeSpan.FromSeconds(5.0);
				_scrollSyncInProgress = false;
				return;
			}
			_scrollSyncInProgress = true;
			// ScrollViewer.Offset 赋值后，TextView 的 ScrollOffsetChanged 在布局期触发；
			// Background 优先级低于布局回调，清旗排在其后 → 同步引发的回声全部被旗拦下。
			Dispatcher.UIThread.Post(delegate
			{
				_scrollSyncInProgress = false;
			}, global::Avalonia.Threading.DispatcherPriority.Background);
		}

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
			// Migration note：Avalonia NameGenerator 只为 StyledElement 生成 x:Name 字段，
			// ColumnDefinition 非 StyledElement，按 XAML 结构索引取列定义。
			MergeCodeEditorMiddleColumn = ((global::Avalonia.Controls.Grid)MergeCodeEditorContainer.Child).ColumnDefinitions[1];
			TopCheckBoxMiddleColumn = ((global::Avalonia.Controls.Grid)((global::Avalonia.Controls.DockPanel)AllRemoteCheckBox.Parent).Parent).ColumnDefinitions[1];
			base.SubmitButtonTitle = PreferencesLocalization.Current("Resolve");
			FileMergeControl.RepositoryUserControl = repositoryUserControl;
			LocalMergeEditor.ViewMode = MergeConflictPart.Local;
			RemoteMergeEditor.ViewMode = MergeConflictPart.Remote;
			MergedMergeEditor.ViewMode = MergeConflictPart.Merged;
			RemoteMergeEditor.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
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
			WeakEventManager<NotificationCenter, EventArgs<double>>.AddHandler(NotificationCenter.Current,"CodeEditorFontSizeChanged",delegate
(object sender, global::System.EventArgs e)			{
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
			base.Dispatcher.Post((Action)delegate
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

		// Migration note（根因）：WPF OnSourceInitialized 在 Avalonia 无对应生命周期（原为死代码，
		// 窗口位置从未恢复）。改 OnOpened override（CustomWindow 已提供 OnOpened 虚链）。
		protected override void OnOpened(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetWindowLocationState(ForkPlusSettings.Default.SideBySideMergeWindowLocationState);
		}

		// Migration note（根因）：原为非 override 的隐藏方法，CustomWindow.PositionChanged 的
		// 虚分发永远到不了这里（移动窗口保存位置从未执行）。改 override 接入分发链。
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
			// v3.9.2：修复按钮无反应——之前 _aiResolving 卡在 true 会导致永远静默 return
			if (_aiResolving)
			{
				// v3.9.2：排队时给用户明确反馈，不再完全没反应
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("AI is still resolving conflicts. Please wait for the current task to finish."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false).ShowDialog();
				return;
			}
			if (_mergeMode != MergeMode.Text || _mergeConflict == null)
			{
				// v3.9.2：状态未就绪也给反馈
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("Cannot resolve conflicts: file or repository is not ready."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
			// v3.8.3：async void 方法必须捕获所有异常，否则未处理异常会导致应用闪退
			try
			{
				await AiResolveButton_ClickCore();
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve crashed: " + ex);
				_aiResolving = false;
				AiResolveButton.SetBusy(false);
			}
		}

		private async Task AiResolveButton_ClickCore()
		{
			if (!OpenAiService.IsAiReviewConfigured())
			{
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI is not configured. Please configure AI review settings in Preferences first."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
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
				// v3.9.2：路径解析失败不再静默，给用户反馈
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.FormatCurrent("Failed to resolve file path: {0}", ex.Message),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
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
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.FormatCurrent("Failed to read conflict file: {0}", ex.Message),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
			}

			if (string.IsNullOrEmpty(conflictedContent)
				|| !conflictedContent.Contains("<<<<<<<") || !conflictedContent.Contains(">>>>>>>"))
			{
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("No conflict markers found in the file."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false).ShowDialog();
			return;
			}

			_aiResolving = true;
			// v3.9.0：统一用 AiActionButton.SetBusy 管理 Loading 态
			AiResolveButton.SetBusy(true, PreferencesLocalization.Current("AI is resolving conflicts..."));

			string fileName = Path.GetFileName(_changedFile.Path);
			string prompt = OpenAiService.BuildResolveConflictsPrompt(fileName, conflictedContent);

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
						requestError = new Exception(result.Error?.FriendlyMessage ?? PreferencesLocalization.Current("Unknown error"));
					}
				}
				catch (Exception ex)
				{
					requestError = ex;
				}
			}).ConfigureAwait(true);

			// v3.8.3：await 后窗口可能已关闭，访问已关闭窗口的 UI 元素会崩溃
			if (!IsLoaded)
			{
				_aiResolving = false;
				return;
			}
			AiResolveButton.SetBusy(false);
			_aiResolving = false;

			if (canceled)
			{
				return;
			}
			if (requestError != null)
			{
				Log.Error("AI Resolve failed: " + requestError.Message);
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.FormatCurrent("AI resolve failed: {0}", requestError.Message),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
			}

			string resolved;
			lock (responseBuilder)
			{
				resolved = responseBuilder.ToString();
			}
			resolved = OpenAiService.StripCodeFences(resolved);
			if (string.IsNullOrWhiteSpace(resolved))
			{
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI returned empty content. Aborting."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
			}

			// 残留冲突标记检测：若 AI 输出仍包含冲突标记，说明没解决干净，提示用户
			if (resolved.Contains("<<<<<<<") || resolved.Contains(">>>>>>>") || resolved.Contains("======="))
			{
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI output still contains conflict markers. Please review and try again, or resolve manually."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
			}

			if (!new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI resolved all conflicts. Apply the resolved content and close?"),
				PreferencesLocalization.Current("Apply"),
				PreferencesLocalization.Current("Cancel"),
				showCancelButton: true,
				550.0).ShowDialog().GetValueOrDefault())
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
			new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.FormatCurrent("Failed to apply resolved content: {0}", ex.Message),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			}
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
			// 防线 1：回声断路器——同步写入引发的连锁回调（布局期到达）直接忽略
			if (_scrollSyncInProgress)
			{
				return;
			}
			// 防线 2：熔断挂起期间不联动
			if (DateTime.Now < _syncSuspendedUntil)
			{
				return;
			}
			double verticalOffset = editor.TextArea.TextView.ScrollOffset.Y;
			double horizontalOffset = editor.TextArea.TextView.ScrollOffset.X;
			bool wroteToPeers = false;

			// ── 垂直滚动同步 ──
			if (editor.IsVerticalOffsetWithinDocumentArea(verticalOffset))
			{
				if (!(DateTime.Now - _lastVerticalScrollTime < TimeSpan.FromMilliseconds(100.0)
					&& editor != _lastVerticalEditor))
				{
					const double vTolerance = 0.5;
					bool synced = false;
					if (editor != RemoteMergeEditor
						&& RemoteMergeEditor.IsVerticalOffsetWithinDocumentArea(verticalOffset)
						&& Math.Abs(RemoteMergeEditor.TextArea.TextView.ScrollOffset.Y - verticalOffset) > vTolerance)
					{
						ScrollToVerticalOffset(RemoteMergeEditor, verticalOffset);
						synced = true;
					}
					if (editor != LocalMergeEditor
						&& LocalMergeEditor.IsVerticalOffsetWithinDocumentArea(verticalOffset)
						&& Math.Abs(LocalMergeEditor.TextArea.TextView.ScrollOffset.Y - verticalOffset) > vTolerance)
					{
						ScrollToVerticalOffset(LocalMergeEditor, verticalOffset);
						synced = true;
					}
					if (editor != MergedMergeEditor
						&& MergedMergeEditor.IsVerticalOffsetWithinDocumentArea(verticalOffset)
						&& Math.Abs(MergedMergeEditor.TextArea.TextView.ScrollOffset.Y - verticalOffset) > vTolerance)
					{
						ScrollToVerticalOffset(MergedMergeEditor, verticalOffset);
						synced = true;
					}
					if (synced)
					{
						_lastVerticalScrollTime = DateTime.Now;
						_lastVerticalEditor = editor;
						wroteToPeers = true;
					}
				}
			}

			// ── 水平滚动同步 ──
			if (editor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset))
			{
				if (!(DateTime.Now - _lastHorizontalScrollTime < TimeSpan.FromMilliseconds(100.0)
					&& editor != _lastHorizontalEditor))
				{
					const double hTolerance = 0.5;
					bool synced = false;
					if (editor != RemoteMergeEditor
						&& RemoteMergeEditor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset)
						&& Math.Abs(RemoteMergeEditor.TextArea.TextView.ScrollOffset.X - horizontalOffset) > hTolerance)
					{
						ScrollToHorizontalOffset(RemoteMergeEditor, horizontalOffset);
						synced = true;
					}
					if (editor != LocalMergeEditor
						&& LocalMergeEditor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset)
						&& Math.Abs(LocalMergeEditor.TextArea.TextView.ScrollOffset.X - horizontalOffset) > hTolerance)
					{
						ScrollToHorizontalOffset(LocalMergeEditor, horizontalOffset);
						synced = true;
					}
					if (editor != MergedMergeEditor
						&& MergedMergeEditor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset)
						&& Math.Abs(MergedMergeEditor.TextArea.TextView.ScrollOffset.X - horizontalOffset) > hTolerance)
					{
						ScrollToHorizontalOffset(MergedMergeEditor, horizontalOffset);
						synced = true;
					}
					if (synced)
					{
						_lastHorizontalScrollTime = DateTime.Now;
						_lastHorizontalEditor = editor;
						wroteToPeers = true;
					}
				}
			}

			// 本次回调发生了同步写入 → 武装回声断路器（拦截本轮回声）
			if (wroteToPeers)
			{
				ArmScrollSyncGuard();
			}
		}

		private static void ScrollToVerticalOffset(MergeCodeEditor editor, double offset)
		{
			editor.ScrollToVerticalOffsetCompat(offset);
		}

		private static void ScrollToHorizontalOffset(MergeCodeEditor editor, double offset)
		{
			editor.ScrollToHorizontalOffsetCompat(offset);
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
				global::Avalonia.Controls.ToolTip.SetTip(avatarImage,revision.Author.Name);
				revision.MessageParts(out var subject, out var _);
				subjectTextBlock.Text = subject;
				global::Avalonia.Controls.ToolTip.SetTip(subjectTextBlock,revision.Message);
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
				MergeStatusBorder.Background = global::ForkPlus.UI.Theme.MergeStatusLabelBrushGreen;
				global::Avalonia.Controls.ToolTip.SetTip(MergeStatusBorder,PreferencesLocalization.Current("All conflicts resolved"));
			}
			else
			{
				MergeStatusBorder.Background = global::ForkPlus.UI.Theme.MergeStatusLabelBrushRed;
				_ = 1;
				global::Avalonia.Controls.ToolTip.SetTip(MergeStatusBorder,PreferencesLocalization.FormatCurrent("Resolved {0} conflicts of {1}", resolved, total));
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
			LayoutOrientationToggleButtonImage.Source = (LayoutOrientationToggleButton.IsChecked.GetValueOrDefault() ? global::ForkPlus.UI.Theme.VerticalMergerIcon : global::ForkPlus.UI.Theme.HorizontalMergerIcon);
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
				MergedMergeEditor.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
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
				MergedMergeEditor.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
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
			// 守卫放宽（2026-09-09，"合并结果栏从不滚到冲突块、停在文件开头"）：
			// 内容行与对齐行都为空的块才无处可滚。原守卫 Lines.Length==0 会把
			// "只有对齐行的块"也拦掉——Merged 视图的未解决冲突块恰好就是这种
			//（ResultLines 为空），remote-only 冲突在 Local 视图同理。
			if (chunk.Lines.Length != 0 || chunk.AlignmentLines.Length != 0)
			{
				// 防线 3：定位冲突块时直接滚动全部编辑器（按 Chunk.Node 跨视图匹配同一冲突块），
				// 不再依赖事件驱动的滚动同步链——同步链在初始大偏移（SelectFirstConflictedChunk
				// 可达数千 px）+ 三编辑器 Extent 不同的情况下会触发钳制回声（详见 OnScrollOffsetChanged）。
				ArmScrollSyncGuard();
				try
				{
					ScrollEditorChunkIntoView(editor, chunk);
					ScrollEditorChunkIntoView(RemoteMergeEditor, FindChunkByNode(RemoteMergeEditor, chunk.Node));
					ScrollEditorChunkIntoView(MergedMergeEditor, FindChunkByNode(MergedMergeEditor, chunk.Node));
				}
				catch (Exception ex)
				{
					Log.Error("ScrollChunkIntoView failed", ex);
				}
			}
		}

		private static void ScrollEditorChunkIntoView(MergeCodeEditor editor, MergeConflictView.Chunk chunk)
		{
			if (editor == null || chunk == null)
			{
				return;
			}
			// Merged 视图的未解决冲突块没有内容行（ResultLines 为空，块内只有对齐行，
			// 首行为 "--- Merge Conflict ---" 占位）——旧守卫 Lines.Length==0 直接 return，
			// 合并结果栏从不滚动到冲突块、永远停在文件开头（Local/Remote 视图的块带
			// 各自内容行不受影响）。修正：内容行为空时回退用对齐行定位——对齐行行号
			// 与该块在各视图中的行号同步推进（见 MergeConflictView.CreateView 的
			// CreateAlignmentLine），块首对齐行即冲突块首行，三视图滚到同一冲突块。
			int lineNumber;
			if (chunk.Lines.Length != 0)
			{
				lineNumber = chunk.Lines[0].LineNumber;
			}
			else if (chunk.AlignmentLines.Length != 0)
			{
				lineNumber = chunk.AlignmentLines[0].LineNumber;
			}
			else
			{
				return;
			}
			editor.ScrollToLine(lineNumber + 1);
		}

		/// <summary>在编辑器自己的 MergeConflictView 里找与给定 chunk 同一底层节点
		///（MergeConflict.Chunk）的块——三个视图都源自同一 MergeConflict，节点引用一致。</summary>
		private static MergeConflictView.Chunk FindChunkByNode(MergeCodeEditor editor, MergeConflict.Chunk node)
		{
			if (editor?.MergeConflictView?.Chunks == null || node == null)
			{
				return null;
			}
			foreach (MergeConflictView.Chunk chunk in editor.MergeConflictView.Chunks)
			{
				if (ReferenceEquals(chunk.Node, node))
				{
					return chunk;
				}
			}
			return null;
		}

		private static Range? MiddleLine(CodeEditor editor)
		{
			TextView textView = editor.TextArea.TextView;
			if (editor.Document == null || textView.Bounds.Height <= 0.0)
			{
				return null;
			}
			double middleVisualTop = editor.GetScrollPosition() + textView.Bounds.Height / 2.0;
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
