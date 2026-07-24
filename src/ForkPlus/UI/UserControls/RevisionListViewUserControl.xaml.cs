// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia（Application/AvaloniaObject/IVisual）+ using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/ScrollViewer/Border/ListBoxItem/ContentPresenter/StackPanel/Separator/MenuItem/ContextMenu/SizeChangedEventArgs/SelectionChangedEventArgs/ContextMenuEventArgs）
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Run）
// - using System.Windows.Input → using Avalonia.Input（Key/KeyEventArgs/KeyboardNavigation/KeyboardNavigationMode/DragEventArgs/PointerPressedEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → 移除（本文件未直接引用 Media 类型；Visual/IVisual 由 using Avalonia 提供）
// - 新增 using Avalonia.Layout（Orientation）、using Avalonia.VisualTree（GetVisualDescendants/GetVisualParent）
// - 新增 using ForkPlus.Services（ServiceLocator/MessageBoxButton/MessageBoxImage，替代 System.Windows.MessageBox）
// - KeyboardNavigation.TabNavigationProperty.OverrideMetadata + FrameworkPropertyMetadata → OverrideDefaultValue<T>（参考 StageFileUserControl）
// - WeakEventManager<TSender,TArgs>.AddHandler(obj,"Event",h) → obj.Event += h（直接订阅，参考 StageFileUserControl）
// - base.PreviewKeyDown/RevisionListView.PreviewKeyDown → base.KeyDown/RevisionListView.KeyDown（Avalonia 无 Preview 变体）
// - VisualTreeHelper.GetChildrenCount/GetChild → GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()（参考 NoUIAutomationListView）
// - VisualTreeHelper.GetParent → (x as IVisual)?.GetVisualParent()（参考 DependencyObjectExtensions）
// - DependencyObject → AvaloniaObject；e.OriginalSource → e.Source（参考 ListViewScrollbarDoubleClickHelper）
// - ScrollViewer.VerticalOffset/ViewportHeight → Offset.Y/Viewport.Height（参考 NoUIAutomationListView）
// - MouseDoubleClick + MouseButtonEventArgs → PointerPressed + PointerPressedEventArgs + ClickCount==2（参考 Treemap；XAML 需同步迁移）
// - e.Data.GetData → e.Data.Get（参考 FileListTreeView）
// - MessageBox.Show → ServiceLocator.MessageBox.Show（参考 CheckForkSyncCommand）
// - MoveFocus(TraversalRequest) → 无 Avalonia 等价，注释（参考 MultiselectionTreeView）
// - FrameworkElement → Control（参考 DiffEntryRowUserControl）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ForkPlus.Biturbo;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.Utils.Http;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionListViewUserControl : UserControl
	{
		private class DecoratedRevisionRowComparer : IComparer<DecoratedRevision>
		{
			public static readonly DecoratedRevisionRowComparer Instance = new DecoratedRevisionRowComparer();

			public int Compare(DecoratedRevision x, DecoratedRevision y)
			{
				return x.Row.CompareTo(y.Row);
			}
		}

		public readonly RevisionsDataSource RevisionsDataSource = new RevisionsDataSource();

		private readonly DelayedAction<string> _refreshContextSearch;

		private Job _activeContextSearchJob;

		private Job _activeSidebarSearchJob;

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public SearchTabItem SidebarSearchTabItem { get; private set; }

		public int SelectedIndex => RevisionListView.SelectedIndex;

		public DecoratedRevision SelectedRevision => RevisionListView.SelectedItem as DecoratedRevision;

		public DecoratedRevision[] SelectedRevisions => RevisionListView.SelectedItems.CompactMap((object x) => x as DecoratedRevision);

		public event EventHandler<EventArgs<RevisionSearchQuery>> SearchQueryChanged;

		public event EventHandler<EventArgs<DecoratedRevision[]>> SelectionChanged;

		public event EventHandler<EventArgs<DecoratedRevision>> RevisionDoubleClick;

		public event EventHandler<EventArgs<Branch>> BranchDoubleClick;

		public void UpdateRepositoryData(RepositoryData repositoryData)
		{
			RevisionListView.SelectedIndex = -1;
			RevisionsDataSource.Reload(RepositoryUserControl.JobQueue, repositoryData.RevisionStorage, repositoryData.Stashes, repositoryData.References, repositoryData.Remotes, repositoryData.Worktrees, repositoryData.ShowStashesInRevisionList, repositoryData.Reflog, repositoryData.CollapseState, repositoryData.UserColors, RepositoryUserControl.GitModule);
		}

		static RevisionListViewUserControl()
		{
			// 阶段 4.5：WPF OverrideMetadata + FrameworkPropertyMetadata → Avalonia OverrideDefaultValue<T>。
			KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RevisionListViewUserControl>(KeyboardNavigationMode.Local);
		}

		public RevisionListViewUserControl()
		{
			InitializeComponent();
			_refreshContextSearch = new DelayedAction<string>(RefreshContextSearch, 0.1);
			RevisionSearchPanelUserControl.SearchQueryChanged += RevisionSearchPanelUserControl_SearchQueryChanged;
			RevisionSearchPanelUserControl.JumpToPreviousSearchResult += delegate
			{
				JumpToPreviousContextSearchResult();
			};
			RevisionSearchPanelUserControl.JumpToNextSearchResult += delegate
			{
				JumpToNextContextSearchResult(initialJump: false);
			};
			RevisionSearchPanelUserControl.Closed += RevisionSearchPanelUserControl_Closed;
			RevisionListView.ItemsSource = RevisionsDataSource;
			DragAndDropListView revisionListView = RevisionListView;
			revisionListView.ItemDrag = (EventHandler<EventArgs>)Delegate.Combine(revisionListView.ItemDrag, new EventHandler<EventArgs>(ValidateDrag));
			// 阶段 4.5：WPF MouseDoubleClick → Avalonia PointerPressed + ClickCount==2（XAML 需同步迁移：移除 MouseDoubleClick 绑定）。
			RevisionListView.PointerPressed += RevisionListView_PointerPressed;
			// 阶段 4.5：WPF PreviewKeyDown → Avalonia KeyDown（无 Preview 变体，参考 StageFileUserControl）。
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if ((e.Key == Key.F3 || (e.Key == Key.F && KeyboardHelper.IsCtrlDown)) && !KeyboardHelper.IsShiftDown)
				{
					RevisionSearchPanelUserControl.ShowSearchBar();
					e.Handled = true;
				}
				else if (e.Key == Key.Escape && RevisionSearchPanelUserControl.IsSearchBarVisible)
				{
					RevisionSearchPanelUserControl.HideSearchBar();
					FocusSelectedItem();
					e.Handled = true;
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Left)
				{
					if (RevisionListView.SelectedItem is DecoratedRevision decoratedRevision2)
					{
						Collapse(decoratedRevision2.Sha);
						e.Handled = true;
					}
				}
				else if (e.Key == Key.Right && RevisionListView.SelectedItem is DecoratedRevision decoratedRevision3)
				{
					Expand(decoratedRevision3.Sha);
					e.Handled = true;
				}
			};
			// 阶段 4.5：WPF PreviewKeyDown → Avalonia KeyDown。
			RevisionListView.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.A && KeyboardHelper.IsCtrlDown && !KeyboardHelper.IsShiftDown)
				{
					e.Handled = true;
				}
			};
			// 阶段 4.5：WPF CommandBindings.Add(RoutedCommand binding) → Avalonia KeyBindings.Add(KeyBinding)。
			// CreateShortcutCommandBinding 已改为返回 KeyBinding（参考 IUICommandExtension）。
			base.KeyBindings.Add(RepositoryUserControl.Commands.CopyRevisionSha.CreateShortcutKeyBinding(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(SelectedRevisions.Map((DecoratedRevision x) => x.ToRevision()));
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.CopyRevisionInfo.CreateShortcutKeyBinding(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(SelectedRevisions.Map((DecoratedRevision x) => x.ToRevision()));
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.CreateShortcutKeyBinding(delegate
			{
				GitModule gitModule2 = RepositoryUserControl.GitModule;
				if (gitModule2 != null)
				{
					RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
					if (repositoryData != null && RevisionListView.SelectedItem is DecoratedRevision decoratedRevision)
					{
						if (decoratedRevision.IsStash())
						{
							RepositoryUserControl.Commands.ShowRenameStashWindow.Execute(RepositoryUserControl, decoratedRevision.AsStashRevision());
						}
						else if (System.Linq.Enumerable.FirstOrDefault(decoratedRevision.References ?? Enumerable.Empty<ReferenceViewModel>(), (ReferenceViewModel x) => x.Reference is LocalBranch)?.Reference is LocalBranch localBranch)
						{
							RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(RepositoryUserControl, gitModule2, repositoryData.References, localBranch);
						}
					}
				}
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.RemoveReferenceCommand.CreateShortcutKeyBinding(delegate
			{
				DecoratedRevision[] array = RevisionListView.SelectedItems.CompactMap((object x) => x as DecoratedRevision);
				if (array.All((DecoratedRevision x) => x.IsStash()))
				{
					StashRevision[] stashes = array.Map((DecoratedRevision x) => x.AsStashRevision());
					RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(RepositoryUserControl, stashes);
				}
				else if (array.Length == 1)
				{
					ReferenceViewModel[] references = array[0].References;
					if (references != null)
					{
						ForkPlus.Git.Reference[] referencesToRemove = references.Map((ReferenceViewModel x) => x.Reference);
						RepositoryUserControl.Commands.RemoveReferenceCommand.Execute(RepositoryUserControl, referencesToRemove);
					}
				}
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateShortcutKeyBinding(delegate
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					DecoratedRevision[] selectedRevisions = SelectedRevisions;
					if (selectedRevisions.Length == 1)
					{
						RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(gitModule, new RevisionDiffTarget.Revision(selectedRevisions[0].Sha));
					}
					else if (selectedRevisions.Length == 2)
					{
						RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(gitModule, new RevisionDiffTarget.Range(selectedRevisions[0].Sha, selectedRevisions[1].Sha));
					}
				}
			}));
			// 阶段 4.5：WPF WeakEventManager<TSender,TArgs>.AddHandler(obj,"Event",h) → 直接订阅 obj.Event（参考 StageFileUserControl）。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
			NotificationCenter.Current.RevisionListOrientatioChanged += delegate
			{
				RefreshRevisionListViewTemplate();
			};
			RefreshRevisionListViewTemplate();
		}

		public void Initialize(RepositoryUserControl repositoryUserControl, SearchTabItem sidebarSearchTabItem)
		{
			RepositoryUserControl = repositoryUserControl;
			SidebarSearchTabItem = sidebarSearchTabItem;
			SidebarSearchTabItem.SearchQueryChanged += SidebarSearchPanelUserControl_SearchQueryChanged;
		}

		public Sha? GetBottomShaInViewPort()
		{
			// 阶段 4.5：WPF VisualTreeHelper.GetChildrenCount/GetChild 逐层查找 → Avalonia GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()（参考 NoUIAutomationListView）。
			ScrollViewer scrollViewer = RevisionListView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
			if (scrollViewer == null)
			{
				return null;
			}
			// 阶段 4.5：WPF ScrollViewer.VerticalOffset/ViewportHeight → Avalonia Offset.Y/Viewport.Height（参考 NoUIAutomationListView）。
			int num = (int)(scrollViewer.Offset.Y + scrollViewer.Viewport.Height) - 1;
			if (num < 0 || num >= RevisionsDataSource.Count)
			{
				return null;
			}
			return RevisionsDataSource.ShaAtRow(num);
		}

		public Sha? GetBottomShaInSelection()
		{
			DecoratedRevision[] selectedRevisions = SelectedRevisions;
			if (selectedRevisions.Length == 0)
			{
				return null;
			}
			DecoratedRevision decoratedRevision = selectedRevisions[0];
			for (int i = 1; i < selectedRevisions.Length; i++)
			{
				if (selectedRevisions[i].Row > decoratedRevision.Row)
				{
					decoratedRevision = selectedRevisions[i];
				}
			}
			return decoratedRevision.Sha;
		}

		public void FocusSelectedItem()
		{
			RevisionListView.FocusSelectedItem();
		}

		public void Select(IReadOnlyList<int> rows)
		{
			RevisionListView.Select(rows, NoUIAutomationListView.SelectOptions.ScrollIntoView);
		}

		public bool Select(RevisionSelector select, NoUIAutomationListView.SelectOptions selectOptions, int fallbackRow = -1)
		{
			if (select is RevisionSelector.Head)
			{
				int? headRow = RevisionsDataSource.HeadRow;
				if (headRow.HasValue)
				{
					int valueOrDefault = headRow.GetValueOrDefault();
					RevisionListView.Select(valueOrDefault, selectOptions);
					return true;
				}
			}
			else if (select is RevisionSelector.Sha sha)
			{
				List<int> list = new List<int>();
				foreach (Sha sha2 in sha.Shas)
				{
					int? headRow = RevisionsDataSource.FindRowBySha(sha2);
					if (headRow.HasValue)
					{
						int valueOrDefault2 = headRow.GetValueOrDefault();
						list.Add(valueOrDefault2);
					}
				}
				RevisionListView.Select(list, selectOptions);
				if (list.Count > 0)
				{
					return true;
				}
			}
			if (fallbackRow != -1 && RevisionsDataSource.Count > fallbackRow)
			{
				RevisionListView.Select(fallbackRow, selectOptions);
				return true;
			}
			return false;
		}

		public void CollapseAll()
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RepositoryUserControl.GitModule.Settings.CollapseAllMergeRevisions = true;
				RepositoryUserControl.GitModule.Settings.Save();
				RepositoryUserControl.UpdateRepositoryData(repositoryData.With(repositoryData.CollapseState.CollapseAll()), null, null);
			}
		}

		public void ExpandAll()
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RepositoryUserControl.GitModule.Settings.CollapseAllMergeRevisions = false;
				RepositoryUserControl.GitModule.Settings.Save();
				RepositoryUserControl.UpdateRepositoryData(repositoryData.With(repositoryData.CollapseState.ExpandAll()), null, null);
			}
		}

		private void Collapse(Sha sha)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RepositoryUserControl.UpdateRepositoryData(repositoryData.With(repositoryData.CollapseState.Collapse(sha)), null, null);
			}
		}

		private void Expand(Sha sha)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RepositoryUserControl.UpdateRepositoryData(repositoryData.With(repositoryData.CollapseState.Expand(sha)), null, null);
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RevisionsDataSource.RefreshTheme();
		}

		private void RevisionListView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RefreshRevisionListViewTemplate();
			RevisionListView.UpdateResizableColumnWidth(0);
		}

		private void RevisionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (RevisionListView.IsMultiselectionInProgress)
			{
				return;
			}
			e.Handled = true;
			if (e.AddedItems.Count <= 0 && e.RemovedItems.Count <= 0)
			{
				return;
			}
			DecoratedRevision[] array = new DecoratedRevision[RevisionListView.SelectedItems.Count];
			for (int i = 0; i < RevisionListView.SelectedItems.Count; i++)
			{
				if (!(RevisionListView.SelectedItems[i] is DecoratedRevision decoratedRevision))
				{
					Log.Error($"Unexpected item in RevisionListView.SelectedItems[{i}]");
					return;
				}
				array[i] = decoratedRevision;
			}
			if (array.Length == 2)
			{
				array = SortRevisionsByRows(array);
			}
			IRoundedSelectionListBoxViewModel[] selectedItems = array;
			selectedItems.RefreshSelectionType();
			this.SelectionChanged?.Invoke(this, new EventArgs<DecoratedRevision[]>(array));
		}

		// 阶段 4.5：WPF MouseDoubleClick + MouseButtonEventArgs → Avalonia PointerPressed + PointerPressedEventArgs + ClickCount==2（参考 Treemap）。
		private void RevisionListView_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// 阶段 4.5：WPF MouseButtonEventArgs → Avalonia PointerPressedEventArgs；ClickCount==2 区分双击。
			if (e.ClickCount != 2)
			{
				return;
			}
			if (e.IsClickedOnScrollbar())
			{
				return;
			}
			Branch clickedBranch = GetClickedBranch(e);
			if (clickedBranch != null)
			{
				this.BranchDoubleClick?.Invoke(this, new EventArgs<Branch>(clickedBranch));
				e.Handled = true;
			}
			else if (RevisionListView.SelectedItem is DecoratedRevision decoratedRevision)
			{
				bool flag = decoratedRevision.GetParents().Length > 1;
				// 阶段 4.5：WPF e.OriginalSource → Avalonia e.Source（参考 ListViewScrollbarDoubleClickHelper）。
				if (!(e.Source is GraphCellView && flag))
				{
					this.RevisionDoubleClick?.Invoke(this, new EventArgs<DecoratedRevision>(decoratedRevision));
				}
			}
		}

		private void SidebarSearchPanelUserControl_SearchQueryChanged(object sender, EventArgs e)
		{
			RefreshSidebarSearch(SidebarSearchTabItem.SearchQuery);
		}

		private void RevisionSearchPanelUserControl_SearchQueryChanged(object sender, EventArgs e)
		{
			_refreshContextSearch.InvokeWithDelay(RevisionSearchPanelUserControl.SearchString);
		}

		private void RevisionListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (sender is ListBox listBox)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					GitModule gitModule = RepositoryUserControl.GitModule;
					if (gitModule != null)
					{
						RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
						if (repositoryData != null)
						{
							CommitGraphCache commitGraphCache = RepositoryUserControl.CommitGraphCache;
							if (commitGraphCache != null)
							{
								DecoratedRevision[] array = listBox.SelectedItems.CompactMap((object x) => x as DecoratedRevision);
								// 阶段 4.5：WPF e.OriginalSource → Avalonia e.Source（参考 ListViewScrollbarDoubleClickHelper）。
								if (e.Source is GraphCellView { DataContext: DecoratedRevision dataContext } && dataContext.GetParents().Length > 1)
								{
									listBox.ContextMenu.SetItems(CreateCollapseContextMenu(dataContext));
									return;
								}
								if (array.AllItems((DecoratedRevision x) => x.IsStash()))
								{
									StashRevision[] stashes = array.Map((DecoratedRevision x) => x.AsStashRevision());
									listBox.ContextMenu.SetItems(CreateStashContextMenuItems(repositoryUserControl, stashes));
									return;
								}
								DecoratedRevision decoratedRevision = array.SingleItem();
								if (decoratedRevision != null)
								{
									listBox.ContextMenu.SetItems(CreateRevisionContextMenuItems(repositoryUserControl, gitModule, repositoryData, decoratedRevision, commitGraphCache));
									return;
								}
								if (array.Length > 1)
								{
									listBox.ContextMenu.SetItems(CreateMultipleRevisionsContextMenuItems(repositoryUserControl, gitModule, repositoryData, array));
									return;
								}
								e.Handled = true;
								RevisionListView.ContextMenu.IsOpen = false;
								return;
							}
						}
					}
				}
			}
			e.Handled = true;
			RevisionListView.ContextMenu.IsOpen = false;
		}

		[Null]
		// 阶段 4.5：WPF MouseButtonEventArgs → Avalonia PointerPressedEventArgs；DependencyObject → AvaloniaObject；e.OriginalSource → e.Source（参考 ListViewScrollbarDoubleClickHelper）。
		private Branch GetClickedBranch(PointerPressedEventArgs args)
		{
			AvaloniaObject dependencyObject = args.Source as AvaloniaObject;
			while (dependencyObject != null && !(dependencyObject is ListBoxItem))
			{
				if (dependencyObject is Run run)
				{
					if (run.DataContext is BranchViewModel branchViewModel)
					{
						return branchViewModel.Reference as Branch;
					}
					return null;
				}
				// 阶段 4.5：WPF VisualTreeHelper.GetParent → Avalonia (x as Visual)?.GetVisualParent()（参考 DependencyObjectExtensions）。
				dependencyObject = (dependencyObject as Visual)?.GetVisualParent();
				if (dependencyObject is ContentPresenter { DataContext: BranchViewModel dataContext })
				{
					return dataContext.Reference as Branch;
				}
			}
			return null;
		}

		private IEnumerable<Control> CreateCollapseContextMenu(DecoratedRevision decoratedRevision)
		{
			if (RevisionsDataSource.IsRowCollapsed(decoratedRevision.Row))
			{
				yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Expand", delegate
				{
					Expand(decoratedRevision.Sha);
				});
			}
			else
			{
				yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Collapse", delegate
				{
					Collapse(decoratedRevision.Sha);
				});
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Expand All", delegate
			{
				ExpandAll();
			});
			yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Collapse All", delegate
			{
				CollapseAll();
			});
		}

		private bool RevisionsAreContinuous(DecoratedRevision[] revisions)
		{
			DecoratedRevision decoratedRevision = null;
			foreach (DecoratedRevision decoratedRevision2 in revisions)
			{
				if (decoratedRevision != null)
				{
					ShaBufferIterator parents = decoratedRevision.GetParents();
					if (parents.Length != 1 || !(parents.Nth(0) == decoratedRevision2.Sha))
					{
						return false;
					}
				}
				decoratedRevision = decoratedRevision2;
			}
			return true;
		}

		private static DecoratedRevision[] SortRevisionsByRows(DecoratedRevision[] revisions)
		{
			return revisions.ToSortedArray(DecoratedRevisionRowComparer.Instance);
		}

		private void RefreshRevisionListViewTemplate()
		{
			double num = 500.0;
			switch (ForkPlusSettings.Default.RevisionListOrientation)
			{
			case RevisionListOrientation.Vertical:
				if (RevisionListView.AvailableWidth > num && base.Resources["SingleRowGridView"] is GridView gridView && RevisionListView.View != gridView)
				{
					RevisionListView.View = gridView;
				}
				else if (RevisionListView.AvailableWidth <= num && base.Resources["DoubleRowGridView"] is GridView gridView2 && RevisionListView.View != gridView2)
				{
					RevisionListView.View = gridView2;
				}
				break;
			case RevisionListOrientation.Horizontal:
				RevisionListView.View = (GridView)base.Resources["SingleRowGridView"];
				break;
			}
		}

		private void ValidateDrag(object sender, EventArgs e)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null && sender is DragAndDropListViewItem { DataContext: DecoratedRevision dataContext } dragAndDropListViewItem)
			{
				bool flag = ActiveBranchContainsMergeCommits(dataContext.Sha, repositoryData);
				dragAndDropListViewItem.AllowDrag = dataContext.IsReachable && !flag;
			}
		}

		private void RevisionListViewItem_Drop(object sender, DragEventArgs e)
		{
			// 阶段 4.5：WPF e.Data.GetData(Type) → Avalonia e.Data.Get(string)（参考 FileListTreeView）。
			if (!(sender is DragAndDropListViewItem { DataContext: DecoratedRevision dataContext } dragAndDropListViewItem) || !(e.Data.Get(typeof(DecoratedRevision[]).FullName) is DecoratedRevision[] array) || array.Length == 0 || array.Contains(dataContext))
			{
				e.Handled = true;
				return;
			}
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			if (repositoryUserControl != null)
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						RevisionStorage revisionStorage = repositoryData.RevisionStorage;
						if (revisionStorage != null)
						{
							LocalBranch activeBranch = repositoryData.References.ActiveBranch;
							if (activeBranch != null)
							{
								DecoratedRevision decoratedRevision = array[0];
								if (!decoratedRevision.IsReachable || !dataContext.IsReachable)
								{
									e.Handled = true;
									return;
								}
								switch (dragAndDropListViewItem.DropPosition)
								{
								case ForkPlus.UI.Controls.DropPosition.Over:
									RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteDragAndDropFixup(repositoryUserControl, activeBranch, decoratedRevision.Sha, dataContext.Sha);
									break;
								case ForkPlus.UI.Controls.DropPosition.Top:
									RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteDragAndDropMove(repositoryUserControl, activeBranch, decoratedRevision.Sha, dataContext.Sha);
									break;
								case ForkPlus.UI.Controls.DropPosition.Bottom:
								{
									Revision parentRevision = revisionStorage.GetParentRevision(gitModule, dataContext.Sha);
									if (parentRevision == null)
									{
										Log.Error("Cannot find parent for " + dataContext.Sha);
									}
									else
									{
										RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteDragAndDropMove(repositoryUserControl, activeBranch, decoratedRevision.Sha, parentRevision.Sha);
									}
									break;
								}
								}
								return;
							}
						}
					}
				}
			}
			e.Handled = true;
		}

		private IEnumerable<Control> CreateMultipleRevisionsContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, DecoratedRevision[] selectedRevisions)
		{
			if (selectedRevisions.Length == 2)
			{
				yield return RepositoryUserControl.Commands.ShowSaveRevisionsAsPatchWindow.CreateMenuItem("Save Commit Range as Patch…", delegate
				{
					RepositoryUserControl.Commands.ShowSaveRevisionsAsPatchWindow.Execute(repositoryUserControl, gitModule, selectedRevisions.Map((DecoratedRevision x) => x.ToRevision()));
				});
				yield return new Separator();
				if (AiAgent.GetAvailableAiAgents().Length != 0 || OpenAiService.IsAiReviewConfigured())
				{
					yield return CreateRevisionRangeAiCodeReviewMenuItem(repositoryUserControl, selectedRevisions);
					yield return new Separator();
				}
			}
			bool isEnabled = selectedRevisions.AllItems((DecoratedRevision x) => x.GetParents().Length == 1);
			yield return RepositoryUserControl.Commands.ShowCherryPickWindow.CreateMenuItem("Cherry-pick…", delegate
			{
				RepositoryUserControl.Commands.ShowCherryPickWindow.Execute(RepositoryUserControl, SortRevisionsByRows(selectedRevisions));
			}, isEnabled);
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch != null && selectedRevisions.AllItems((DecoratedRevision x) => x.IsReachable))
			{
				yield return new Separator();
				DecoratedRevision[] sortedRevisions = SortRevisionsByRows(selectedRevisions);
				Revision[] revisions = sortedRevisions.Map((DecoratedRevision x) => x.ToRevision());
				if (RevisionsAreContinuous(sortedRevisions))
				{
					yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Squash into Parent…", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteSquash(RepositoryUserControl, gitModule, activeBranch, revisions);
					});
				}
				yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Drop…", delegate
				{
					RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteDrop(RepositoryUserControl, gitModule, activeBranch, revisions);
				});
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(SortRevisionsByRows(selectedRevisions).Map((DecoratedRevision x) => x.ToRevision()));
			});
			yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(SortRevisionsByRows(selectedRevisions).Map((DecoratedRevision x) => x.ToRevision()));
			});
		}

		private IEnumerable<Control> CreateRevisionContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, DecoratedRevision selectedRevision, CommitGraphCache commitGraphCache)
		{
			if (selectedRevision == null)
			{
				yield break;
			}
			foreach (Control item in CreateReferenceMenuItems(repositoryUserControl, gitModule, repositoryData, selectedRevision, commitGraphCache))
			{
				yield return item;
			}
			yield return RepositoryUserControl.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, selectedRevision.ToRevision());
			});
			yield return RepositoryUserControl.Commands.ShowCreateTagWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, selectedRevision.ToRevision());
			});
			yield return new Separator();
			if (repositoryData.GitFlowSettings != null)
			{
				IReadOnlyList<LocalBranch> gitFlowBranches = LocalGitFlowBranches(selectedRevision.References, repositoryData.GitFlowSettings);
				yield return CreateGitFlowMenuItem(repositoryUserControl, gitModule, repositoryData, repositoryData.GitFlowSettings, gitFlowBranches);
				yield return new Separator();
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch != null)
			{
				if (!selectedRevision.IsReachable || repositoryData.References.FilterReferences.Length != 0)
				{
					ForkPlus.Git.Reference[] references = selectedRevision.References?.CompactMap((ReferenceViewModel x) => x.Reference);
					ForkPlus.Git.Reference targetReference = System.Linq.Enumerable.FirstOrDefault(references ?? Array.Empty<ForkPlus.Git.Reference>(), (ForkPlus.Git.Reference x) => x is LocalBranch) ?? System.Linq.Enumerable.FirstOrDefault(references ?? Array.Empty<ForkPlus.Git.Reference>(), (ForkPlus.Git.Reference x) => x is Tag) ?? System.Linq.Enumerable.FirstOrDefault(references ?? Array.Empty<ForkPlus.Git.Reference>());
					if (targetReference != null && activeBranch.Sha != selectedRevision.Sha)
					{
						yield return RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, targetReference, activeBranch);
						});
					}
					yield return RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + activeBranch.Name + "' to Here...", delegate
					{
						RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, activeBranch, selectedRevision.ToRevision());
					});
				}
				bool flag = ActiveBranchContainsMergeCommits(selectedRevision.Sha, repositoryData);
				if (!selectedRevision.IsReachable || flag)
				{
					yield return RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Interactively Rebase '" + activeBranch.Name + "' to Here...", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, activeBranch, selectedRevision.ToRevision());
					});
				}
				else
				{
					MenuItem menuItem = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Interactive Rebase")
					};
					bool flag2 = selectedRevision.GetParents().Length == 0;
					MenuItem newItem = RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.CreateMenuItem("Interactively Rebase '" + activeBranch.Name + "' to Here...", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, activeBranch, selectedRevision.ToRevision());
					});
					menuItem.Items.Add(newItem);
					menuItem.Items.Add(new Separator());
					menuItem.Items.Add(new HeaderMenuItem("Quick Actions"));
					menuItem.Items.Add(new Separator());
					MenuItem menuItem2 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Reword...")
					};
					menuItem2.Click += delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteReword(repositoryUserControl, gitModule, activeBranch, selectedRevision.ToRevision());
					};
					menuItem.Items.Add(menuItem2);
					MenuItem menuItem3 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Edit...")
					};
					menuItem3.Click += delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteEdit(repositoryUserControl, gitModule, activeBranch, selectedRevision.ToRevision());
					};
					menuItem.Items.Add(menuItem3);
					MenuItem menuItem4 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Squash into Parent...")
					};
					menuItem4.Click += delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteSquash(repositoryUserControl, gitModule, activeBranch, new Revision[1] { selectedRevision.ToRevision() });
					};
					menuItem4.IsEnabled = !flag2;
					menuItem.Items.Add(menuItem4);
					MenuItem menuItem5 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Fixup into Parent…")
					};
					menuItem5.Click += delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteFixup(repositoryUserControl, gitModule, activeBranch, selectedRevision.ToRevision());
					};
					menuItem5.IsEnabled = !flag2;
					menuItem.Items.Add(menuItem5);
					MenuItem menuItem6 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Drop…")
					};
					menuItem6.Click += delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.ExecuteDrop(repositoryUserControl, gitModule, activeBranch, new Revision[1] { selectedRevision.ToRevision() });
					};
					menuItem6.IsEnabled = !flag2;
					menuItem.Items.Add(menuItem6);
					yield return menuItem;
				}
				yield return new Separator();
			}
			if (!selectedRevision.IsHead)
			{
				string text = repositoryData.References.ActiveBranch?.Name ?? "HEAD";
				yield return RepositoryUserControl.Commands.ShowResetBranchWindow.CreateMenuItem("Reset '" + text + "' to Here...", delegate
				{
					RepositoryUserControl.Commands.ShowResetBranchWindow.Execute(repositoryUserControl, repositoryData.References.ActiveBranch, selectedRevision.ToRevision());
				});
				yield return new Separator();
			}
			yield return RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.Execute(repositoryUserControl, selectedRevision.ToRevision(), selectedRevision.Sha);
			});
			yield return RepositoryUserControl.Commands.ShowCherryPickWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCherryPickWindow.Execute(repositoryUserControl, new DecoratedRevision[1] { selectedRevision });
			});
			yield return RepositoryUserControl.Commands.ShowRevertRevisionWindow.CreateMenuItem(delegate
		{
			RepositoryUserControl.Commands.ShowRevertRevisionWindow.Execute(repositoryUserControl, selectedRevision);
		});
		yield return CreateAiExplainRevisionMenuItem(repositoryUserControl, gitModule, selectedRevision.Sha);
		yield return RepositoryUserControl.Commands.ShowSaveRevisionsAsPatchWindow.CreateMenuItem(delegate
		{
			RepositoryUserControl.Commands.ShowSaveRevisionsAsPatchWindow.Execute(repositoryUserControl, gitModule, new Revision[1] { selectedRevision.ToRevision() });
		});
		yield return new Separator();
	yield return RepositoryUserControl.Commands.CompareRevisionToWorkingDirectory.CreateMenuItem(delegate
	{
		RepositoryUserControl.Commands.CompareRevisionToWorkingDirectory.Execute(selectedRevision.Sha);
	});
	yield return new Separator();
		yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
		{
			RepositoryUserControl.Commands.CopyRevisionSha.Execute(new Revision[1] { selectedRevision.ToRevision() });
		});
			yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(new Revision[1] { selectedRevision.ToRevision() });
			});
			CustomCommand[] revisionCustomCommands = CustomCommandManager.Current.GetCustomCommands(repositoryData, CustomCommandTarget.Revision);
			if (revisionCustomCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> list = new List<MenuItem>();
				foreach (CustomCommand customCommand in revisionCustomCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, selectedRevision.Sha);
						customCommand.AddCustomCommandItem(repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, list);
					}
				}
				foreach (MenuItem item2 in list)
				{
					yield return item2;
				}
			}
		}

		private bool ActiveBranchContainsMergeCommits(Sha targetRevisionSha, RepositoryData repositoryData)
		{
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				return false;
			}
			bool result = false;
			HashSet<Sha> hashSet = new HashSet<Sha>();
			HashSet<Sha> hashSet2 = new HashSet<Sha>();
			hashSet.Add(targetRevisionSha);
			hashSet2.Add(activeBranch.Sha);
			RevisionStorage revisionStorage = repositoryData.RevisionStorage;
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				Sha sha = revisionStorage.GetSha(current);
				ShaBufferIterator parents = revisionStorage.GetParents(current);
				if (hashSet2.Contains(sha))
				{
					if (parents.Length > 1)
					{
						result = true;
					}
					if (hashSet.Contains(sha))
					{
						return result;
					}
					hashSet2.Remove(sha);
					ShaBufferIterator.Enumerator enumerator2 = parents.GetEnumerator();
					while (enumerator2.MoveNext())
					{
						Sha current2 = enumerator2.Current;
						hashSet2.Add(current2);
					}
				}
				else if (hashSet.Contains(sha))
				{
					hashSet.Remove(sha);
					ShaBufferIterator.Enumerator enumerator2 = parents.GetEnumerator();
					while (enumerator2.MoveNext())
					{
						Sha current3 = enumerator2.Current;
						hashSet.Add(current3);
					}
				}
			}
			return result;
		}

		private static int SortBranchesByGroupsLocalFirst(Branch l, Branch r)
		{
			if (l is RemoteBranch remoteBranch && r is LocalBranch { UpstreamFullReference: var upstreamFullReference } localBranch)
			{
				int num = ((upstreamFullReference == null) ? NaturalStringComparer.Instance.Compare(remoteBranch.ShortName, localBranch.Name) : NaturalStringComparer.Instance.Compare(remoteBranch.FullReference, upstreamFullReference));
				if (num != 0)
				{
					return num;
				}
				return 1;
			}
			if (l is LocalBranch localBranch2 && r is RemoteBranch remoteBranch2)
			{
				string upstreamFullReference2 = localBranch2.UpstreamFullReference;
				int num2 = ((upstreamFullReference2 == null) ? NaturalStringComparer.Instance.Compare(localBranch2.Name, remoteBranch2.ShortName) : NaturalStringComparer.Instance.Compare(upstreamFullReference2, remoteBranch2.FullReference));
				if (num2 != 0)
				{
					return num2;
				}
				return -1;
			}
			return NaturalStringComparer.Instance.Compare(l.FullReference, r.FullReference);
		}

		private IEnumerable<Control> CreateReferenceMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, DecoratedRevision selectedRevision, CommitGraphCache commitGraphCache)
		{
			ForkPlus.Git.Reference[] references = selectedRevision.References?.Map((ReferenceViewModel x) => x.Reference);
			if (references == null)
			{
				yield break;
			}
			Branch[] allBranches = references.CompactMap((ForkPlus.Git.Reference x) => x as Branch);
			Array.Sort(allBranches, SortBranchesByGroupsLocalFirst);
			foreach (Branch branch in allBranches)
			{
				yield return CreateBranchMenuItem(repositoryUserControl, branch, gitModule, repositoryData, commitGraphCache);
			}
			if (allBranches.Length != 0)
			{
				yield return new Separator();
			}
			Tag[] allTags = references.CompactMap((ForkPlus.Git.Reference x) => x as Tag);
			foreach (Tag tag in allTags)
			{
				yield return CreateTagMenuItem(tag, gitModule, repositoryUserControl, repositoryData);
			}
			if (allTags.Length != 0)
			{
				yield return new Separator();
			}
		}

		private static Control CreateBranchMenuItem(RepositoryUserControl repositoryUserControl, Branch branch, GitModule gitModule, RepositoryData repositoryData, CommitGraphCache commitGraphCache)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = branch.Name.Replace("_", "__")
			};
			menuItem.Items.Add(new HeaderMenuItem(branch.Name));
			menuItem.Items.Add(CreateReferenceButtonsMenuItem(repositoryUserControl, gitModule, repositoryData.References, branch));
			if (branch is RemoteBranch remoteBranch)
			{
				foreach (Control item in CreateRemoteBranchSubMenuItems(repositoryUserControl, remoteBranch, gitModule, repositoryData, commitGraphCache))
				{
					menuItem.Items.Add(item);
				}
			}
			else if (branch is LocalBranch localBranch)
			{
				foreach (Control item2 in CreateLocalBranchSubMenuItems(repositoryUserControl, localBranch, gitModule, repositoryData, commitGraphCache))
				{
					menuItem.Items.Add(item2);
				}
			}
			return menuItem;
		}

		private static IEnumerable<Control> CreateRemoteBranchSubMenuItems(RepositoryUserControl repositoryUserControl, RemoteBranch remoteBranch, GitModule gitModule, RepositoryData repositoryData, CommitGraphCache commitGraphCache)
		{
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowCheckoutBranchWindow.CreateMenuItem("Checkout", delegate
			{
				RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, remoteBranch);
			});
			Remote remote = IReadOnlyListExtensions.FirstItem(repositoryData.Remotes.Items, (Remote x) => x.Name == remoteBranch.Remote);
			if (remote != null)
			{
				string pullRequestUrl = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(remoteBranch.ShortName);
				if (pullRequestUrl != null)
				{
					yield return RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem("Create Pull Request on '" + remote.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
					});
				}
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch != null)
			{
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, remoteBranch, activeBranch);
				});
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.CreateMenuItem("Delete...", delegate
			{
				RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(repositoryUserControl, new RemoteBranch[1] { remoteBranch });
			});
			if (AiAgent.GetAvailableAiAgents().Length != 0 || OpenAiService.IsAiReviewConfigured())
			{
				LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
				if (localBranch != null)
				{
					RemoteBranch remoteMain = repositoryData.References.Upstream(localBranch);
					if (remoteMain != null)
					{
						yield return new Separator();
						yield return CreateBranchAiCodeReviewMenuItem(repositoryUserControl, gitModule, remoteBranch, remoteMain, commitGraphCache);
					}
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Branch Name", delegate
			{
				RepositoryUserControl.Commands.CopyReferenceName.Execute(remoteBranch);
			});
			foreach (Control item in GetReferenceCustomCommandMenuItems(repositoryUserControl, gitModule, remoteBranch))
			{
				yield return item;
			}
		}

		private static IEnumerable<Control> CreateLocalBranchSubMenuItems(RepositoryUserControl repositoryUserControl, LocalBranch localBranch, GitModule gitModule, RepositoryData repositoryData, CommitGraphCache commitGraphCache)
		{
			yield return new Separator();
			RemoteBranch upstreamBranch = null;
			string upstreamFullReference = localBranch.UpstreamFullReference;
			if (upstreamFullReference != null)
			{
				RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
				if (remoteBranch != null)
				{
					upstreamBranch = remoteBranch;
				}
			}
			if (!localBranch.IsActive)
			{
				if (repositoryData.Worktrees.WorktreesByFullReference.TryGetValue(localBranch.FullReference, out Worktree branchWorktree))
				{
					if (!branchWorktree.IsMain)
					{
						yield return RepositoryUserControl.Commands.OpenWorktree.CreateMenuItem("Open '" + branchWorktree.FriendlyName + "' Worktree", delegate
						{
							RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { branchWorktree });
						});
						yield return new Separator();
					}
				}
				else
				{
					yield return RepositoryUserControl.Commands.ShowCheckoutBranchWindow.CreateMenuItem("Checkout", delegate
					{
						RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, localBranch);
					});
					if (repositoryData.Worktrees.IsEnabled)
					{
						yield return MainWindow.Commands.ShowCheckoutBranchAsWorktreeWindow.CreateMenuItem("Checkout as Worktree...", delegate
						{
							MainWindow.Commands.ShowCheckoutBranchAsWorktreeWindow.Execute(repositoryUserControl, localBranch);
						});
					}
					yield return new Separator();
				}
			}
			bool hasInactiveBranchWorktree = repositoryData.Worktrees.WorktreesByFullReference.TryGetValue(localBranch.FullReference, out Worktree branchWorktreeValue) && !branchWorktreeValue.IsActive;
			bool? isLocalBranchInfrontUpstream = (upstreamBranch != null) ? localBranch.IsInfrontUpstream(upstreamBranch, gitModule, commitGraphCache) : null;
			if (upstreamBranch != null)
			{
				if (isLocalBranchInfrontUpstream == false)
				{
					yield return RepositoryUserControl.Commands.FastForward.CreateMenuItem("Fast-Forward to '" + upstreamBranch.Name + "'", delegate
					{
						RepositoryUserControl.Commands.FastForward.Execute(repositoryUserControl, localBranch);
					}, !hasInactiveBranchWorktree);
				}
				else
				{
					yield return RepositoryUserControl.Commands.FastForwardPull.CreateMenuItem("Fast-Forward to '" + upstreamBranch.Name + "'", delegate
					{
						RepositoryUserControl.Commands.FastForwardPull.Execute(repositoryUserControl, localBranch);
					}, !hasInactiveBranchWorktree);
				}
				if (localBranch.IsActive)
				{
					yield return RepositoryUserControl.Commands.FastForward.CreateMenuItem("Pull '" + upstreamBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPullWindow.Execute(repositoryUserControl, upstreamBranch);
					});
				}
			}
			if (repositoryData.Remotes.Items.Length == 1)
			{
				Remote remote = repositoryData.Remotes.Items[0];
				yield return RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push '" + localBranch.Name + "' to '" + remote.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowPushBranchWindow.Execute(repositoryUserControl, remote, localBranch);
				});
				string branch = upstreamBranch?.ShortName ?? localBranch.Name;
				string pullRequestUrl = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(branch);
				if (pullRequestUrl != null)
				{
					bool pushRequired = upstreamBranch == null || isLocalBranchInfrontUpstream.GetValueOrDefault();
					string header = pushRequired ? PreferencesLocalization.FormatCurrent("Push and Create Pull Request on '{0}'...", remote.Name) : PreferencesLocalization.FormatCurrent("Create Pull Request on '{0}'...", remote.Name);
					yield return RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem(header, delegate
					{
						if (pushRequired)
						{
							RepositoryUserControl.Commands.CreatePullRequest.Execute(repositoryUserControl, localBranch, upstreamBranch, remote.Name, pullRequestUrl);
						}
						else
						{
							RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
						}
					});
				}
			}
			else if (repositoryData.Remotes.Items.Length > 1)
			{
				MenuItem menuItem = RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem();
				MenuItem pullRequestMenuItem = RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Create Pull Request");
				Remote[] array = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
				foreach (Remote remote in array)
				{
					MenuItem newItem = RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push to '" + remote.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushBranchWindow.Execute(repositoryUserControl, remote, localBranch);
					});
					menuItem.Items.Add(newItem);
					string branch = upstreamBranch?.ShortName ?? localBranch.Name;
					string pullRequestUrl = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(branch);
					if (pullRequestUrl == null)
					{
						continue;
					}
					RemoteBranch matchingUpstreamBranch = IReadOnlyListExtensions.FirstItem(repositoryData?.References.RemoteBranches, (RemoteBranch x) => x.FullReference == localBranch.UpstreamFullReference && x.Remote == remote.Name);
					bool pushRequired = matchingUpstreamBranch == null || isLocalBranchInfrontUpstream.GetValueOrDefault();
					string header = pushRequired ? PreferencesLocalization.FormatCurrent("Push and Create Pull Request on '{0}'...", remote.Name) : PreferencesLocalization.FormatCurrent("Create Pull Request on '{0}'...", remote.Name);
					MenuItem newItem2 = RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem(header, delegate
					{
						if (pushRequired)
						{
							RepositoryUserControl.Commands.CreatePullRequest.Execute(repositoryUserControl, localBranch, upstreamBranch, remote.Name, pullRequestUrl);
						}
						else
						{
							RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
						}
					});
					pullRequestMenuItem.Items.Add(newItem2);
				}
				yield return menuItem;
				if (pullRequestMenuItem.Items.Count > 0)
				{
					yield return pullRequestMenuItem;
				}
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch != null)
			{
				yield return new Separator();
				bool isEnabled = localBranch != activeBranch;
				yield return RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, localBranch, activeBranch);
				}, isEnabled);
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.CreateMenuItem("Rename...", delegate
			{
				RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(repositoryUserControl, gitModule, repositoryData.References, localBranch);
			});
			if (!localBranch.IsActive)
			{
				yield return RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.CreateMenuItem("Delete...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, new LocalBranch[1] { localBranch });
				});
			}
			if (AiAgent.GetAvailableAiAgents().Length != 0 || OpenAiService.IsAiReviewConfigured())
			{
				LocalBranch localMain = repositoryData.References.LocalMain(gitModule);
				if (localMain != null)
				{
					RemoteBranch remoteMain = repositoryData.References.Upstream(localMain);
					if (remoteMain != null)
					{
						yield return new Separator();
						yield return CreateBranchAiCodeReviewMenuItem(repositoryUserControl, gitModule, localBranch, remoteMain, commitGraphCache);
					}
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Branch Name", delegate
			{
				RepositoryUserControl.Commands.CopyReferenceName.Execute(localBranch);
			});
			foreach (Control item in GetReferenceCustomCommandMenuItems(repositoryUserControl, gitModule, localBranch))
			{
				yield return item;
			}
		}

		private static Control CreateTagMenuItem(Tag tag, GitModule gitModule, RepositoryUserControl repositoryUserControl, RepositoryData repositoryData)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = tag.Name.Replace("_", "__")
			};
			menuItem.Items.Add(new HeaderMenuItem(tag.Name));
			menuItem.Items.Add(CreateReferenceButtonsMenuItem(repositoryUserControl, gitModule, repositoryData.References, tag));
			Remote[] remotes = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			foreach (Control item in CreateTagSubMenuItems(tag, repositoryUserControl, gitModule, remotes, repositoryData))
			{
				menuItem.Items.Add(item);
			}
			return menuItem;
		}

		private static IEnumerable<Control> CreateTagSubMenuItems(Tag tag, RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote[] remotes, RepositoryData repositoryData)
		{
			yield return new Separator();
			if (tag.TargetObjectSha.HasValue)
			{
				yield return RepositoryUserControl.Commands.ShowTagDetailsWindow.CreateMenuItem("Show '" + tag.Name + "' Details...", delegate
				{
					RepositoryUserControl.Commands.ShowTagDetailsWindow.Execute(repositoryUserControl, tag);
				});
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch != null)
			{
				yield return RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, tag, activeBranch);
				});
				yield return new Separator();
			}
			yield return RepositoryUserControl.Commands.ShowRemoveTagWindow.CreateMenuItem("Delete '" + tag.Name + "'...", delegate
			{
				RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(repositoryUserControl, new Tag[1] { tag });
			});
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Tag Name", delegate
			{
				RepositoryUserControl.Commands.CopyReferenceName.Execute(tag);
			});
			if (remotes.Length == 1)
			{
				yield return new Separator();
				Remote remote = remotes[0];
				yield return RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem("Push '" + tag.Name + "' to '" + remote.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowPushTagWindowCommand.Execute(repositoryUserControl, tag, remote);
				});
			}
			else if (remotes.Length > 1)
			{
				yield return new Separator();
				MenuItem menuItem = RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem();
				foreach (Remote remote in remotes)
				{
					MenuItem newItem = RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem("Push to '" + remote.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushTagWindowCommand.Execute(repositoryUserControl, tag, remote);
					});
					menuItem.Items.Add(newItem);
				}
				yield return menuItem;
			}
			foreach (Control item in GetReferenceCustomCommandMenuItems(repositoryUserControl, gitModule, tag))
			{
				yield return item;
			}
		}

		public static Control CreateBranchAiCodeReviewMenuItem(RepositoryUserControl rc, GitModule gitModule, Branch branch, RemoteBranch remoteMain, CommitGraphCache commitGraphCache)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("AI");
			bool isEnabled = branch.IsAhead(remoteMain, gitModule, commitGraphCache);
			AiAgent[] availableAiAgents = AiAgent.GetAvailableAiAgents();
			foreach (AiAgent aiAgent in availableAiAgents)
			{
				MenuItem menuItem2 = new MenuItem();
				menuItem2.Header = PreferencesLocalization.FormatMenuHeader("Code Review with {0}...", aiAgent.Name);
				menuItem2.IsEnabled = isEnabled;
				menuItem2.Click += delegate
				{
					AiCodeReviewTarget aiCodeReviewTarget2 = CreateBranchTarget(rc, remoteMain, branch);
					if (aiCodeReviewTarget2 != null)
					{
						RepositoryUserControl.Commands.ShowAiResultWindow.Execute(rc, aiCodeReviewTarget2, aiAgent);
					}
				};
				menuItem.Items.Add(menuItem2);
			}
			if (OpenAiService.IsAiReviewConfigured())
			{
				MenuItem menuItem3 = new MenuItem();
				menuItem3.Header = PreferencesLocalization.FormatMenuHeader("Code Review with {0}...", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI");
				menuItem3.IsEnabled = isEnabled;
				menuItem3.Click += delegate
				{
					AiCodeReviewTarget aiCodeReviewTarget = CreateBranchTarget(rc, remoteMain, branch);
					if (aiCodeReviewTarget != null)
					{
						RepositoryUserControl.Commands.ShowAiResultWindow.Execute(rc, aiCodeReviewTarget);
					}
				};
				menuItem.Items.Add(menuItem3);
				// AI 生成 PR 描述（基于 merge base..branch 的 commit 列表 + 聚合 diff）
				MenuItem prDescItem = new MenuItem();
				prDescItem.Header = PreferencesLocalization.FormatMenuHeader("Generate PR Description with {0}...", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI");
				prDescItem.IsEnabled = isEnabled;
				prDescItem.Click += delegate
				{
					AiCodeReviewTarget.Branch target = CreateBranchTarget(rc, remoteMain, branch);
					if (target != null)
					{
						GeneratePullRequestDescription(rc, gitModule, target.Src, target.Dst, branch.Name);
					}
				};
				menuItem.Items.Add(new Separator());
				menuItem.Items.Add(prDescItem);
			}
			return menuItem;
		}

		[Null]
		private static AiCodeReviewTarget.Branch CreateBranchTarget(RepositoryUserControl repositoryUserControl, RemoteBranch remoteMain, Branch branch)
		{
			GitCommandResult<Sha> gitCommandResult = new GetMergeBaseGitCommand().Execute(repositoryUserControl.GitModule, remoteMain.Sha, branch.Sha);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
				return null;
			}
			return new AiCodeReviewTarget.Branch(gitCommandResult.Result, branch);
		}

		/// <summary>AI 生成 PR 描述：拉取 src..dst 的 commit log 和聚合 diff，打开 AiTextResultWindow 流式展示。</summary>
		private static void GeneratePullRequestDescription(RepositoryUserControl repositoryUserControl, GitModule gitModule, Sha src, Sha dst, string name)
		{
			if (gitModule == null || src == null || dst == null)
			{
				return;
			}
			string srcStr = src.ToString();
			string dstStr = dst.ToString();
			string rangeLabel = name ?? dst.ToAbbreviatedString();

			AiTextResultWindow window = new AiTextResultWindow();
			window.Owner = Application.Current?.MainWindow;
			string title = PreferencesLocalization.FormatCurrent("AI PR Description: {0}", rangeLabel);
			window.Show();
			window.StartStreaming(title, delegate(AiTextResultWindow w, JobMonitor monitor)
			{
				try
				{
					// 拉取 commit log（含 subject + body）
					GitCommand logCmd = new GitCommand("--no-pager", "log", "--no-color", "--no-decorate", "--pretty=format:* %s%n%b%n", srcStr + ".." + dstStr);
					GitRequestResult logResult = new GitRequest(gitModule).Command(logCmd).Execute();
					string commitLog = "";
					if (logResult.ExitCode < 2)
					{
						commitLog = logResult.Stdout;
						const int maxLogChars = 20000;
						if (commitLog.Length > maxLogChars)
						{
							commitLog = commitLog.Substring(0, maxLogChars) + "\n... (log truncated)\n";
						}
					}
					// 拉取聚合 diff
					GitCommand diffCmd = new GitCommand("--no-pager", "diff", "--no-color", "--find-renames", "--no-ext-diff", "--submodule=short", "--unified=10", srcStr + ".." + dstStr);
					GitRequestResult diffResult = new GitRequest(gitModule).Command(diffCmd).Execute();
					string aggregatedDiff = "";
					if (diffResult.ExitCode < 2)
					{
						aggregatedDiff = diffResult.Stdout;
					}
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.GeneratePullRequestDescription(commitLog, aggregatedDiff, monitor, w.OnChunk);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						w.OnError(response.Error.FriendlyMessage);
					}
					else
					{
						w.OnSuccess(response.Result.Message);
					}
				}
				catch (Exception ex)
				{
					w.OnError(ex.Message);
				}
			});
	}

		/// <summary>AI 解释单个 commit：拉取 commit subject/body/diff，打开 AiTextResultWindow 流式展示 AI 解释。
		/// 供 commit 列表右键菜单和 stash 列表右键菜单共用。</summary>
		private static void AiExplainRevision(RepositoryUserControl repositoryUserControl, GitModule gitModule, Sha sha)
		{
			if (gitModule == null || sha == null)
			{
				return;
			}
			if (!OpenAiService.IsAiReviewConfigured())
			{
				// 阶段 4.5：WPF MessageBox.Show → ServiceLocator.MessageBox.Show（参考 CheckForkSyncCommand）。
				ServiceLocator.MessageBox.Show(
					PreferencesLocalization.Translate("AI is not configured. Please configure AI review settings in Preferences first.", ForkPlusSettings.Default.UiLanguage),
					PreferencesLocalization.Translate("AI Explain Commit", ForkPlusSettings.Default.UiLanguage),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}
			string shaStr = sha.ToString();
			string abbreviatedSha = sha.ToAbbreviatedString();

			AiTextResultWindow window = new AiTextResultWindow();
			window.Owner = Application.Current?.MainWindow;
			string title = PreferencesLocalization.FormatCurrent("AI Explain {0}", abbreviatedSha);
			window.Show();
			window.StartStreaming(title, delegate(AiTextResultWindow w, JobMonitor monitor)
			{
				try
				{
					// 拉 commit 的 subject + body（与 AiExplainCommitButton_Click 同样模式）
					GitCommand logCmd = new GitCommand("--no-pager", "log", "-1", "--no-color", "--no-decorate", "--pretty=format:%s%n%n%b", shaStr);
					GitRequestResult logResult = new GitRequest(gitModule).Command(logCmd).Execute();
					string commitSubject = "";
					string commitBody = "";
					if (logResult.ExitCode < 2)
					{
						string msg = logResult.Stdout ?? "";
						int idx = msg.IndexOf("\n\n", StringComparison.Ordinal);
						if (idx >= 0)
						{
							commitSubject = msg.Substring(0, idx).Trim();
							commitBody = msg.Substring(idx + 2).Trim();
						}
						else
						{
							commitSubject = msg.Trim();
						}
					}
					// 拉 commit 的 patch（含变更文件和差异内容）
					GitCommand showCmd = new GitCommand("--no-pager", "show", "--no-color", "--find-renames", "--submodule=short", "--unified=50", "--no-ext-diff", shaStr);
					GitRequestResult showResult = new GitRequest(gitModule).Command(showCmd).Execute();
					string diffSummary = "";
					if (showResult.ExitCode < 2)
					{
						diffSummary = showResult.Stdout;
						// 限制 diff 体量，避免 token 爆炸
						const int maxDiffChars = 20000;
						if (diffSummary.Length > maxDiffChars)
						{
							diffSummary = diffSummary.Substring(0, maxDiffChars) + "\n... (diff truncated)\n";
						}
					}
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.ExplainCommit(commitSubject, commitBody, diffSummary, monitor, w.OnChunk);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						w.OnError(response.Error.FriendlyMessage);
					}
					else
					{
						w.OnSuccess(response.Result.Message);
					}
				}
				catch (Exception ex)
				{
					w.OnError(ex.Message);
				}
			});
		}

		/// <summary>创建 "AI Explain Commit..." 菜单项。仅在 AI 配置完毕时启用。</summary>
		private static Control CreateAiExplainRevisionMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, Sha sha)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("AI Explain Commit...");
			menuItem.IsEnabled = OpenAiService.IsAiReviewConfigured() && sha != null && gitModule != null;
			menuItem.Click += delegate
			{
				AiExplainRevision(repositoryUserControl, gitModule, sha);
			};
			return menuItem;
		}


		private static Control CreateRevisionRangeAiCodeReviewMenuItem(RepositoryUserControl repositoryUserControl, DecoratedRevision[] decoratedRevisions)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("AI");
			bool isEnabled = decoratedRevisions.Length == 2;
			AiAgent[] availableAiAgents = AiAgent.GetAvailableAiAgents();
			foreach (AiAgent aiAgent in availableAiAgents)
			{
				MenuItem menuItem2 = new MenuItem();
				menuItem2.Header = PreferencesLocalization.FormatMenuHeader("Code Review with {0}...", aiAgent.Name);
				menuItem2.IsEnabled = isEnabled;
				menuItem2.Click += delegate
				{
					Revision[] array2 = SortRevisionsByRows(decoratedRevisions).Map((DecoratedRevision x) => x.ToRevision());
					RepositoryUserControl.Commands.ShowAiResultWindow.Execute(repositoryUserControl, new AiCodeReviewTarget.ShaRange(array2[1].Sha, array2[0].Sha), aiAgent);
				};
				menuItem.Items.Add(menuItem2);
			}
			if (OpenAiService.IsAiReviewConfigured())
			{
				MenuItem menuItem3 = new MenuItem();
				menuItem3.Header = PreferencesLocalization.FormatMenuHeader("Code Review with {0}...", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI");
				menuItem3.Click += delegate
				{
					Revision[] array = SortRevisionsByRows(decoratedRevisions).Map((DecoratedRevision x) => x.ToRevision());
					RepositoryUserControl.Commands.ShowAiResultWindow.Execute(repositoryUserControl, new AiCodeReviewTarget.ShaRange(array[1].Sha, array[0].Sha));
				};
				menuItem.Items.Add(menuItem3);
				// AI 生成 PR 描述（基于 src..dst 的 commit 列表 + 聚合 diff）
				MenuItem prDescItem = new MenuItem();
				prDescItem.Header = PreferencesLocalization.FormatMenuHeader("Generate PR Description with {0}...", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI");
				prDescItem.IsEnabled = isEnabled;
				prDescItem.Click += delegate
				{
					Revision[] array3 = SortRevisionsByRows(decoratedRevisions).Map((DecoratedRevision x) => x.ToRevision());
					Sha src = array3[1].Sha;
					Sha dst = array3[0].Sha;
					GeneratePullRequestDescription(repositoryUserControl, repositoryUserControl.GitModule, src, dst, dst.ToAbbreviatedString());
				};
				menuItem.Items.Add(new Separator());
				menuItem.Items.Add(prDescItem);
			}
			return menuItem;
		}

		private static IEnumerable<Control> GetReferenceCustomCommandMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, ForkPlus.Git.Reference reference)
		{
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.Reference);
			IReadOnlyList<CustomCommand> result = null;
			if (reference is LocalBranch)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.LocalBranch) ?? false);
			}
			else if (reference is RemoteBranch)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.RemoteBranch) ?? false);
			}
			else if (reference is Tag)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.Tag) ?? false);
			}
			if (result == null || result.Count == 0)
			{
				yield break;
			}
			yield return new Separator();
			yield return new HeaderMenuItem("Custom Commands");
			List<MenuItem> list = new List<MenuItem>();
			foreach (CustomCommand item in result)
			{
				if (item.OS.IsSupported())
				{
					CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, reference);
					item.AddCustomCommandItem(repositoryUserControl, env, item.Name.Split(Consts.Chars.Slash), 0, list);
				}
			}
			foreach (MenuItem item in list)
			{
				yield return item;
			}
		}

		private Control CreateGitFlowMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, GitFlowSettings gitFlowSettings, IReadOnlyList<LocalBranch> gitFlowBranches)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Git Flow")
			};
			foreach (LocalBranch gitFlowBranch in gitFlowBranches)
			{
				if (gitFlowBranch.IsFeatureBranch(gitFlowSettings))
				{
					menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
					}));
				}
				else if (gitFlowBranch.IsReleaseBranch(gitFlowSettings))
				{
					menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
					}));
				}
				else if (gitFlowBranch.IsHotfixBranch(gitFlowSettings))
				{
					menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
					}));
				}
			}
			if (menuItem.Items.Count > 0)
			{
				menuItem.Items.Add(new Separator());
			}
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.Execute(repositoryUserControl, gitModule);
			}));
			return menuItem;
		}

		private IEnumerable<Control> CreateStashContextMenuItems(RepositoryUserControl repositoryUserControl, StashRevision[] stashes)
		{
			StashRevision stash = stashes.SingleItem();
			if (stash != null)
			{
				yield return RepositoryUserControl.Commands.ShowApplyStashWindow.CreateMenuItem("Apply '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowApplyStashWindow.Execute(repositoryUserControl, stash);
				});
				yield return RepositoryUserControl.Commands.ShowRenameStashWindow.CreateMenuItem("Rename '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRenameStashWindow.Execute(repositoryUserControl, stash);
				});
				yield return RepositoryUserControl.Commands.ShowRemoveStashWindow.CreateMenuItem("Delete '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(repositoryUserControl, new StashRevision[1] { stash });
				});
				yield return new Separator();
			yield return RepositoryUserControl.Commands.CompareRevisionToWorkingDirectory.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CompareRevisionToWorkingDirectory.Execute(stash.Sha);
			});
			yield return CreateAiExplainRevisionMenuItem(repositoryUserControl, repositoryUserControl.GitModule, stash.Sha);
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
			{
				CopyRevisionShaCommand copyRevisionSha = RepositoryUserControl.Commands.CopyRevisionSha;
				Revision[] revisions = new StashRevision[1] { stash };
				copyRevisionSha.Execute(revisions);
			});
				yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
				{
					CopyRevisionInfoCommand copyRevisionInfo = RepositoryUserControl.Commands.CopyRevisionInfo;
					Revision[] revisions = new StashRevision[1] { stash };
					copyRevisionInfo.Execute(revisions);
				});
			}
			else
			{
				yield return RepositoryUserControl.Commands.ShowRemoveStashWindow.CreateMenuItem($"Delete {stashes.Length} Stashes...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(repositoryUserControl, stashes);
				});
			}
		}

		private IReadOnlyList<LocalBranch> LocalGitFlowBranches([Null] ReferenceViewModel[] references, GitFlowSettings gitFlowSettings)
		{
			if (references == null)
			{
				return new LocalBranch[0];
			}
			return references.CompactMap((ReferenceViewModel x) => x.Reference as LocalBranch).Filter((LocalBranch x) => x.IsFeatureBranch(gitFlowSettings) || x.IsReleaseBranch(gitFlowSettings) || x.IsHotfixBranch(gitFlowSettings));
		}

		private void RevisionSearchPanelUserControl_Closed(object sender, EventArgs e)
		{
			RefreshContextSearch(null);
			// 阶段 4.5：WPF MoveFocus(TraversalRequest) → 无 Avalonia 等价，注释（参考 MultiselectionTreeView）。
			// RevisionSearchPanelUserControl.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}

		private void RefreshContextSearch(string searchString)
		{
			_activeContextSearchJob?.Monitor.Cancel();
			_activeContextSearchJob = null;
			RepositoryUserControl.CancelActiveFetchRevisionsJobs();
			this.SearchQueryChanged?.Invoke(this, new EventArgs<RevisionSearchQuery>(new RevisionSearchQuery(RevisionSearchType.All, RevisionSearchScope.Repository, searchString)));
			if (!string.IsNullOrEmpty(searchString))
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
					if (repositoryData != null && RepositoryUserControl.CommitGraphCache != null)
					{
						List<Sha> shas = new List<Sha>(RevisionsDataSource.Count);
						for (int i = 0; i < RevisionsDataSource.Count; i++)
						{
							shas.Add(RevisionsDataSource.ShaAtRow(i));
						}
						Sha[] refMatches = repositoryData.References.Items.Filter((ForkPlus.Git.Reference x) => ReferenceMatch(x, searchString)).Map((ForkPlus.Git.Reference x) => x.Sha);
			_activeContextSearchJob = RepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Context search '{0}'", searchString), delegate(JobMonitor monitor)
						{
							if (!monitor.IsCanceled)
							{
								base.Dispatcher.Async(delegate
								{
									RevisionSearchPanelUserControl.IsBusyIndicatorVisible = true;
								});
								GitCommandResult<Sha[]> searchResult = new RevisionContextSearchGitCommand().Execute(gitModule, searchString, shas.ToArray(), refMatches, monitor);
								base.Dispatcher.Async(delegate
								{
									if (!monitor.IsCanceled)
									{
										RevisionContextSearch? revisionContextSearch;
										if (searchResult.Succeeded)
										{
											HashSet<Sha> hashSet = new HashSet<Sha>();
											Sha[] result = searchResult.Result;
											for (int j = 0; j < result.Length; j++)
											{
												hashSet.Add(result[j]);
											}
											revisionContextSearch = new RevisionContextSearch(searchString, hashSet);
										}
										else
										{
											revisionContextSearch = null;
											new ErrorWindow(searchResult.Error.FriendlyDescription).ShowDialog();
										}
										_activeContextSearchJob = null;
										RevisionSearchPanelUserControl.IsBusyIndicatorVisible = false;
										RevisionSearchPanelUserControl.UpdateMatchesCount(revisionContextSearch?.MatchCount ?? 0);
										RevisionsDataSource.SetContextSearch(revisionContextSearch);
										JumpToNextContextSearchResult(initialJump: true);
									}
								});
							}
						}, JobFlags.Hidden);
						return;
					}
				}
			}
			RevisionSearchPanelUserControl.IsBusyIndicatorVisible = false;
			RevisionsDataSource.SetContextSearch(null);
			RevisionSearchPanelUserControl.UpdateMatchesCount(null);
		}

		private void JumpToPreviousContextSearchResult()
		{
			int? num = RevisionsDataSource.PreviousContextSearchMatch(RevisionListView.SelectedIndex);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				RevisionListView.SelectAndScrollIntoView(valueOrDefault, focus: false);
			}
		}

		private void JumpToNextContextSearchResult(bool initialJump)
		{
			int num = ((RevisionListView.SelectedIndex >= 0) ? RevisionListView.SelectedIndex : 0);
			int? num2 = RevisionsDataSource.NextContextSearchMatch(num, initialJump);
			if (num2.HasValue)
			{
				int valueOrDefault = num2.GetValueOrDefault();
				RevisionListView.SelectAndScrollIntoView(valueOrDefault, focus: false);
			}
			else
			{
				RepositoryUserControl.FetchUntilFindContextSearchMatch(num);
			}
		}

		private void RefreshSidebarSearch(RevisionSearchQuery query)
		{
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			if (_activeSidebarSearchJob != null && _activeSidebarSearchJob.Status != JobStatus.Finished)
			{
				_activeSidebarSearchJob?.Monitor.Cancel();
			}
			_activeSidebarSearchJob = null;
			this.SearchQueryChanged?.Invoke(this, new EventArgs<RevisionSearchQuery>(query));
			SidebarSearchTabItem.ClearMatches();
			if (string.IsNullOrEmpty(query.SearchString))
			{
				RevisionsDataSource.SetSidebarSearch(null);
				return;
			}
			RevisionsDataSource.SetSidebarSearch(query);
			_activeSidebarSearchJob = RepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Search '{0}'", query.SearchString), delegate(JobMonitor monitor)
			{
				Action<RevisionWithFiles> matchCallback = delegate(RevisionWithFiles revisionMatch)
				{
					base.Dispatcher.Async(delegate
					{
						RevisionsDataSource.AddSidebarSearchMatch(query.SearchString, revisionMatch.Sha);
						SidebarSearchTabItem.AddMatch(revisionMatch);
					});
				};
				base.Dispatcher.Async(delegate
				{
					SidebarSearchTabItem.IsSearchInProgress = true;
				});
				new RevisionSearchGitCommand().Execute(gitModule, repositoryData.Submodules.Items, query, matchCallback, monitor);
				base.Dispatcher.Async(delegate
				{
					SidebarSearchTabItem.IsSearchInProgress = false;
				});
			}, JobFlags.Hidden);
		}

		private void GraphCellView_ExpandToggle(object sender, EventArgs e)
		{
			if ((sender as GraphCellView)?.DataContext is DecoratedRevision decoratedRevision)
			{
				if (decoratedRevision.IsCollapsed)
				{
					Expand(decoratedRevision.Sha);
				}
				else
				{
					Collapse(decoratedRevision.Sha);
				}
			}
		}

		private static bool ReferenceMatch(ForkPlus.Git.Reference reference, string query)
		{
			if (reference.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) != -1)
			{
				return true;
			}
			return false;
		}

		private static Control CreateReferenceButtonsMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryReferences references, ForkPlus.Git.Reference reference)
		{
			ImageToggleButton pinButton = new ImageToggleButton
			{
				Style = Theme.BranchOptionButtonStyle,
				Image = Theme.PinOnIcon,
				AlternativeImage = Theme.PinOffIcon,
				State = references.IsPinned(reference),
				ToolTip = PreferencesLocalization.Current("Pin '" + reference.Name + "'")
			};
			pinButton.Click += delegate
			{
				if (!pinButton.State)
				{
					RepositoryUserControl.Commands.AddReferenceStar.Execute(gitModule, references.Items, reference);
				}
				else
				{
					RepositoryUserControl.Commands.RemoveReferenceStar.Execute(gitModule, references.Items, reference);
				}
				pinButton.State = !pinButton.State;
			};
			ImageToggleButton filterButton = new ImageToggleButton
			{
				Style = Theme.BranchOptionButtonStyle,
				Image = Theme.BranchFilterOnIcon,
				AlternativeImage = Theme.BranchFilterOffIcon,
				State = references.IsFiltered(reference),
				ToolTip = PreferencesLocalization.Current("Show '" + reference.Name + "' commits only")
			};
			ImageToggleButton hideButton = new ImageToggleButton
			{
				Style = Theme.BranchOptionButtonStyle,
				Image = Theme.HideBranchOnIcon,
				AlternativeImage = Theme.HideBranchOffIcon,
				State = references.IsHidden(reference),
				ToolTip = PreferencesLocalization.Current("Hide '" + reference.Name + "' in the commit list")
			};
			filterButton.Click += delegate
			{
				ReferenceFilterState filterStatus2 = ((!filterButton.State) ? ReferenceFilterState.Filter : ReferenceFilterState.None);
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(repositoryUserControl, reference, filterStatus2);
				if (!filterButton.State)
				{
					hideButton.State = false;
				}
				filterButton.State = !filterButton.State;
			};
			hideButton.Click += delegate
			{
				ReferenceFilterState filterStatus = ((!hideButton.State) ? ReferenceFilterState.Hide : ReferenceFilterState.None);
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(repositoryUserControl, reference, filterStatus);
				if (!hideButton.State)
				{
					filterButton.State = false;
				}
				hideButton.State = !hideButton.State;
			};
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			stackPanel.Children.Add(pinButton);
			stackPanel.Children.Add(filterButton);
			stackPanel.Children.Add(hideButton);
			return new MenuItem
			{
				Header = stackPanel,
				Style = Theme.CustomContentMenuItemStyle
			};
		}

		// 阶段 4.5：WPF FrameworkElement → Avalonia Control（参考 DiffEntryRowUserControl）。
		private static void HideParentContextMenu(object ctrl)
		{
			for (Control frameworkElement = ctrl as Control; frameworkElement != null; frameworkElement = frameworkElement.Parent as Control)
			{
				if (frameworkElement is ContextMenu contextMenu)
				{
					contextMenu.IsOpen = false;
					break;
				}
			}
		}

	}
}
