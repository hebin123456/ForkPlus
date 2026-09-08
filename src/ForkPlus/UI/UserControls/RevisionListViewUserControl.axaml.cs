using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Biturbo;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.Utils.Http;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

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

		private Window _contextMenuOwnerWindow;

		private EventHandler _contextMenuOwnerDeactivatedHandler;

		private EventHandler<PointerPressedEventArgs> _contextMenuOwnerPointerPressedHandler;

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
			ClearRevisionListSelection();
			RevisionsDataSource.Reload(RepositoryUserControl.JobQueue, repositoryData.RevisionStorage, repositoryData.Stashes, repositoryData.References, repositoryData.Remotes, repositoryData.Worktrees, repositoryData.ShowStashesInRevisionList, repositoryData.Reflog, repositoryData.CollapseState, repositoryData.UserColors, RepositoryUserControl.GitModule);
		}

		private void ClearRevisionListSelection()
		{
			RevisionListView.IsMultiselectionInProgress = true;
			try
			{
				RevisionListView.SelectedItems.Clear();
				RevisionListView.SelectedItem = null;
				RevisionListView.SelectedIndex = -1;
			}
			finally
			{
				RevisionListView.IsMultiselectionInProgress = false;
			}
		}

		static RevisionListViewUserControl()
		{
			// Migration note：WPF TabNavigationProperty.OverrideMetadata(Local) → 改构造函数 KeyboardNavigation.SetTabNavigation(this, Local)（Avalonia OverrideMetadata 为私有）。
		}

		public RevisionListViewUserControl()
		{
			InitializeComponent();
			_refreshContextSearch = new DelayedAction<string>(RefreshContextSearch, 0.1);
			RevisionSearchPanelUserControl.SearchQueryChanged += RevisionSearchPanelUserControl_SearchQueryChanged;
			RevisionSearchPanelUserControl.SearchSubmitted += delegate
			{
				_refreshContextSearch.InvokeNow(RevisionSearchPanelUserControl.SearchString);
			};
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
			RevisionListView.AddHandler(DragDrop.DropEvent, RevisionListView_Drop);
			RevisionListView.LayoutUpdated += delegate
			{
				// 性能修复（大仓库滚动卡顿）：LayoutUpdated 在滚动实化新行时每个布局帧都会触发，
				// 原实现每次都全量遍历可视树，开销巨大。此处合并到每个渲染帧最多执行一次。
				RequestStretchRevisionListItems();
			};
			RevisionListView.AddHandler(
				InputElement.PointerPressedEvent,
				new EventHandler<PointerPressedEventArgs>(RevisionListView_ContextMenuPointerPressed),
				global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
				handledEventsToo: true);
			base.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
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
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
			RevisionListView.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.A && KeyboardHelper.IsCtrlDown && !KeyboardHelper.IsShiftDown)
				{
					e.Handled = true;
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			this.AddCommandBinding(RepositoryUserControl.Commands.CopyRevisionSha.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(SelectedRevisions.Map((DecoratedRevision x) => x.ToRevision()));
			}));
			this.AddCommandBinding(RepositoryUserControl.Commands.CopyRevisionInfo.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(SelectedRevisions.Map((DecoratedRevision x) => x.ToRevision()));
			}));
			this.AddCommandBinding(RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.CreateShortcutCommandBinding(delegate
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
			this.AddCommandBinding(RepositoryUserControl.Commands.RemoveReferenceCommand.CreateShortcutCommandBinding(delegate
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
			this.AddCommandBinding(RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateShortcutCommandBinding(delegate
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
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			WeakEventManager<NotificationCenter, EventArgs<RevisionListOrientation>>.AddHandler(NotificationCenter.Current,"RevisionListOrientatioChanged",delegate
(object sender, global::System.EventArgs e)			{
				RefreshRevisionListViewTemplate();
			});
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
			ScrollViewer scrollViewer = ScrollViewerHelper.FindScrollViewer(RevisionListView);
			if (scrollViewer == null)
			{
				return null;
			}
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
			NotifySelectionChangedFromCurrentItems();
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
					NotifySelectionChangedFromCurrentItems();
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
				if (list.Count > 0)
				{
					RevisionListView.Select(list, selectOptions);
					NotifySelectionChangedFromCurrentItems();
					return true;
				}
			}
			if (fallbackRow != -1 && RevisionsDataSource.Count > fallbackRow)
			{
				RevisionListView.Select(fallbackRow, selectOptions);
				NotifySelectionChangedFromCurrentItems();
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
			StretchRevisionListItems();
		}

		private bool _stretchQueued;

		private global::Avalonia.Controls.ScrollViewer _cachedScroller;

		private void RequestStretchRevisionListItems()
		{
			if (_stretchQueued)
			{
				return;
			}
			_stretchQueued = true;
			Dispatcher.UIThread.Post(delegate
			{
				_stretchQueued = false;
				StretchRevisionListItems();
			}, DispatcherPriority.Render);
		}

		private void StretchRevisionListItems()
		{
			// Bug 修复（轨道图宽度 100%）：原取 Bounds.Width - 8（含竖向滚动条占位 ~13px，
			// 且窗口拉伸/行实化时机不同步），行宽比视口大 → extent 超出 → "有点超出"的横向滚动条。
			// 改为取内嵌 ScrollViewer 的 Viewport.Width（真实可视区域，无滚动条占位）。
			// 性能：ScrollViewer 查找结果缓存，避免每次全量遍历可视树。
			global::Avalonia.Controls.ScrollViewer scroller = _cachedScroller;
			if (scroller == null || !global::Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(scroller).Any((global::Avalonia.Visual x) => ReferenceEquals(x, RevisionListView)))
			{
				scroller = (_cachedScroller = global::Avalonia.VisualTree.VisualExtensions
					.GetVisualDescendants(RevisionListView).OfType<global::Avalonia.Controls.ScrollViewer>().FirstOrDefault());
			}
			double width = scroller != null ? scroller.Viewport.Width : Math.Max(0.0, RevisionListView.Bounds.Width - 23.0);
			if (width <= 0.0)
			{
				return;
			}
			// Bug1 修复：限制 refs 徽章 ItemsControl 的 MaxWidth（对齐原版 WPF GridView
			// UpdateResizableColumnWidth(0) 语义：第 0 列(graph+refs+subject)总宽 =
			// 列表可用宽 - 固定列(avatar 18 + author 120 + sha 70 + date 130) - 边距。
			// Avalonia Grid 的 Auto 列空间不足时不裁剪（与 WPF 行为不同），超长分支名
			// 徽章会把行撑爆、溢出盖住日期列；此处给徽章留足空间但封顶剩余宽度
			// （subject 星号列至少保留 120，graph 按典型宽度预留 130）。
			double refsMaxWidth = Math.Max(0.0, width - 18.0 - 120.0 - 70.0 - 130.0 - 120.0 - 130.0 - 40.0);
			// 性能：用 GetRealizedContainers() 直接拿已实化容器（O(行数)），
			// 替代原来的 GetVisualDescendants 全可视树遍历（含每个行内所有子元素，分配大）。
			foreach (DragAndDropListViewItem item in RevisionListView.GetRealizedContainers().OfType<DragAndDropListViewItem>())
			{
				if (double.IsNaN(item.Width) || Math.Abs(item.Width - width) > 0.5)
				{
					item.Width = width;
				}
				if (Math.Abs(item.MinWidth - width) > 0.5)
				{
					item.MinWidth = width;
				}
				// 行内唯一的 ItemsControl 即 refs 徽章容器（GraphCellView 为纯 Control 无子树）。
				global::Avalonia.Controls.ItemsControl refsItemsControl = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(item).OfType<global::Avalonia.Controls.ItemsControl>().FirstOrDefault();
				if (refsItemsControl != null && (double.IsNaN(refsItemsControl.MaxWidth) || Math.Abs(refsItemsControl.MaxWidth - refsMaxWidth) > 0.5))
				{
					refsItemsControl.MaxWidth = refsMaxWidth;
				}
			}
		}

		private void RevisionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (RevisionListView.IsMultiselectionInProgress)
			{
				return;
			}
			e.Handled = true;
			if (e.AddedItems.Count <= 0 && e.RemovedItems.Count <= 0 && RevisionListView.SelectedItems.Count == 0 && RevisionListView.SelectedItem == null)
			{
				return;
			}
			NotifySelectionChangedFromCurrentItems();
		}

		private void NotifySelectionChangedFromCurrentItems()
		{
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
			if (array.Length == 0 && RevisionListView.SelectedItem is DecoratedRevision selectedRevision)
			{
				array = new DecoratedRevision[1] { selectedRevision };
			}
			if (array.Length == 0)
			{
				return;
			}
			// v3.12 修复防御（同 IndexOf 修复）：同一实例在 SelectedItems 中出现两次时会被
			// 误判为双选（Range）——底部"提交/文件树"tab 变灰、右键菜单变多选菜单。WPF 的
			// ListBox.SelectedItems 不可能含重复项，这里按引用去重对齐该语义，把任何来源的
			// 重复（孤儿项 + 按索引选中叠加等）限制在单选语义内。
			if (array.Length > 1)
			{
				array = DistinctRevisions(array);
			}
			if (array.Length == 2)
			{
				array = SortRevisionsByRows(array);
			}
			IRoundedSelectionListBoxViewModel[] selectedItems = array;
			selectedItems.RefreshSelectionType();
			this.SelectionChanged?.Invoke(this, new EventArgs<DecoratedRevision[]>(array));
		}

		private static DecoratedRevision[] DistinctRevisions(DecoratedRevision[] items)
		{
			List<DecoratedRevision> list = new List<DecoratedRevision>(items.Length);
			DecoratedRevision[] array = items;
			for (int i = 0; i < array.Length; i++)
			{
				DecoratedRevision item = array[i];
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
			if (list.Count == items.Length)
			{
				return items;
			}
			return list.ToArray();
		}

		private void RevisionListView_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
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

		private void RevisionListView_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			if (sender is ListBox listBox)
			{
				Point point;
				Point? position = e.TryGetPosition(listBox, out point) ? point : null;
				if (TrySelectContextMenuTarget(listBox, e.Source, position) && OpenRevisionContextMenu(listBox, e.Source))
				{
					e.Handled = true;
					return;
				}
			}
			e.Handled = true;
			RevisionListView.ContextMenu.Close();
		}

		private void RevisionListView_ContextMenuPointerPressed(object sender, PointerPressedEventArgs e)
		{
			PointerPointProperties properties = e.GetCurrentPoint(RevisionListView).Properties;
			if (!properties.IsRightButtonPressed && properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
			{
				return;
			}

			e.Handled = true;
			RevisionListView.ContextMenu?.Close();
			if (!TrySelectContextMenuTarget(RevisionListView, e.Source, e.GetPosition(RevisionListView)) || !OpenRevisionContextMenu(RevisionListView, e.Source))
			{
				RevisionListView.ContextMenu?.Close();
			}
		}

		private bool OpenRevisionContextMenu(ListBox listBox, object source)
		{
			AttachContextMenuDismissBehavior(listBox.ContextMenu);
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			GitModule gitModule = RepositoryUserControl?.GitModule;
			RepositoryData repositoryData = RepositoryUserControl?.RepositoryData;
			CommitGraphCache commitGraphCache = RepositoryUserControl?.CommitGraphCache;
			if (repositoryUserControl == null || gitModule == null || repositoryData == null || commitGraphCache == null)
			{
				return false;
			}

			GraphCellView graphCellView = FindVisualAncestorOrSelf<GraphCellView>(source as global::Avalonia.Visual);
			if (graphCellView?.DataContext is DecoratedRevision graphRevision && graphRevision.GetParents().Length > 1)
			{
				ShowRevisionContextMenu(listBox, CreateCollapseContextMenu(graphRevision));
				return true;
			}

			DecoratedRevision[] selectedRevisions = listBox.SelectedItems.CompactMap((object x) => x as DecoratedRevision);
			if (selectedRevisions.AllItems((DecoratedRevision x) => x.IsStash()))
			{
				StashRevision[] stashes = selectedRevisions.Map((DecoratedRevision x) => x.AsStashRevision());
				ShowRevisionContextMenu(listBox, CreateStashContextMenuItems(repositoryUserControl, stashes));
				return true;
			}

			DecoratedRevision decoratedRevision = selectedRevisions.SingleItem();
			if (decoratedRevision != null)
			{
				ShowRevisionContextMenu(listBox, CreateRevisionContextMenuItems(repositoryUserControl, gitModule, repositoryData, decoratedRevision, commitGraphCache));
				return true;
			}

			if (selectedRevisions.Length > 1)
			{
				ShowRevisionContextMenu(listBox, CreateMultipleRevisionsContextMenuItems(repositoryUserControl, gitModule, repositoryData, selectedRevisions));
				return true;
			}

			return false;
		}

		private void ShowRevisionContextMenu(ListBox listBox, IEnumerable<Control> items)
		{
			ContextMenu contextMenu = listBox.ContextMenu;
			if (contextMenu == null)
			{
				return;
			}
			contextMenu.PlacementTarget = listBox;
			contextMenu.SetItems(items);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, listBox);
			contextMenu.Open();
		}

		private void AttachContextMenuDismissBehavior(ContextMenu contextMenu)
		{
			if (contextMenu == null)
			{
				return;
			}
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, RevisionListView);

			_contextMenuOwnerWindow = TopLevel.GetTopLevel(this) as Window;
			if (_contextMenuOwnerWindow != null)
			{
				_contextMenuOwnerDeactivatedHandler ??= (_, _) =>
				{
					if (contextMenu.IsOpen)
					{
						contextMenu.Close();
					}
				};
				_contextMenuOwnerWindow.Deactivated -= _contextMenuOwnerDeactivatedHandler;
				_contextMenuOwnerWindow.Deactivated += _contextMenuOwnerDeactivatedHandler;

				_contextMenuOwnerPointerPressedHandler ??= (_, _) =>
				{
					if (contextMenu.IsOpen)
					{
						contextMenu.Close();
					}
				};
				_contextMenuOwnerWindow.RemoveHandler(global::Avalonia.Input.InputElement.PointerPressedEvent, _contextMenuOwnerPointerPressedHandler);
				_contextMenuOwnerWindow.AddHandler(
					global::Avalonia.Input.InputElement.PointerPressedEvent,
					_contextMenuOwnerPointerPressedHandler,
					global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
					handledEventsToo: true);
			}

			contextMenu.Closed -= ContextMenu_Closed;
			contextMenu.Closed += ContextMenu_Closed;
		}

		private void ContextMenu_Closed(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (sender is ContextMenu contextMenu)
			{
				contextMenu.Closed -= ContextMenu_Closed;
			}
			if (_contextMenuOwnerWindow != null && _contextMenuOwnerDeactivatedHandler != null)
			{
				_contextMenuOwnerWindow.Deactivated -= _contextMenuOwnerDeactivatedHandler;
			}
			if (_contextMenuOwnerWindow != null && _contextMenuOwnerPointerPressedHandler != null)
			{
				_contextMenuOwnerWindow.RemoveHandler(global::Avalonia.Input.InputElement.PointerPressedEvent, _contextMenuOwnerPointerPressedHandler);
			}
			_contextMenuOwnerWindow = null;
		}

		[Null]
			private Branch GetClickedBranch(global::Avalonia.Input.TappedEventArgs args) // Migration note：双击事件改为 TappedEventArgs。
			{
				// Migration note：WPF DependencyObject 可视树遍历 → Avalonia Visual。
				// WPF 中分支名是 Run 元素（Run.DataContext = BranchViewModel）；Avalonia 的 Run 不是 Visual，
				// 命中源是包含它的 TextBlock，改为检查遍历元素的 DataContext 是否为 BranchViewModel。
				global::Avalonia.Visual dependencyObject = args.Source as global::Avalonia.Visual;
				while (dependencyObject != null && !(dependencyObject is global::Avalonia.Controls.ListBoxItem))
				{
					if (dependencyObject is global::Avalonia.Controls.Control { DataContext: BranchViewModel branchViewModel })
					{
						return branchViewModel.Reference as Branch;
					}
					dependencyObject = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject);
					if (dependencyObject is global::Avalonia.Controls.Presenters.ContentPresenter { DataContext: BranchViewModel dataContext }) // Migration note：ContentPresenter 在 Presenters 命名空间。
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
		// Migration note：WPF ListView.View=GridView（宽→SingleRow / 窄→DoubleRow）在 Avalonia 无列视图体系，
		// 改为切换 ListBox.ItemTemplate（模板在 axaml Resources 重建，见 SingleRowRevisionTemplate/DoubleRowRevisionTemplate）。
		bool useSingleRow = ForkPlusSettings.Default.RevisionListOrientation == RevisionListOrientation.Horizontal || RevisionListView.GetAvailableWidth() > num;
		string resourceKey = useSingleRow ? "SingleRowRevisionTemplate" : "DoubleRowRevisionTemplate";
		object value = null;
		global::Avalonia.Controls.Templates.IDataTemplate dataTemplate = null;
		bool found = base.Resources is global::Avalonia.Controls.IResourceDictionary resourceDictionary && resourceDictionary.TryGetResource(resourceKey, null, out value) && (dataTemplate = (value as global::Avalonia.Controls.Templates.IDataTemplate)) != null;
		if (found && !ReferenceEquals(RevisionListView.ItemTemplate, dataTemplate))
		{
			RevisionListView.ItemTemplate = dataTemplate;
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
			if (!(sender is DragAndDropListViewItem { DataContext: DecoratedRevision dataContext } dragAndDropListViewItem) || !(e.WpfData().GetData(typeof(DecoratedRevision[])) is DecoratedRevision[] array) || array.Length == 0 || array.Contains(dataContext))
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

		private bool TrySelectContextMenuTarget(ListBox listBox, object source, Point? position)
		{
			ListBoxItem container = FindVisualAncestorOrSelf<ListBoxItem>(source as global::Avalonia.Visual);
			if (container == null && position.HasValue)
			{
				container = listBox.GetContainerAtPoint<ListBoxItem>(position.Value);
			}
			if (container?.DataContext is not DecoratedRevision revision)
			{
				return false;
			}
			DecoratedRevision[] selectedRevisions = listBox.SelectedItems.CompactMap((object x) => x as DecoratedRevision);
			if (container.IsSelected && selectedRevisions.Contains(revision))
			{
				return true;
			}
			listBox.SelectedItems.Clear();
			listBox.SelectedItems.Add(revision);
			listBox.SelectedItem = revision;
			listBox.SelectedIndex = revision.Row;
			container.IsSelected = true;
			container.InvalidateVisual();
			return true;
		}

		private static T FindVisualAncestorOrSelf<T>(global::Avalonia.Visual visual) where T : class
		{
			for (global::Avalonia.Visual current = visual; current != null; current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current))
			{
				if (current is T match)
				{
					return match;
				}
			}
			return null;
		}

		private void RevisionListView_Drop(object sender, DragEventArgs e)
		{
			DragAndDropListViewItem item = e.Source as DragAndDropListViewItem
				?? (e.Source as global::Avalonia.Visual)?.GetParent<DragAndDropListViewItem>();
			if (item != null)
			{
				RevisionListViewItem_Drop(item, e);
			}
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
			window.SetOwnerCompat(WpfApp.MainWindow);
			string title = PreferencesLocalization.FormatCurrent("AI PR Description: {0}", rangeLabel);
			window.ShowAtOwnerScreen(); // 修复链 23：跟随主窗口所在屏幕
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
					// v3.10.2：与 status 命令完全对齐 fsmonitor/untrackedCache/checkStat/--no-optional-locks 四件套。
				GitCommand diffCmd = new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "--no-pager", "diff", "--no-color", "--find-renames", "--no-ext-diff", "--submodule=short", "--unified=10", srcStr + ".." + dstStr);
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
				new MessageBoxWindow("AI Explain Commit", "AI is not configured. Please configure AI review settings in Preferences first.", "OK", showCancelButton: false, showWarningIcon: true).ShowDialog();
				return;
			}
			string shaStr = sha.ToString();
			string abbreviatedSha = sha.ToAbbreviatedString();

			AiTextResultWindow window = new AiTextResultWindow();
			window.SetOwnerCompat(WpfApp.MainWindow);
			string title = PreferencesLocalization.FormatCurrent("AI Explain {0}", abbreviatedSha);
			window.ShowAtOwnerScreen(); // 修复链 23：跟随主窗口所在屏幕
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
			RevisionSearchPanelUserControl.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
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
								base.Dispatcher.Post(delegate
								{
									RevisionSearchPanelUserControl.IsBusyIndicatorVisible = true;
								});
								GitCommandResult<Sha[]> searchResult = new RevisionContextSearchGitCommand().Execute(gitModule, searchString, shas.ToArray(), refMatches, monitor);
								base.Dispatcher.Post(delegate
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
					base.Dispatcher.Post(delegate
					{
						RevisionsDataSource.AddSidebarSearchMatch(query.SearchString, revisionMatch.Sha);
						SidebarSearchTabItem.AddMatch(revisionMatch);
					});
				};
				base.Dispatcher.Post(delegate
				{
					SidebarSearchTabItem.IsSearchInProgress = true;
				});
				new RevisionSearchGitCommand().Execute(gitModule, repositoryData.Submodules.Items, query, matchCallback, monitor);
				base.Dispatcher.Post(delegate
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
			ImageToggleButton pinButton = global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new ImageToggleButton
			{
				Image = global::ForkPlus.UI.Theme.PinOnIcon,				AlternativeImage = global::ForkPlus.UI.Theme.PinOffIcon,				State = references.IsPinned(reference)			},PreferencesLocalization.Current("Pin '" + reference.Name + "'")
),global::ForkPlus.UI.Theme.BranchOptionButtonStyle);
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
			ImageToggleButton filterButton = global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new ImageToggleButton
			{
				Image = global::ForkPlus.UI.Theme.BranchFilterOnIcon,				AlternativeImage = global::ForkPlus.UI.Theme.BranchFilterOffIcon,				State = references.IsFiltered(reference)			},PreferencesLocalization.Current("Show '" + reference.Name + "' commits only")
),global::ForkPlus.UI.Theme.BranchOptionButtonStyle);
			ImageToggleButton hideButton = global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new ImageToggleButton
			{
				Image = global::ForkPlus.UI.Theme.HideBranchOnIcon,				AlternativeImage = global::ForkPlus.UI.Theme.HideBranchOffIcon,				State = references.IsHidden(reference)			},PreferencesLocalization.Current("Hide '" + reference.Name + "' in the commit list")
),global::ForkPlus.UI.Theme.BranchOptionButtonStyle);
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
			return global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(new MenuItem
			{
				Header = stackPanel			},global::ForkPlus.UI.Theme.CustomContentMenuItemStyle
);
		}

		private static void HideParentContextMenu(object ctrl)
		{
			for (global::Avalonia.Controls.Control frameworkElement = ctrl as global::Avalonia.Controls.Control; frameworkElement != null; frameworkElement = frameworkElement.Parent as global::Avalonia.Controls.Control)
			{
				if (frameworkElement is ContextMenu contextMenu)
				{
					contextMenu.Close();
					break;
				}
			}
		}

	}
}
