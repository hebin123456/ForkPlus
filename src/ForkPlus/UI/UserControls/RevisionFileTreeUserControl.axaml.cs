using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionFileTreeUserControl : UserControl
	{
		private Sha? _sha;

		private int _treeRequestId;

		private int _contentRequestId;

		private bool _isRefreshingTree;

		// v5.3.0（文件树顶部过滤框，与变更页 RevisionChangesUserControl 同款交互）：
		// 当前过滤串 + 输入防抖（FilterRequestChanged 高频触发，DelayedAction 合并刷新）。
		private string _fileTreeFilter;

		private readonly DelayedAction<string> _refreshTreeFilterAction;

		// v5.3.0：懒加载树只含已展开目录的子项，过滤需要全量路径——首次过滤时后台一次性
		// 递归拉取整树补全（git ls-tree -r -t，一次进程），完成后重应用过滤。
		private bool _treeFullyLoaded;

		private int _fullTreeLoadRequestId;

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		public RevisionFileTreeUserControl()
		{
			InitializeComponent();
			_refreshTreeFilterAction = new DelayedAction<string>(UpdateTreeFilter);
			FilterTextBox.FilterRequestChanged += delegate
			{
				_refreshTreeFilterAction.InvokeWithDelay(FilterTextBox.FilterRequest);
			};
			this.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.F && Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.LeftShift))
				{
					FilterTextBox.ShowWithAnimation();
					e.Handled = true;
				}
				else if (e.Key == Key.Escape)
				{
					FilterTextBox.HideWithAnimation();
					e.Handled = true;
				}
			};
			// v5.3.0：视图模式（FileListMode）变更页/提交窗口共享同一设置——文件树 tab 同步响应。
			WeakEventManager<NotificationCenter, EventArgs<FileListMode>>.AddHandler(NotificationCenter.Current, "FileListModeChanged", delegate
(object sender, System.EventArgs e)			{
				ApplyFileListMode();
			});
			RestoreFilesTreeViewColumnWidth();
			GridSplitter.DragCompleted += delegate
			{
				SaveFilesTreeViewColumnWidth();
			};
			this.AddCommandBinding(RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateShortcutCommandBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem3 = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem3 != null)
				{
					ChangedFile changedFile = new ChangedFile(revisionFileTreeViewItem3.FileTreeItem.FilePath, staged: true);
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(RevisionDetailsUserControl.GitModule, _sha.ToString(), changedFile);
				}
			}));
			this.AddCommandBinding(RepositoryUserControl.Commands.CopyFilePaths.CreateShortcutCommandBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem2 = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem2 != null)
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { revisionFileTreeViewItem2.FileTreeItem.FilePath });
				}
			}));
			this.AddCommandBinding(RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateShortcutCommandBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem != null)
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(RevisionDetailsUserControl.GitModule, new string[1] { revisionFileTreeViewItem.FileTreeItem.FilePath });
				}
			}));
			FilesTreeView.AddHandler(
				InputElement.PointerPressedEvent,
				new EventHandler<PointerPressedEventArgs>(FilesTreeView_ContextMenuPointerPressed),
				global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
				handledEventsToo: true);
		}

		private void FilesTreeView_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
		{
			RevisionFileTreeViewItem[] array = FilesTreeView.SelectedItems.CompactMap((object x) => x as RevisionFileTreeViewItem);
			MultiselectionTreeViewItem[] items = array;
			items.RefreshSelectionType();
			if (array.Length != 0)
			{
				UpdateFileDetails(array[0]);
			}
			else
			{
				UpdateFileDetails(null);
			}
		}

		private void FilesTreeView_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
			ListBoxItem listBoxItem = (e.Source as global::Avalonia.Visual)?.GetParent<ListBoxItem>() /* Migration note：Visual 扩展需 Visual 接收者 */;
			if (listBoxItem != null && FilesTreeView.ItemFromContainer(listBoxItem) is RevisionFileTreeViewItem revisionFileTreeViewItem)
			{
				revisionFileTreeViewItem.IsExpanded = !revisionFileTreeViewItem.IsExpanded;
			}
		}

		private void UpdateFileDetails([Null] RevisionFileTreeViewItem fileTreeItem)
		{
			if (fileTreeItem == null || fileTreeItem.FileTreeItem.ItemType == FileTreeItem.FileTreeItemType.Directory)
			{
				FileContentControl.Content = null;
				return;
			}
			if (fileTreeItem.FileTreeItem.ItemType == FileTreeItem.FileTreeItemType.Submodule)
			{
				FileContentControl.Content = GitCommandResult<Content>.Success(new TextContent(fileTreeItem.FileTreeItem.FilePath, isTracked: true, "Submodule: " + fileTreeItem.FileTreeItem.TreeSha));
				return;
			}
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			Sha? sha2 = _sha;
			if (!sha2.HasValue)
			{
				return;
			}
			Sha sha = sha2.GetValueOrDefault();
			int requestId = ++_contentRequestId;
			FileContentControl.Content = null;
			Task.Run(() => new GetFileContentGitCommand().Execute(gitModule, sha, fileTreeItem.FileTreeItem.FilePath)).ContinueWith(delegate(Task<GitCommandResult<Content>> getFileContentTask)
			{
				Dispatcher.UIThread.Post(delegate
				{
					if (getFileContentTask.IsFaulted || getFileContentTask.IsCanceled || requestId != _contentRequestId)
					{
						return;
					}
					if (FilesTreeView.SelectedItems.Count > 0 && FilesTreeView.SelectedItems[0] == fileTreeItem)
					{
						FileContentControl.Content = getFileContentTask.Result;
					}
				});
			});
		}

		public void Refresh(Sha sha)
		{
			_sha = sha;
			int requestId = ++_treeRequestId;
			_isRefreshingTree = true;
			// v5.3.0：切换 revision 重建树，整树补全状态作废（重新懒加载）。
			_treeFullyLoaded = false;
			_fullTreeLoadRequestId++;
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			RepositoryUserControl repositoryUserControl = RevisionDetailsUserControl.RepositoryUserControl;
			FileContentControl.RepositoryUserControl = repositoryUserControl;
			FileContentControl.Content = null;
			Sha shaValue = sha;
			Task.Run(() => new GetRevisionFileTreeGitCommand().Execute(gitModule, "", shaValue)).ContinueWith(delegate(Task<GitCommandResult<FileTreeItem[]>> treeTask)
			{
				Dispatcher.UIThread.Post(delegate
				{
					if (requestId != _treeRequestId)
					{
						return;
					}
					_isRefreshingTree = false;
					if (treeTask.IsFaulted || treeTask.IsCanceled)
					{
						return;
					}
					GitCommandResult<FileTreeItem[]> gitCommandResult = treeTask.Result;
					if (!gitCommandResult.Succeeded)
					{
						return;
					}
					// 期间若已切换到其它 revision，丢弃本次结果。
					if (!_sha.HasValue || _sha.GetValueOrDefault() != shaValue)
					{
						return;
					}
					RevisionFileTreeViewItem revisionFileTreeViewItem = new RevisionFileTreeViewItem(null, null);
					FileTreeItem[] result = gitCommandResult.Result;
					foreach (FileTreeItem fileTreeItem in result)
					{
						revisionFileTreeViewItem.Children.Add(new RevisionFileTreeViewItem(gitModule, fileTreeItem));
					}
					RevisionFileTreeViewItem obj = FilesTreeView.SelectedItem as RevisionFileTreeViewItem;
					FilesTreeView.RootItem = revisionFileTreeViewItem;
					// v5.3.0：切换 revision 重建树后，重应用当前过滤条件；列表模式下按当前
					// 模式重建（树未补全则触发后台补全，回调里重建列表）。
					if (ForkPlusSettings.Default.FileListMode != FileListMode.Tree)
					{
						ApplyFileListMode();
					}
					ApplyFileTreeFilter();
					string text = _pendingFilePath ?? obj?.FileTreeItem?.FilePath;
					_pendingFilePath = null;
					if (text != null)
					{
						string[] pathComponents = text.Split('/');
						Expand(pathComponents, FilesTreeView.RootItem.Children);
					}
				});
			});
		}

		private string _pendingFilePath;

		public void ShowRevisionDetails(string filePath)
		{
			if (_isRefreshingTree || FilesTreeView.RootItem == null)
			{
				_pendingFilePath = filePath;
				return;
			}
			string[] pathComponents = filePath.Split('/');
			Expand(pathComponents, FilesTreeView.RootItem.Children);
		}

		// ── v5.3.0：文件树过滤（与变更页 FilterTextBox 同款交互） ──

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			// v5.3.0 修复：点击切换开/关（与变更页一致），原实现只开不关。
			if (FilterTextBox.IsAnimationPlaceholderVisible)
			{
				FilterTextBox.HideWithAnimation();
			}
			else
			{
				FilterTextBox.ShowWithAnimation();
				FilterTextBox.FocusAndSelectAllText();
			}
		}

		// ── v5.3.0：整树补全后的全量文件项缓存——Tree/List 模式重建复用。 ──

		[Null]
		private FileTreeItem[] _allTreeItems;

		// ── v5.3.0：视图模式下拉（FileListMode：Tree/List，与变更页共享设置）──
		// 注：变更页的 CombinedList（合并列表）选项对文件树无意义，菜单中不提供；
		// 若用户在变更页选了 CombinedList，文件树按 List（平铺）渲染。

		private void FileTreeSettingsDropdownButton_ContextMenuOpened(object sender, RoutedEventArgs e)
		{
			UpdateFileTreeSettingsMenuItems(FileTreeSettingsDropdownButton.ContextMenu);
		}

		private void UpdateFileTreeSettingsMenuItems(ContextMenu menu)
		{
			menu.Items.Clear();
			FileListMode fileListMode = ForkPlusSettings.Default.FileListMode;
			menu.Items.Add(CreateFileTreeModeMenuItem("View as Tree", FileListMode.Tree, fileListMode));
			menu.Items.Add(CreateFileTreeModeMenuItem("View as List", FileListMode.List, fileListMode));
		}

		private MenuItem CreateFileTreeModeMenuItem(string text, FileListMode mode, FileListMode currentMode)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = Preferences.PreferencesLocalization.MenuHeader(text);
			menuItem.IsChecked = currentMode == mode;
			menuItem.ToggleType = global::Avalonia.Controls.MenuItemToggleType.CheckBox;
			menuItem.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = mode;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, mode);
			};
			return menuItem;
		}

		// List/CombinedList 模式 = 全部文件平铺为单层列表（目录行不显示）；Tree = 目录层级树。
		// 与过滤叠加：重建后统一走 ApplyTreeFilter。Tree 重建需要全量数据缓存（整树补全后才有）。
		private void ApplyFileListMode()
		{
			if (FilesTreeView.RootItem == null)
			{
				return;
			}
			if (ForkPlusSettings.Default.FileListMode == FileListMode.Tree)
			{
				if (_treeFullyLoaded && _allTreeItems != null)
				{
					RebuildTreeFromItems(_allTreeItems);
				}
				return;
			}
			if (_treeFullyLoaded && _allTreeItems != null)
			{
				RebuildTreeAsFlatList((RevisionFileTreeViewItem)FilesTreeView.RootItem, _allTreeItems, RevisionDetailsUserControl?.GitModule);
				ApplyFileTreeFilter();
				return;
			}
			// 树未补全（懒加载只含已展开目录）——先补全，回调里重建列表。
			EnsureTreeFullyLoaded();
		}

		private void RebuildTreeFromItems(FileTreeItem[] allItems)
		{
			GitModule gitModule = RevisionDetailsUserControl?.GitModule;
			BuildFullTreeChildren((RevisionFileTreeViewItem)FilesTreeView.RootItem, allItems, gitModule);
			ApplyFileTreeFilter();
		}

		// internal 供回归测试直测（InternalsVisibleTo）：列表模式 = 全部文件平铺单层。
		internal static void RebuildTreeAsFlatList(RevisionFileTreeViewItem root, FileTreeItem[] allItems, GitModule gitModule)
		{
			root.Children.Clear();
			foreach (FileTreeItem item in allItems)
			{
				if (item.ItemType == FileTreeItem.FileTreeItemType.File)
				{
					root.Children.Add(new RevisionFileTreeViewItem(gitModule, item));
				}
			}
		}

		// 整树补全（懒加载树首次过滤/切列表模式时触发）：后台 git ls-tree -r -t 一次性拉全，
		// 回调里按当前视图模式重建（Tree 层级树 / List 平铺列表）并重应用过滤。
		// 已补全直接返回 true；发起后台补全返回 false。
		private bool EnsureTreeFullyLoaded()
		{
			if (_treeFullyLoaded)
			{
				return true;
			}
			if (!_sha.HasValue || FilesTreeView.RootItem == null)
			{
				return false;
			}
			GitModule gitModule = RevisionDetailsUserControl?.GitModule;
			if (gitModule == null)
			{
				return false;
			}
			Sha sha = _sha.GetValueOrDefault();
			int requestId = ++_fullTreeLoadRequestId;
			Task.Run(() => new GetRevisionFileTreeGitCommand().ExecuteRecursive(gitModule, sha)).ContinueWith(delegate(Task<GitCommandResult<FileTreeItem[]>> treeTask)
			{
				Dispatcher.UIThread.Post(delegate
				{
					if (treeTask.IsFaulted || treeTask.IsCanceled || requestId != _fullTreeLoadRequestId)
					{
						return;
					}
					if (!_sha.HasValue || _sha.GetValueOrDefault() != sha || FilesTreeView.RootItem == null)
					{
						return;
					}
					GitCommandResult<FileTreeItem[]> result = treeTask.Result;
					if (result == null || !result.Succeeded)
					{
						return;
					}
					_allTreeItems = result.Result;
					if (ForkPlusSettings.Default.FileListMode != FileListMode.Tree)
					{
						RebuildTreeAsFlatList((RevisionFileTreeViewItem)FilesTreeView.RootItem, _allTreeItems, gitModule);
					}
					else
					{
						BuildFullTreeChildren((RevisionFileTreeViewItem)FilesTreeView.RootItem, _allTreeItems, gitModule);
					}
					_treeFullyLoaded = true;
					ApplyFileTreeFilter();
				});
			});
			return false;
		}

		private void UpdateTreeFilter(string filterString)
		{
			_fileTreeFilter = filterString;
			// v5.3.0：懒加载树未补全时先按已加载部分过滤出即时结果，同时后台整树补全
			//（EnsureTreeFullyLoaded 回调里统一重建 + 重应用过滤），结果自动变完整。
			ApplyFileTreeFilter();
			if (!string.IsNullOrWhiteSpace(filterString))
			{
				EnsureTreeFullyLoaded();
			}
		}

		// v5.3.0：把递归 ls-tree 的全量路径挂载为完整 UI 树。输入按 git 树序（父先于子、
		// 目录先于内容）；每层子项按既有规则重排（目录优先 + 自然排序，与单层加载一致）。
		// gitModule 为 null 时创建的目录节点不可再懒加载（测试构造场景）。
		// internal 供回归测试直测（InternalsVisibleTo）。
		internal static void BuildFullTreeChildren(RevisionFileTreeViewItem root, FileTreeItem[] allItems, GitModule gitModule)
		{
			// 按父路径分组（保持输出序；父路径为目录的完整相对路径，root 组为空串）。
			Dictionary<string, List<FileTreeItem>> itemsByParent = new Dictionary<string, List<FileTreeItem>>(StringComparer.Ordinal);
			foreach (FileTreeItem item in allItems)
			{
				string path = item.FilePath ?? "";
				int lastSlash = path.LastIndexOf('/');
				string parentPath = lastSlash < 0 ? "" : path.Substring(0, lastSlash);
				if (!itemsByParent.TryGetValue(parentPath, out List<FileTreeItem> bucket))
				{
					bucket = new List<FileTreeItem>();
					itemsByParent[parentPath] = bucket;
				}
				bucket.Add(item);
			}
			root.Children.Clear();
			AttachLevel(root, "", itemsByParent, gitModule);
		}

		private static void AttachLevel(RevisionFileTreeViewItem parent, string parentPath, Dictionary<string, List<FileTreeItem>> itemsByParent, GitModule gitModule)
		{
			if (!itemsByParent.TryGetValue(parentPath, out List<FileTreeItem> childrenItems))
			{
				return;
			}
			childrenItems.Sort(GetRevisionFileTreeGitCommand.FileTreeItemComparer);
			foreach (FileTreeItem item in childrenItems)
			{
				RevisionFileTreeViewItem node = new RevisionFileTreeViewItem(gitModule, item);
				parent.Children.Add(node);
				if (item.ItemType == FileTreeItem.FileTreeItemType.Directory)
				{
					AttachLevel(node, item.FilePath, itemsByParent, gitModule);
				}
			}
		}

		// 按"文件/目录路径子串"过滤树节点：节点自身路径或任一后代命中即保留（父目录
		// 连带可见），其余隐藏（MultiselectionTreeViewItem.IsHidden，自带扁平列表刷新）。
		// 过滤激活时展开全部可见目录，保证命中文件不藏于折叠目录下。
		private void ApplyFileTreeFilter()
		{
			MultiselectionTreeViewItem root = FilesTreeView.RootItem;
			if (root == null)
			{
				return;
			}
			ApplyTreeFilter(root, _fileTreeFilter);
		}

		// internal 供回归测试直测（InternalsVisibleTo）：对任意树根应用过滤串。
		internal static void ApplyTreeFilter(MultiselectionTreeViewItem root, string filterString)
		{
			string filter = filterString?.Trim();
			bool filtering = !string.IsNullOrEmpty(filter);
			FilterSubtree(root, filtering ? filter : null, isRoot: true);
			if (filtering)
			{
				ExpandVisibleDirectories(root);
			}
		}

		private static bool FilterSubtree(MultiselectionTreeViewItem node, string filter, bool isRoot)
		{
			// 返回该子树是否有可见内容。root（FileTreeItem 为 null）恒可见。
			bool selfMatch = isRoot || filter == null || MatchesFilter(node, filter);
			bool anyChildVisible = false;
			for (int i = 0; i < node.Children.Count; i++)
			{
				anyChildVisible |= FilterSubtree(node.Children[i], filter, isRoot: false);
			}
			node.IsHidden = !(selfMatch || anyChildVisible);
			return selfMatch || anyChildVisible;
		}

		private static bool MatchesFilter(MultiselectionTreeViewItem node, string filter)
		{
			return node is RevisionFileTreeViewItem fileNode
				&& fileNode.FileTreeItem?.FilePath?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void ExpandVisibleDirectories(MultiselectionTreeViewItem node)
		{
			for (int i = 0; i < node.Children.Count; i++)
			{
				MultiselectionTreeViewItem child = node.Children[i];
				if (child.IsHidden)
				{
					continue;
				}
				if (child.Children.Count > 0 && !child.IsExpanded)
				{
					child.IsExpanded = true;
				}
				ExpandVisibleDirectories(child);
			}
		}

		private void FilesTreeView_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			Point point;
			Point? position = e.TryGetPosition(FilesTreeView, out point) ? point : null;
			if (OpenFileTreeContextMenu(e.Source, position))
			{
				e.Handled = true;
				return;
			}
			e.Handled = true;
			FilesTreeView.ContextMenu?.Close();
		}

		private void FilesTreeView_ContextMenuPointerPressed(object sender, PointerPressedEventArgs e)
		{
			PointerPointProperties properties = e.GetCurrentPoint(FilesTreeView).Properties;
			if (!properties.IsRightButtonPressed && properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
			{
				return;
			}
			e.Handled = true;
			FilesTreeView.ContextMenu?.Close();
			if (!OpenFileTreeContextMenu(e.Source, e.GetPosition(FilesTreeView)))
			{
				FilesTreeView.ContextMenu?.Close();
			}
		}

		private bool OpenFileTreeContextMenu(object source, Point? position)
		{
			if (!TrySelectContextMenuTarget(source, position) || FilesTreeView.SelectedItem is not RevisionFileTreeViewItem revisionFileTreeViewItem)
			{
				return false;
			}
			RepositoryUserControl repositoryUserControl = RevisionDetailsUserControl.RepositoryUserControl;
			if (repositoryUserControl != null)
			{
				RepositoryData repositoryData = repositoryUserControl.RepositoryData;
				if (repositoryData != null)
				{
					GitModule gitModule = repositoryUserControl.GitModule;
					if (gitModule != null)
					{
						Sha? sha = _sha;
						if (sha.HasValue)
						{
							Sha valueOrDefault = sha.GetValueOrDefault();
							FilesTreeView.ContextMenu.PlacementTarget = FilesTreeView;
							FilesTreeView.ContextMenu.SetItems(CreateFileTreeViewContextMenuItems(repositoryUserControl, repositoryData, gitModule, valueOrDefault, revisionFileTreeViewItem.FileTreeItem));
							global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(FilesTreeView.ContextMenu, FilesTreeView);
							FilesTreeView.ContextMenu.Open();
							return FilesTreeView.ContextMenu.Items.Count > 0;
						}
					}
				}
			}
			return false;
		}

		private void Expand(string[] pathComponents, MultiselectionTreeViewItemCollection items)
		{
			if (pathComponents.Length == 0)
			{
				return;
			}
			string text = pathComponents[0];
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i] is RevisionFileTreeViewItem revisionFileTreeViewItem && revisionFileTreeViewItem.Title == text)
				{
					if (revisionFileTreeViewItem.ShowExpander)
					{
						revisionFileTreeViewItem.IsExpanded = true;
						Expand(pathComponents.Skip(1).ToArray(), revisionFileTreeViewItem.Children);
					}
					else
					{
						UpdateFileDetails(revisionFileTreeViewItem);
						FilesTreeView.SelectedItems.Clear();
						FilesTreeView.SelectedItems.Add(revisionFileTreeViewItem);
						FilesTreeView.SelectedItem = revisionFileTreeViewItem;
						FilesTreeView.ScrollIntoView(revisionFileTreeViewItem);
					}
					break;
				}
			}
		}

		private bool TrySelectContextMenuTarget(object source, Point? position)
		{
			TreeViewControlItem container = FindVisualAncestorOrSelf<TreeViewControlItem>(source as global::Avalonia.Visual);
			if (container == null && position.HasValue)
			{
				container = FilesTreeView.GetContainerAtPoint<TreeViewControlItem>(position.Value);
			}
			if (container?.DataContext is not RevisionFileTreeViewItem item || item.FileTreeItem == null)
			{
				return false;
			}
			if (!item.IsSelected)
			{
				FilesTreeView.SelectedItems.Clear();
				FilesTreeView.SelectedItems.Add(item);
				FilesTreeView.SelectedItem = item;
				container.IsSelected = true;
				container.InvalidateVisual();
			}
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

		private static IEnumerable<Control> CreateFileTreeViewContextMenuItems(RepositoryUserControl repositoryUserControl, RepositoryData repositoryData, GitModule gitModule, Sha sha, FileTreeItem fileTreeItem)
		{
			FileTreeItem.FileTreeItemType itemType = fileTreeItem.ItemType;
			string filePath = fileTreeItem.FilePath;
			ChangedFile changedFile = new ChangedFile(filePath, StatusType.Modified);
			bool isSubmodule = repositoryData.Submodules.Items.AnyItem((Submodule x) => x.Path == filePath);
			if (!isSubmodule && itemType == FileTreeItem.FileTreeItemType.File)
			{
				bool isEnabled = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(gitModule, filePath);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(gitModule, sha.ToString(), changedFile);
				}, isEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, filePath);
			});
			yield return new Separator();
			if (!isSubmodule)
			{
				bool isEnabled = itemType == FileTreeItem.FileTreeItemType.File || itemType == FileTreeItem.FileTreeItemType.Submodule;
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(repositoryUserControl, filePath, sha);
				}, isEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				ShowFileHistoryWindowCommand.Mode mode = null;
				if (itemType == FileTreeItem.FileTreeItemType.Directory)
				{
					mode = new ShowFileHistoryWindowCommand.Mode.Directory(fileTreeItem.FilePath);
				}
				else if (fileTreeItem.ItemType == FileTreeItem.FileTreeItemType.File)
				{
					mode = new ShowFileHistoryWindowCommand.Mode.File(fileTreeItem.FilePath);
				}
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, mode, sha);
			});
			if (!isSubmodule)
			{
				yield return new Separator();
				yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.SaveFile.Execute(repositoryUserControl, changedFile, sha.ToString());
				}, itemType != FileTreeItem.FileTreeItemType.Directory);
				if (repositoryData.GitLfsInitialized && repositoryData.Remotes.HasLfsCompatibleRemotes())
				{
					MenuItem lfsMenuItem = new MenuItem();
					lfsMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("LFS");
					lfsMenuItem.IsEnabled = itemType == FileTreeItem.FileTreeItemType.File;
					lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsLockCommand.CreateMenuItem(delegate
					{
						RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, new string[1] { filePath });
					}));
					lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsUnlockCommand.CreateMenuItem(delegate
					{
						RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, new string[1] { filePath });
					}));
					yield return new Separator();
					yield return lfsMenuItem;
				}
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, filePath, sha);
			if (isSubmodule)
			{
				Submodule submodule = IReadOnlyListExtensions.FirstItem(repositoryData.Submodules.Items, (Submodule x) => x.Path == filePath);
				if (submodule != null)
				{
					customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
					CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
					{
						new CustomCommandEnvironment.SubmoduleParameter(submodule)
					};
					env = new CustomCommandEnvironment(gitModule, parameters);
				}
			}
			if (customCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> customMenuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in customCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, customMenuItems);
					}
				}
				foreach (MenuItem menuItem in customMenuItems)
				{
					yield return menuItem;
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { filePath });
			});
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, new string[1] { filePath });
			});
		}

		private void RestoreFilesTreeViewColumnWidth()
		{
			double revisionDetailsFileTreeColumnWidth = ForkPlusSettings.Default.RevisionDetailsFileTreeColumnWidth;
			ContainerGrid.ColumnDefinitions[0].Width = new GridLength(revisionDetailsFileTreeColumnWidth, GridUnitType.Pixel);
		}

		private void SaveFilesTreeViewColumnWidth()
		{
			double value = ContainerGrid.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.RevisionDetailsFileTreeColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

	}
}
