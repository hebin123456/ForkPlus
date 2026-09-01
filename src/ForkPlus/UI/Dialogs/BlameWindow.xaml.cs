using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
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
				if (VisualTreeHelper.GetChildrenCount(BlameListBox) <= 0)
				{
					return null;
				}
				if (!(VisualTreeHelper.GetChild(BlameListBox, 0) is Border reference))
				{
					return null;
				}
				if (!(VisualTreeHelper.GetChild(reference, 0) is ScrollViewer result))
				{
					return null;
				}
				return result;
			}
		}

		public BlameWindow(RepositoryUserControl repositoryUserControl, string filePath, Sha? shaToSelect, [Null] ForkPlus.Git.Reference targetReference)
		{
			_repositoryUserControl = repositoryUserControl;
			_refreshBlame = new DelayedAction<BlameArgs>(RefreshBlame);
			base.Title = PathHelper.GetReadableFileName(filePath) + " - " + Translate("Blame");
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			InitializeComponent();
			BlameTitleTextBlock.Text = Translate("Blame");
			UndoButton.ToolTip = Translate("Go Back");
			RedoButton.ToolTip = Translate("Go Forward");
			TextDiffControl.FontSize = 14.0;
			TextDiffControl.ScrollOffsetChanged += SplitTextDiffControl_ScrollOffsetChanged;
			TextDiffControl.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
			RevisionListFallbackBorder.Show();
			RevisionListFallbackUserControl.Show();
			RevisionListFallbackUserControl.FallbackTitle = Translate("Loading...");
			BlameListBox.Hide();
			CodeEditorFallbackUserControl.Show();
			TextDiffControl.Hide();
			FileIcon.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(filePath));
			FileNameTextBlock.FilePath = filePath;
			FileNameTextBlock.ToolTip = filePath;
			RefreshUndoControls();
			Initialize(filePath, shaToSelect, targetReference);
			base.SizeChanged += BlameWindow_SizeChanged;
			base.Activated += BlameWindow_Activated;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetWindowLocationState(ForkPlusSettings.Default.BlameWindowLocationState);
		}

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
					base.Dispatcher.Async(delegate
					{
						ShowErrorFallback(shaResult.Error);
					});
				}
				else
				{
					GitCommandResult<RevisionWithFiles[]> fileHistoryResult = new GetFileHistoryGitCommand().Execute(gitModule, repositoryData.Submodules.Items, filePath, targetReference?.Sha);
					if (!fileHistoryResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							ShowErrorFallback(fileHistoryResult.Error);
						});
					}
					else
					{
						RevisionViewModel[] revisions = fileHistoryResult.Result.Map((RevisionWithFiles x) => new RevisionViewModel(x));
						base.Dispatcher.Async(delegate
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
			FileNameTextBlock.ToolTip = args.Filepath;
			int tabWidth = gitModule.Settings.TabWidth;
			new Task(delegate
			{
				ChangedFile changedFile = new ChangedFile(PathHelper.NormalizeUnix(args.Filepath), StatusType.Modified);
				GitCommandResult<DiffContent> fileDiffResult = new GetRevisionFileChangesGitCommand().Execute(gitModule, new RevisionDiffTarget.Revision(args.Sha), changedFile, 1, tabWidth, ignoreWhitespaces: false, showEntireFile: true);
				if (!fileDiffResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						ShowErrorFallback(fileDiffResult.Error);
					});
				}
				else if (!(fileDiffResult.Result is ParsedDiffContent parsedDiffContent) || parsedDiffContent.Diff.Chunks.Length == 0)
				{
					base.Dispatcher.Async(delegate
					{
						ShowErrorFallback(Translate("Blame can only be used for text files"));
					});
				}
				else
				{
					Diff diff = parsedDiffContent.Diff;
					base.Dispatcher.Async(delegate
					{
						CodeEditorFallbackUserControl.Hide();
						TextDiffControl.Show();
						TextDiffControl.SetDiff(diff, tabWidth, entireFile: true, DiffLocation.Revision);
					});
					GitCommandResult<GetBlameGitCommand.BlameChunk[]> blameResult = new GetBlameGitCommand().Execute(gitModule, args.Filepath, $"{args.Sha}~");
				if (!blameResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						ShowErrorFallback(blameResult.Error);
					});
				}
				else
				{
					// git-ai 行级归属：仅当前提交新增的行会有归属条目。
					// git-ai 未安装 / 用户关闭开关 / 仓库未使用 git-ai 时得到空列表，Blame 视图不显示 AI 徽标。
					List<GitAiLineAttribution> aiAttributions = GetAiAttributions(gitModule, args);
					base.Dispatcher.Async(delegate
					{
						if (TextDiffControl.VisualPatch.VisualDiff.Node == diff)
						{
							BusyIndicator.Hide();
							_undoManager.Add(args);
							RefreshUndoControls();
							Revision revision = IReadOnlyListExtensions.FirstItem(_revisions, (RevisionViewModel x) => x.Sha == args.Sha).Revision.Revision;
							BlameListBox.ItemsSource = CreateBlameItems(blameResult.Result, TextDiffControl.VisualPatch, revision, aiAttributions);
								if (RevisionListScrollViewer != null)
								{
									RevisionListScrollViewer.ScrollChanged -= RevisionListScrollViewer_ScrollChanged;
									RevisionListScrollViewer.ScrollChanged += RevisionListScrollViewer_ScrollChanged;
								}
								RevisionListFallbackBorder.Hide();
								RevisionListFallbackUserControl.Hide();
								BlameListBox.Show();
							}
						});
					}
				}
			}).Start();
		}

		/// <summary>
		/// 获取当前提交在当前文件上的 git-ai 行级归属（后台线程调用）。
		/// git-ai 未安装、被关闭或该提交无 AI 代码时返回空列表。
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

		private static BlameItemViewModel[] CreateBlameItems(GetBlameGitCommand.BlameChunk[] blameChunks, VisualPatch visualPatch, Revision newCommit, List<GitAiLineAttribution> aiAttributions)
		{
			Revision[] array = Expand(blameChunks);
			List<Revision> list = new List<Revision>();
			// 与 list 平行的新文件行号（1-based）。删除行只存在于旧文件，记 0（不会命中 AI 归属）。
			List<int> list2 = new List<int>();
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
						list2.Add(num2);
						num++;
						num2++;
					}
					for (int l = visualSubChunk.DeletedLines.Start; l < visualSubChunk.DeletedLines.End; l++)
					{
						list.Add(array[num - 1]);
						list2.Add(0);
						num++;
					}
					for (int m = visualSubChunk.AddedLines.Start; m < visualSubChunk.AddedLines.End; m++)
					{
						list.Add(newCommit);
						list2.Add(num2);
						num2++;
					}
					for (int n = visualSubChunk.PostContextLines.Start; n < visualSubChunk.PostContextLines.End; n++)
					{
						list.Add(array[num - 1]);
						list2.Add(num2);
						num++;
						num2++;
					}
				}
			}
			List<BlameItemViewModel> list3 = new List<BlameItemViewModel>();
			int num3 = 0;
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				if (num4 > 0 && list[num3].Sha != list[num4].Sha)
				{
					BlameItemViewModel blameItemViewModel = new BlameItemViewModel(list[num3]);
					ApplyAiAttribution(blameItemViewModel, list, list2, num3, num4, newCommit, aiAttributions);
					list3.Add(blameItemViewModel);
					for (int num5 = 1; num5 < num4 - num3; num5++)
					{
						list3.Add(new BlameItemBodyViewModel(list[num3]));
					}
					num3 = num4;
				}
			}
			BlameItemViewModel blameItemViewModel2 = new BlameItemViewModel(list[num3]);
			ApplyAiAttribution(blameItemViewModel2, list, list2, num3, list.Count, newCommit, aiAttributions);
			list3.Add(blameItemViewModel2);
			for (int num6 = 1; num6 < list.Count - num3; num6++)
			{
				list3.Add(new BlameItemBodyViewModel(list[num3]));
			}
			list3.Add(new DummyBlameItemViewModel(DummyRevision));
			list3.Add(new DummyBlameItemBodyViewModel(DummyRevision));
			if (flag)
			{
				list3.Add(new DummyBlameItemBodyViewModel(DummyRevision));
			}
			return list3.ToArray();
		}

		/// <summary>
		/// 给 blame 块头部视图模型打 AI 归属徽标。
		/// 仅当该块属于当前被 blame 的提交（新增行），且其中有行命中 git-ai 归属区间时设置。
		/// </summary>
		/// <param name="header">块头部视图模型（XAML 徽标绑定其 AiBadgeVisibility）。</param>
		/// <param name="revisions">按显示顺序排列的行归属（与 lineNumbers 平行）。</param>
		/// <param name="lineNumbers">每行对应的新文件行号（1-based，0 = 仅旧文件的删除行）。</param>
		/// <param name="start">块起始下标（含）。</param>
		/// <param name="end">块结束下标（不含）。</param>
		/// <param name="newCommit">当前被 blame 的提交。</param>
		/// <param name="aiAttributions">git-ai diff 归属区间（空 = 功能未启用）。</param>
		private static void ApplyAiAttribution(BlameItemViewModel header, List<Revision> revisions, List<int> lineNumbers, int start, int end, Revision newCommit, List<GitAiLineAttribution> aiAttributions)
		{
			if (aiAttributions.Count == 0 || end <= start || revisions[start].Sha != newCommit.Sha)
			{
				return;
			}
			GitAiLineAttribution first = null;
			int num = 0;
			for (int i = start; i < end; i++)
			{
				int lineNumber = lineNumbers[i];
				if (lineNumber <= 0)
				{
					continue;
				}
				GitAiLineAttribution gitAiLineAttribution = IReadOnlyListExtensions.FirstItem(aiAttributions, (GitAiLineAttribution x) => x.Contains(lineNumber));
				if (gitAiLineAttribution != null)
				{
					num++;
					if (first == null)
					{
						first = gitAiLineAttribution;
					}
				}
			}
			if (first != null)
			{
				header.SetAiAttribution(first, num, end - start);
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

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.ChangedButton == MouseButton.XButton1)
			{
				Undo();
				e.Handled = true;
			}
			else if (e.ChangedButton == MouseButton.XButton2)
			{
				Redo();
				e.Handled = true;
			}
		}

		private void RevisionListScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			double verticalOffset = e.VerticalOffset;
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

		private void RevisionsListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: BlameItemViewModel dataContext })
			{
				GitModule gitModule = _repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RevealRevision(gitModule, dataContext.RevisionSha);
				}
			}
		}

		private void BlameListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!(ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: var dataContext }))
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
			RevisionListScrollViewer?.ScrollToVerticalOffset(verticalOffset);
		}

		private void RevealRevision(GitModule gitModule, Sha sha, [Null] string filePath = null)
		{
			Application.Current.MainWindow.Activate();
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
			goToLineWindow.Owner = this;
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
