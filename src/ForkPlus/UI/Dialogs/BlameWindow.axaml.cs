using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class BlameWindow : CustomWindow
	{
		private class UndoManager
		{
			private readonly List<BlameArgs> _items = new List<BlameArgs>();

			private int _currentIndex = -1;

			[Null]
			public BlameArgs CurrentItem
			{
				get
				{
					if (_currentIndex == -1)
					{
						return null;
					}
					return _items[_currentIndex];
				}
			}

			public bool IsUndoPossible => _currentIndex > 0;

			public bool IsRedoPossible => _currentIndex < _items.Count - 1;

			public void Add(BlameArgs newItem)
			{
				BlameArgs currentItem = CurrentItem;
				if (currentItem == null || !(newItem.Sha == currentItem.Sha))
				{
					for (int num = _items.Count - 1; num > _currentIndex; num--)
					{
						_items.RemoveAt(num);
					}
					_items.Add(newItem);
					_currentIndex++;
				}
			}

			public void Undo()
			{
				if (IsUndoPossible)
				{
					_currentIndex--;
				}
			}

			public void Redo()
			{
				if (IsRedoPossible)
				{
					_currentIndex++;
				}
			}
		}

		/// <summary>
		/// blame 块的行号匹配上下文：块头部项 + 块内"当前提交新增行"的新文件行号（1-based）。
		/// git-ai 归属数据异步到达后按这些行号匹配区间，命中即给头部补 AI 徽标（见 ApplyAiAttributions）。
		/// </summary>
		private class BlameBlockContext
		{
			public readonly BlameItemViewModel HeaderItem;

			public readonly List<int> NewFileLineNumbers;

			public BlameBlockContext(BlameItemViewModel headerItem, List<int> newFileLineNumbers)
			{
				HeaderItem = headerItem;
				NewFileLineNumbers = newFileLineNumbers;
			}
		}

		private static readonly Revision DummyRevision = new Revision(Sha.NullSha, new RevisionHeader(new UserIdentity("dummy", "dummy"), DateTimeHelper.UnixStartTime, "dummy", hasBody: false));

		private readonly UndoManager _undoManager = new UndoManager();

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly DelayedAction<BlameArgs> _refreshBlame;

		private RevisionViewModel[] _revisions;

		private RevisionViewModel _selectedRevision;

		private bool _initialized;

		private bool _startUpFinished;

		private ScrollViewer RevisionListScrollViewer
		{
			get
			{
				return ScrollViewerHelper.FindScrollViewer(BlameListBox);
			}
		}

		public BlameWindow(RepositoryUserControl repositoryUserControl, string filePath, Sha? shaToSelect, [Null] ForkPlus.Git.Reference targetReference)
		{
			_repositoryUserControl = repositoryUserControl;
			_refreshBlame = new DelayedAction<BlameArgs>(RefreshBlame);
			base.Title = PathHelper.GetReadableFileName(filePath) + " - " + Translate("Blame");
			base.ShowInTaskbar = true;
			// 修复链 23：删除 CenterScreen（多显示器下居中到主显示器而非主窗口所在屏；打开方用 ShowAtOwnerScreen）。
			ResizeMode = ResizeMode.CanResizeWithGrip;
			InitializeComponent();
			BlameTitleTextBlock.Text = Translate("Blame");
			global::Avalonia.Controls.ToolTip.SetTip(UndoButton,Translate("Go Back"));
			global::Avalonia.Controls.ToolTip.SetTip(RedoButton,Translate("Go Forward"));
			TextDiffControl.FontSize = 14.0;
			TextDiffControl.ScrollOffsetChanged += SplitTextDiffControl_ScrollOffsetChanged;
			TextDiffControl.HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
			RevisionListFallbackBorder.Show();
			RevisionListFallbackUserControl.Show();
			RevisionListFallbackUserControl.FallbackTitle = Translate("Loading...");
			BlameListBox.Hide();
			CodeEditorFallbackUserControl.Show();
			TextDiffControl.Hide();
			FileIcon.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(filePath));
			FileNameTextBlock.FilePath = filePath;
			global::Avalonia.Controls.ToolTip.SetTip(FileNameTextBlock,filePath);
			RefreshUndoControls();
			Initialize(filePath, shaToSelect, targetReference);
			base.SizeChanged += BlameWindow_SizeChanged;
			base.Activated += BlameWindow_Activated;
		}

		// Migration note（根因）：WPF OnSourceInitialized 在 Avalonia 无对应生命周期（原为死代码，
		// 窗口位置从未恢复）。改 OnOpened override（CustomWindow 已提供 OnOpened 虚链）。
		protected override void OnOpened(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetWindowLocationState(ForkPlusSettings.Default.BlameWindowLocationState);
		}

		// Migration note：CustomWindow.OnLocationChanged 为 protected virtual（由 Window.PositionChanged 派发），
		// WPF 原代码漏写 override 导致只是隐藏而非重写，这里补上 override 恢复"移动窗口即保存位置"语义。
		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.BlameWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void BlameWindow_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.BlameWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void BlameWindow_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		private void Initialize(string filePath, Sha? sha, [Null] ForkPlus.Git.Reference targetReference)
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			BusyIndicator.Show();
			new Task(delegate
			{
				GitCommandResult<Sha> shaResult = new GetFirstRevisionGitCommand().Execute(gitModule, filePath, sha);
				if (!shaResult.Succeeded)
				{
					base.Dispatcher.Post(delegate
					{
						ShowErrorFallback(shaResult.Error);
					});
				}
				else
				{
					GitCommandResult<RevisionWithFiles[]> fileHistoryResult = new GetFileHistoryGitCommand().Execute(gitModule, repositoryData.Submodules.Items, filePath, targetReference?.Sha);
					if (!fileHistoryResult.Succeeded)
					{
						base.Dispatcher.Post(delegate
						{
							ShowErrorFallback(fileHistoryResult.Error);
						});
					}
					else
					{
						RevisionViewModel[] revisions = fileHistoryResult.Result.Map((RevisionWithFiles x) => new RevisionViewModel(x));
						base.Dispatcher.Post(delegate
						{
							_revisions = revisions;
							RevisionsComboBox.ItemsSource = _revisions;
							RevisionViewModel revisionViewModel = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == shaResult.Result) ?? _revisions.FirstItem();
							RevisionsComboBox.SelectedItem = revisionViewModel;
							RevisionTimeLine.Revisions = fileHistoryResult.Result;
							RevisionTimeLine.ActiveRevision = revisionViewModel.Sha;
							_selectedRevision = revisionViewModel;
							_initialized = true;
							_refreshBlame.InvokeNow(new BlameArgs(revisionViewModel.Sha, filePath));
						});
					}
				}
			}).Start();
		}

		private void RefreshUndoControls()
		{
			UndoButton.IsEnabled = _undoManager.IsUndoPossible;
			RedoButton.IsEnabled = _undoManager.IsRedoPossible;
		}

		private void RefreshBlame(BlameArgs args)
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			BusyIndicator.Show();
			FileNameTextBlock.FilePath = args.Filepath;
			global::Avalonia.Controls.ToolTip.SetTip(FileNameTextBlock,args.Filepath);
			int tabWidth = gitModule.Settings.TabWidth;
			new Task(delegate
			{
				ChangedFile changedFile = new ChangedFile(PathHelper.NormalizeUnix(args.Filepath), StatusType.Modified);
				GitCommandResult<DiffContent> fileDiffResult = new GetRevisionFileChangesGitCommand().Execute(gitModule, new RevisionDiffTarget.Revision(args.Sha), changedFile, 1, tabWidth, ignoreWhitespaces: false, showEntireFile: true);
				if (!fileDiffResult.Succeeded)
				{
					base.Dispatcher.Post(delegate
					{
						ShowErrorFallback(fileDiffResult.Error);
					});
				}
				else if (!(fileDiffResult.Result is ParsedDiffContent parsedDiffContent) || parsedDiffContent.Diff.Chunks.Length == 0)
				{
					base.Dispatcher.Post(delegate
					{
						ShowErrorFallback(Translate("Blame can only be used for text files"));
					});
				}
				else
				{
					Diff diff = parsedDiffContent.Diff;
				base.Dispatcher.Post(delegate
				{
					CodeEditorFallbackUserControl.Hide();
					TextDiffControl.Show();
					TextDiffControl.SetDiff(diff, tabWidth, entireFile: true, DiffLocation.Revision);
				});
				// git-ai 行级归属与 git blame 并行执行（同为后台线程）：
				// git-ai 首次调用可能冷启动 daemon（秒级耗时，超时上限 15 秒），
				// blame 首屏不等它——归属数据到达后经 ApplyAiAttributions 异步补 AI 徽标。
				Task<List<GitAiLineAttribution>> aiAttributionTask = Task.Factory.StartNew(delegate
				{
					return GetAiAttributions(gitModule, args);
				});
				GitCommandResult<GetBlameGitCommand.BlameChunk[]> blameResult = new GetBlameGitCommand().Execute(gitModule, args.Filepath, $"{args.Sha}~");
				if (!blameResult.Succeeded)
				{
					base.Dispatcher.Post(delegate
					{
						ShowErrorFallback(blameResult.Error);
					});
				}
				else
				{
					base.Dispatcher.Post(delegate
					{
						if (TextDiffControl.VisualPatch.VisualDiff.Node == diff)
						{
							BusyIndicator.Hide();
							_undoManager.Add(args);
							RefreshUndoControls();
							Revision revision = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == args.Sha).Revision.Revision;
							List<BlameBlockContext> blockContexts = new List<BlameBlockContext>();
							BlameListBox.ItemsSource = CreateBlameItems(blameResult.Result, TextDiffControl.VisualPatch, revision, blockContexts);
							if (RevisionListScrollViewer != null)
							{
								RevisionListScrollViewer.ScrollChanged -= RevisionListScrollViewer_ScrollChanged;
								RevisionListScrollViewer.ScrollChanged += RevisionListScrollViewer_ScrollChanged;
							}
							RevisionListFallbackBorder.Hide();
							RevisionListFallbackUserControl.Hide();
							BlameListBox.Show();
							ApplyAiAttributions(diff, aiAttributionTask, blockContexts);
						}
					});
				}
				}
			}).Start();
		}

		/// <summary>
		/// 构建 blame 列表项。blockContexts 非空时，为"当前提交（newCommit）新增行"的块
		/// 记录头部项与块内新文件行号（1-based），供 git-ai 归属数据异步到达后
		/// 补 AI 徽标（见 ApplyAiAttributions）；旧提交块不打徽标（git-ai diff 只查当前提交）。
		/// </summary>
		private static BlameItemViewModel[] CreateBlameItems(GetBlameGitCommand.BlameChunk[] blameChunks, VisualPatch visualPatch, Revision newCommit, List<BlameBlockContext> blockContexts)
		{
			Revision[] array = Expand(blameChunks);
			List<Revision> list = new List<Revision>();
			// 与 list 平行的新文件行号表：Added/Context 行记录其在提交后版本中的行号（1-based），
			// Deleted 行只存在于旧版本，记 0 表示"新文件中无此行"。
			List<int> newFileLineNumbers = new List<int>();
			bool flag = false;
			VisualChunk[] visualChunks = visualPatch.VisualDiff.VisualChunks;
			foreach (VisualChunk obj in visualChunks)
			{
				int num = obj.Node.FromStart;
				int num2 = obj.Node.ToStart;
				VisualSubChunk[] visualSubChunks = obj.VisualSubChunks;
				foreach (VisualSubChunk visualSubChunk in visualSubChunks)
				{
					if (visualSubChunk.PragmaLines.Length != 0)
					{
						flag = true;
					}
					for (int k = visualSubChunk.PreContextLines.Start; k < visualSubChunk.PreContextLines.End; k++)
					{
						list.Add(array[num - 1]);
						newFileLineNumbers.Add(num2);
						num++;
						num2++;
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						list.Add(array[num - 1]);
						newFileLineNumbers.Add(0);
						num++;
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						list.Add(newCommit);
						newFileLineNumbers.Add(num2);
						num2++;
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						list.Add(array[num - 1]);
						newFileLineNumbers.Add(num2);
						num++;
						num2++;
					}
				}
			}
			List<BlameItemViewModel> list2 = new List<BlameItemViewModel>();
			int num3 = 0;
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				if (num4 > 0 && list[num3].Sha != list[num4].Sha)
				{
					BlameItemViewModel headerItem = new BlameItemViewModel(list[num3]);
					list2.Add(headerItem);
					AddBlameBlockContext(blockContexts, headerItem, newFileLineNumbers, num3, num4, newCommit);
					for (int num5 = 1; num5 < num4 - num3; num5++)
					{
						list2.Add(new BlameItemBodyViewModel(list[num3]));
					}
					num3 = num4;
				}
			}
			BlameItemViewModel lastHeaderItem = new BlameItemViewModel(list[num3]);
			list2.Add(lastHeaderItem);
			AddBlameBlockContext(blockContexts, lastHeaderItem, newFileLineNumbers, num3, list.Count, newCommit);
			for (int num6 = 1; num6 < list.Count - num3; num6++)
			{
				list2.Add(new BlameItemBodyViewModel(list[num3]));
			}
			list2.Add(new DummyBlameItemViewModel(DummyRevision));
			list2.Add(new DummyBlameItemBodyViewModel(DummyRevision));
			if (flag)
			{
				list2.Add(new DummyBlameItemBodyViewModel(DummyRevision));
			}
			return list2.ToArray();
		}

		/// <summary>
		/// 为"当前提交（newCommit）新增行"的 blame 块记录匹配上下文：头部项 + 块内新文件行号（1-based）。
		/// 只有这些块可能携带 AI 徽标——git-ai diff 查询的是当前提交的行级归属，
		/// 旧提交块的行号属于彼时版本，与当前提交的归属区间不对应。
		/// </summary>
		private static void AddBlameBlockContext(List<BlameBlockContext> blockContexts, BlameItemViewModel headerItem, List<int> newFileLineNumbers, int start, int end, Revision newCommit)
		{
			if (blockContexts == null || headerItem.Revision.Sha != newCommit.Sha)
			{
				return;
			}
			List<int> list = new List<int>();
			for (int i = start; i < end; i++)
			{
				if (newFileLineNumbers[i] > 0)
				{
					list.Add(newFileLineNumbers[i]);
				}
			}
			if (list.Count > 0)
			{
				blockContexts.Add(new BlameBlockContext(headerItem, list));
			}
		}

		/// <summary>
		/// 获取当前提交在当前文件上的 git-ai 行级归属（后台线程调用）。
		/// git-ai 未安装、被用户关闭或该提交无 AI 代码时返回空列表——
		/// AI 归属是增强信息，任何失败都静默降级，不影响 blame 主流程。
		/// </summary>
		private static List<GitAiLineAttribution> GetAiAttributions(GitModule gitModule, BlameArgs args)
		{
			if (!App.IsAiAttributionEnabled)
			{
				return new List<GitAiLineAttribution>();
			}
			GitCommandResult<GitAiDiffAttribution> aiResult = new GetGitAiDiffAttributionGitCommand().Execute(gitModule, args.Sha, App.GitAiPath);
			if (!aiResult.Succeeded)
			{
				return new List<GitAiLineAttribution>();
			}
			return aiResult.Result.GetAttributions(args.Filepath);
		}

		/// <summary>
		/// 等待并行的 git-ai 归属查询完成（后台线程），再回到 UI 线程按块内行号
		/// 匹配归属区间、给命中的 blame 块头部补 AI 徽标。归属数据在 blame 列表
		/// 首屏之后异步浮现（SetAiAttribution 触发属性变更通知），
		/// 窗口交互不被 git-ai 的冷启动/超时阻塞。
		/// </summary>
		private void ApplyAiAttributions(Diff diff, Task<List<GitAiLineAttribution>> aiAttributionTask, List<BlameBlockContext> blockContexts)
		{
			if (blockContexts.Count == 0)
			{
				return;
			}
			new Task(delegate
			{
				List<GitAiLineAttribution> aiAttributions = aiAttributionTask.Result;
				if (aiAttributions.Count == 0)
				{
					return;
				}
				base.Dispatcher.Post(delegate
				{
					// 用户已切到其他提交/文件（diff 已被替换）时丢弃，避免把陈旧徽标打到新列表上
					if (TextDiffControl.VisualPatch.VisualDiff.Node != diff)
					{
						return;
					}
					foreach (BlameBlockContext blockContext in blockContexts)
					{
						MatchAiAttribution(blockContext, aiAttributions);
					}
				});
			}).Start();
		}

		/// <summary>
		/// 块内新文件行号逐个与归属区间比对，取命中行数最多的区间作为该块的归属
		/// （一个块通常只落在一个 agent 的生成区间内；命中行数用于 tooltip 的
		/// "n of m lines" 部分归属描述）。
		/// </summary>
		private static void MatchAiAttribution(BlameBlockContext blockContext, List<GitAiLineAttribution> aiAttributions)
		{
			GitAiLineAttribution bestAttribution = null;
			int bestHitCount = 0;
			foreach (GitAiLineAttribution attribution in aiAttributions)
			{
				int hitCount = 0;
				foreach (int lineNumber in blockContext.NewFileLineNumbers)
				{
					if (attribution.Contains(lineNumber))
					{
						hitCount++;
					}
				}
				if (hitCount > bestHitCount)
				{
					bestAttribution = attribution;
					bestHitCount = hitCount;
				}
			}
			if (bestAttribution != null)
			{
				blockContext.HeaderItem.SetAiAttribution(bestAttribution, bestHitCount, blockContext.NewFileLineNumbers.Count);
			}
		}

		private static Revision[] Expand(GetBlameGitCommand.BlameChunk[] chunks)
		{
			if (chunks.Length == 0)
			{
				return new Revision[0];
			}
			Revision[] array = new Revision[chunks[chunks.Length - 1].LineNumber + chunks[chunks.Length - 1].LineCount - 1];
			foreach (GetBlameGitCommand.BlameChunk blameChunk in chunks)
			{
				for (int j = 0; j < blameChunk.LineCount; j++)
				{
					int num = blameChunk.LineNumber + j - 1;
					array[num] = blameChunk.Revision;
				}
			}
			return array;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			else if (KeyboardHelper.IsCtrlDown && e.Key == Key.G)
			{
				ShowGoToLineWindow();
			}
			else if (KeyboardHelper.IsAltDown && e.Key == Key.Left)
			{
				Undo();
			}
			else if (KeyboardHelper.IsAltDown && e.Key == Key.Right)
			{
				Redo();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			// Migration note：Avalonia PointerPressedEventArgs 无 ChangedButton，
			// 用 GetCurrentPoint(this).Properties.IsXButton1/2Pressed 判断按下的鼠标侧键。
			global::Avalonia.Input.PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
			if (properties.IsXButton1Pressed)
			{
				Undo();
				e.Handled = true;
			}
			else if (properties.IsXButton2Pressed)
			{
				Redo();
				e.Handled = true;
			}
		}

		private void RevisionListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			double verticalOffset = e.OffsetDelta.Y;
			// Migration note：SplitTextDiffControl.VerticalOffset 在 Avalonia 版为只读，
			// 改调其 ScrollToVerticalOffset（行为与原赋值一致）。
			// Migration note：SplitTextDiffControl 自带 ScrollToVerticalOffset，无需 ScrollViewer 扩展。
                        TextDiffControl.ScrollToVerticalOffset(verticalOffset);
		}

		private void SplitTextDiffControl_ScrollOffsetChanged(object sender, EventArgs e)
		{
			double verticalOffset = TextDiffControl.VerticalOffset;
			ScrollTo(verticalOffset);
		}

		private void ShaButton_Click(object sender, RoutedEventArgs e)
		{
			if (!(sender is Button { DataContext: var dataContext }))
			{
				return;
			}
			BlameItemViewModel blameChunk = dataContext as BlameItemViewModel;
			if (blameChunk != null)
			{
				RevisionsComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == blameChunk.RevisionSha);
			}
		}

		private void OpenRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule != null && sender is Button { DataContext: BlameItemViewModel dataContext })
			{
				RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(dataContext.RevisionSha);
				string fileToSelect = _selectedRevision?.ChangedFile?.Path;
				RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(gitModule, target, fileToSelect);
			}
		}

		private void RevisionsListBoxItem_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
			e.Handled = true;
			// Migration note：WPF ItemsControl.ContainerFromElement(itemsControl, element) 双参静态方法
			// 在 Avalonia 无对应，改用 WpfCompat 的单参扩展（沿可视树向上找已生成的条目容器）。
			if ((sender as ListBox)?.ContainerFromElement(e.Source as global::Avalonia.Visual) is ListBoxItem { DataContext: BlameItemViewModel dataContext })
			{
				GitModule gitModule = _repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RevealRevision(gitModule, dataContext.RevisionSha);
				}
			}
		}

		private void BlameListBox_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			if (!((sender as ListBox)?.ContainerFromElement(e.Source as global::Avalonia.Visual) is ListBoxItem { DataContext: var dataContext }))
			{
				return;
			}
			BlameItemViewModel blameItem = dataContext as BlameItemViewModel;
			if (blameItem == null)
			{
				return;
			}
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			List<Control> list = new List<Control>();
			if (blameItem.RevisionSha != DummyRevision.Sha)
			{
				MenuItem item = RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateMenuItem(delegate
				{
					string fileToSelect = _selectedRevision?.ChangedFile?.Path;
					RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(blameItem.RevisionSha);
					RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(gitModule, target, fileToSelect);
				}, isEnabled: true, showShortcut: false);
				list.Add(item);
				MenuItem menuItem = new MenuItem();
				menuItem.Header = Translate("Reveal in ForkPlus");
				menuItem.Click += delegate
				{
					string filePath = _selectedRevision?.ChangedFile?.Path;
					RevealRevision(gitModule, blameItem.RevisionSha, filePath);
				};
				list.Add(menuItem);
				list.Add(new Separator());
				ChangedFile changedFile = _selectedRevision?.ChangedFile;
				if (changedFile != null)
				{
					list.AddRange(CreateFileContextMenuItems(_repositoryUserControl, changedFile, blameItem));
				}
				list.Add(new Separator());
				list.AddRange(CreateRevisionContextMenuItems(blameItem));
			}
			BlameListBox.ContextMenu.SetItems(list);
		}

		private void ScrollTo(double verticalOffset)
		{
			// Migration note：Avalonia ScrollViewer 无 ScrollToVerticalOffset 方法，
			// 用 WpfCompat 的 ScrollToVerticalOffsetCompat（内部设置 Offset）。
			RevisionListScrollViewer?.ScrollToVerticalOffsetCompat(verticalOffset);
		}

		private void RevealRevision(GitModule gitModule, Sha sha, [Null] string filePath = null)
		{
			(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow.Activate();
			if (MainWindow.ActiveRepositoryUserControl?.GitModule != gitModule)
			{
				Application.Current.TabManager()?.OpenRepository(gitModule.Path);
			}
			MainWindow.ActiveRepositoryUserControl?.SelectRevision(sha, filePath);
		}

		private void UndoButton_Click(object sender, RoutedEventArgs e)
		{
			Undo();
		}

		private void RedoButton_Click(object sender, RoutedEventArgs e)
		{
			Redo();
		}

		private void RevisionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized)
			{
				if (RevisionsComboBox.SelectedItem is RevisionViewModel selectedRevision)
				{
					_selectedRevision = selectedRevision;
					RevisionTimeLine.ActiveRevision = _selectedRevision.Sha;
				}
				_refreshBlame.InvokeWithDelay(new BlameArgs(_selectedRevision.Sha, _selectedRevision.FilePath));
			}
		}

		private void Undo()
		{
			if (!_undoManager.IsUndoPossible)
			{
				return;
			}
			_undoManager.Undo();
			BlameArgs previousItem = _undoManager.CurrentItem;
			if (previousItem != null)
			{
				RevisionsComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == previousItem.Sha);
			}
			RefreshUndoControls();
		}

		private void Redo()
		{
			if (!_undoManager.IsRedoPossible)
			{
				return;
			}
			_undoManager.Redo();
			BlameArgs nextItem = _undoManager.CurrentItem;
			if (nextItem != null)
			{
				RevisionsComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == nextItem.Sha);
			}
			RefreshUndoControls();
		}

		private void ShowErrorFallback(GitCommandError error)
		{
			ShowErrorFallback(error.ToString());
		}

		private void ShowErrorFallback(string errorString)
		{
			BusyIndicator.Hide();
			CodeEditorFallbackUserControl.Show();
			CodeEditorFallbackUserControl.FallbackTitle = Translate("Error");
			CodeEditorFallbackUserControl.FallbackMessage = errorString;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void ShowGoToLineWindow()
		{
			GoToLineWindow goToLineWindow = new GoToLineWindow();
			goToLineWindow.SetOwnerCompat(this);
			if (goToLineWindow.ShowDialog().GetValueOrDefault() && goToLineWindow.LineNumber.HasValue)
			{
				TextDiffControl.ScrollToLine(goToLineWindow.LineNumber.Value);
			}
		}

		private static IEnumerable<Control> CreateFileContextMenuItems(RepositoryUserControl repositoryUserControl, ChangedFile changedFile, BlameItemViewModel chunk)
		{
			yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.SaveFile.Execute(repositoryUserControl, changedFile, chunk.RevisionSha.ToString());
			});
		}

		private static IEnumerable<Control> CreateRevisionContextMenuItems(BlameItemViewModel chunk)
		{
			yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(new Revision[1] { chunk.Revision });
			}, isEnabled: true, showShortcut: false);
			yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(new Revision[1] { chunk.Revision });
			}, isEnabled: true, showShortcut: false);
		}

	}
}
