// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/ContextMenu/ContextMenuEventArgs/SelectionChangedEventArgs/GridLength/GridUnitType/Separator）
// - using System.Windows.Input → using Avalonia.Input（DragEventArgs/DragDropEffects）
// - using System.Windows.Markup → 移除（IComponentConnector 不需要）
// - 新增 using Avalonia.Platform.Storage（IStorageItem/e.Data.GetFiles）；新增 using System.Linq（Select）
// - WeakEventManager<NotificationCenter, EventArgs<T>>.AddHandler(NotificationCenter.Current, "Event", h)
//   → NotificationCenter.Current.Event += h（直接订阅，参考 FileControlHeaderUserControl）
// - e.Data.GetData(DataFormats.FileDrop) is string[] → e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray()
//   （Avalonia 文件拖放 API，参考 RepositoryUserControl.OnDrop/RepositoryManagerSourceDirectoryItem）
// - RepositoriesTreeView.CommandBindings.Add(cmd.CreateShortcutCommandBinding(h))
//   → RepositoriesTreeView.KeyBindings.Add(cmd.CreateShortcutKeyBinding(h))（参考 RevisionFileTreeUserControl/IUICommandExtension）
// - MouseDoubleClick + MouseButtonEventArgs → DoubleTapped + RoutedEventArgs（参考 FileListUserControl/MultiselectionTreeView.OnDoubleTapped）
//   构造函数显式订阅 RepositoriesTreeView.DoubleTapped += RepositoriesListBox_MouseDoubleClick
//   （XAML 的 MouseDoubleClick 在 Avalonia 不存在，由 XAML 迁移阶段移除）
// - GridSplitter.DragCompleted 保留（已在 RepositoryContentUserControl/CommitUserControl 等迁移文件中验证可用）
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryManagerUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public static readonly RepositoryManagerUserControlCommands Commands = new RepositoryManagerUserControlCommands();

		private bool _handleExpandEvents = true;

		private readonly JobQueue JobQueue = new JobQueue();

		private readonly RepositoryManagerTreeViewItem _root;

		private readonly RepositoryManagerTreeViewItem _repositories;

		private readonly RepositoryManagerTreeViewItem _recent;

		public RepositoryManagerRepositoryItem SelectedRepository => RepositoriesTreeView.SelectedItem as RepositoryManagerRepositoryItem;

		public RepositoryManagerTreeViewItem[] SelectedItems
		{
			get
			{
				IList selectedItems = RepositoriesTreeView.SelectedItems;
				List<RepositoryManagerTreeViewItem> list = new List<RepositoryManagerTreeViewItem>(selectedItems.Count);
				foreach (object item2 in selectedItems)
				{
					if (item2 is RepositoryManagerTreeViewItem item)
					{
						list.Add(item);
					}
				}
				return list.ToArray();
			}
		}

		public RepositoryManagerUserControl()
		{
			InitializeComponent();
			RepositoryDetailsUserControl.RepositoryManagerUserControl = this;
			RepositoriesTreeView.AllowDragDrop = true;
			_root = new RepositoryManagerTreeViewItem(null)
			{
				Title = string.Empty
			};
			_recent = new RepositoryManagerSectionItem(_root, "Recent");
			_repositories = new RepositoryManagerRepositorySectionItem(_root, "Repositories", this);
			_root.IsExpanded = true;
			_recent.IsExpanded = true;
			_repositories.IsExpanded = true;
			_root.Children.Add(_recent);
			_root.Children.Add(_repositories);
			RepositoriesTreeView.RootItem = _root;
			ApplyLocalization();
			Refresh(restoreSelection: false);
			base.Loaded += delegate
			{
				RestoreTreeViewColumnWidth();
				if (_recent.Children.Count > 0)
				{
					SelectFirstRecent();
				}
				else if (_repositories.Children.Count > 0)
				{
					SelectFirstRepository();
				}
			};
			GridSplitter.DragCompleted += delegate
			{
				SaveTreeViewColumnWidth();
			};
			// 阶段 4.5：WPF MouseDoubleClick → Avalonia DoubleTapped（参考 FileListUserControl.TreeView.DoubleTapped += TreeView_MouseDoubleClick）。
			RepositoriesTreeView.DoubleTapped += RepositoriesListBox_MouseDoubleClick;
			// 阶段 4.5：WPF CommandBindings.Add(CreateShortcutCommandBinding) → Avalonia KeyBindings.Add(CreateShortcutKeyBinding)（参考 RevisionFileTreeUserControl/IUICommandExtension）。
			RepositoriesTreeView.KeyBindings.Add(Commands.OpenRepository.CreateShortcutKeyBinding(delegate
			{
				Commands.OpenRepository.Execute(SelectedRepository?.Repository);
			}));
			RepositoriesTreeView.KeyBindings.Add(Commands.RenameRepository.CreateShortcutKeyBinding(delegate
			{
				if (SelectedItems.Length == 1 && SelectedItems[0] is RepositoryManagerRepositoryItem itemToRename)
				{
					Commands.RenameRepository.Execute(itemToRename);
				}
			}));
			RepositoriesTreeView.KeyBindings.Add(Commands.RemoveRepository.CreateShortcutKeyBinding(delegate
			{
				if (SelectedItems.Length != 0 && SameType(SelectedItems) && SameParent(SelectedItems))
				{
					Commands.RemoveRepository.Execute(this, SelectedItems);
				}
			}));
			RepositoriesTreeView.ContextMenuOpening += RepositoriesListBox_ContextMenuOpening;
			// 阶段 4.5：WPF WeakEventManager<NotificationCenter, EventArgs<T>>.AddHandler(NotificationCenter.Current, "Event", h)
			// → Avalonia NotificationCenter.Current.Event += h（直接订阅，参考 FileControlHeaderUserControl）。
			NotificationCenter.Current.RepositoryNameChanged += RepositoryNameChanged;
			NotificationCenter.Current.RepositoryColorChanged += RepositoryColorChanged;
			NotificationCenter.Current.RepositoryManagerRepositoriesUpdated += RepositoriesChanged;
			Task.Run(delegate
			{
				new RescanUserRepositoriesCommand().Execute(this);
			});
		}

		public void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			RepositoryManagerTitleTextBlock.Text = PreferencesLocalization.Translate("Repository Manager", language);
			FallbackMessage.Text = PreferencesLocalization.Translate("Drop repository here to add", language);
			_recent.Title = PreferencesLocalization.Translate("Recent", language);
			_repositories.Title = PreferencesLocalization.Translate("Repositories", language);
			RepositoryDetailsUserControl.ApplyLocalization();
		}

		public void Refresh(bool restoreSelection = true)
		{
			ImportKnownGitMmWorkspaces();
			RepositoryManagerRepositoryItem repositoryManagerRepositoryItem = RepositoriesTreeView.SelectedItem as RepositoryManagerRepositoryItem;
			if (RepositoryManager.Instance.Repositories.Length == 0)
			{
				FallbackView.Show();
				RepositoriesTreeView.Hide();
			}
			else
			{
				FallbackView.Hide();
				RepositoriesTreeView.Show();
			}
			_handleExpandEvents = false;
			_recent.Children.Clear();
			_repositories.Children.Clear();
			CreateRecentRepositories(_recent);
			CreateRepositoryItems(_repositories);
			RepositoriesTreeView.SetExpandedItems(ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems);
			_handleExpandEvents = true;
			if (restoreSelection && repositoryManagerRepositoryItem != null)
			{
				SelectRepositoryItemWithPath(repositoryManagerRepositoryItem.Path, RepositoriesTreeView.RootItem);
			}
		}

		private static void ImportKnownGitMmWorkspaces()
		{
			string[] workspaces = (ForkPlusSettings.Default.GitMm.Workspaces ?? new string[0])
				.Where((string path) => GitMmUserControl.IsGitMmWorkspace(path))
				.ToArray();
			if (workspaces.Length == 0)
			{
				return;
			}
			int repositoriesCount = RepositoryManager.Instance.Repositories.Length;
			RepositoryManager.Instance.AddRepositories(workspaces);
			bool shouldSave = RepositoryManager.Instance.Repositories.Length != repositoriesCount;
			string activeWorkspace = ForkPlusSettings.Default.GitMm.ActiveWorkspace;
			if (!string.IsNullOrWhiteSpace(activeWorkspace) && GitMmUserControl.IsGitMmWorkspace(activeWorkspace))
			{
				RepositoryManager.Repository? activeRepository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository repository) => repository.Path == PathHelper.Normalize(activeWorkspace));
				if (!activeRepository.HasValue || !activeRepository.GetValueOrDefault().Opened.HasValue)
				{
					RepositoryManager.Instance.AddOrUpdateLastOpened(activeWorkspace);
					shouldSave = true;
				}
			}
			if (shouldSave)
			{
				RepositoryManager.Instance.Save();
			}
		}

		private void CreateRecentRepositories(RepositoryManagerTreeViewItem root)
		{
			RepositoryManager.Repository[] array = RepositoryManager.Instance.Repositories.Filter((RepositoryManager.Repository x) => x.Opened.HasValue).ToArray().ToSortedArray((RepositoryManager.Repository lhs, RepositoryManager.Repository rhs) => rhs.Opened.GetValueOrDefault().CompareTo(lhs.Opened.GetValueOrDefault()))
				.Subsequence(0, 5);
			for (int i = 0; i < array.Length; i++)
			{
				RepositoryManager.Repository repository = array[i];
				RepositoryManagerRepositoryItem repositoryItem = new RepositoryManagerRepositoryItem(repository, root);
				root.Children.Add(repositoryItem);
				JobQueue.Add(PreferencesLocalization.Current("Validating repository path..."), delegate
				{
					bool isInvalid = !IsRepositoryManagerPathValid(repository.Path);
					base.Dispatcher.Async(delegate
					{
						repositoryItem.RepositoryIcon = (isInvalid ? Theme.RepositoryWarningIcon : Theme.RepositoryIcon);
					});
				}, JobFlags.Hidden);
			}
		}

		private void CreateRepositoryItems(RepositoryManagerTreeViewItem root)
		{
			string[] array = RepositoryManager.Instance.SourceDirs.ToSortedArray((string x, string y) => NumericIgnoreCaseStringComparer.Comparer.Compare(x, y));
			RepositoryManager.Repository[] array2 = RepositoryManager.Instance.Repositories.ToSortedArray((RepositoryManager.Repository x, RepositoryManager.Repository y) => NumericIgnoreCaseStringComparer.Comparer.Compare(x.Path, y.Path));
			if (array.Length > 1)
			{
				foreach (var (sourceDir, title) in array.Zip(RemoveCommonPrefix(array)))
				{
					root.Children.Add(new RepositoryManagerSourceDirectoryItem(this, title, sourceDir, root));
				}
			}
			RepositoryManager.Repository[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				RepositoryManager.Repository repository = array3[i];
				RepositoryManagerTreeViewItem repositoryManagerTreeViewItem = root;
				if (array.Length > 1)
				{
					using IEnumerator<MultiselectionTreeViewItem> enumerator2 = root.Children.GetEnumerator();
					while (enumerator2.MoveNext() && enumerator2.Current is RepositoryManagerSourceDirectoryItem repositoryManagerSourceDirectoryItem)
					{
						if (repository.Path.StartsWith(repositoryManagerSourceDirectoryItem.SourceDir))
						{
							repositoryManagerTreeViewItem = repositoryManagerSourceDirectoryItem;
							break;
						}
					}
				}
				string text = repository.Folder(array);
				if (text != null)
				{
					string[] array4 = text.Split('\\');
					for (int j = 0; j < array4.Length; j++)
					{
						repositoryManagerTreeViewItem = FindOrCreateFolder(repositoryManagerTreeViewItem, array4[j]);
					}
				}
				string name = GitMmUserControl.IsGitMmWorkspace(repository.Path) ? "git mm: " + repository.Name() : repository.Name();
				int num = repositoryManagerTreeViewItem.Children.BinarySearchBy(delegate(MultiselectionTreeViewItem x)
				{
					if (x is RepositoryManagerSourceDirectoryItem)
					{
						return -1;
					}
					return (x is RepositoryManagerRepositoryFolderItem) ? (-1) : NumericIgnoreCaseStringComparer.Comparer.Compare(x.Title, name);
				});
				if (num >= 0)
				{
					if (repositoryManagerTreeViewItem.Children[num] is RepositoryManagerRepositoryItem repositoryManagerRepositoryItem)
					{
						Log.Warn("Failed to add item '" + repository.Path + "' because item with same name '" + repositoryManagerRepositoryItem.Repository.Path + "' already exists in '" + repositoryManagerTreeViewItem.Title + "'");
					}
					continue;
				}
				RepositoryManagerRepositoryItem newItem = new RepositoryManagerRepositoryItem(repository, repositoryManagerTreeViewItem);
				repositoryManagerTreeViewItem.Children.Insert(~num, newItem);
				JobQueue.Add(PreferencesLocalization.Current("Validating repository path..."), delegate
				{
					bool isInvalid = !IsRepositoryManagerPathValid(repository.Path);
					base.Dispatcher.Async(delegate
					{
						newItem.RepositoryIcon = (isInvalid ? Theme.RepositoryWarningIcon : Theme.RepositoryIcon);
					});
				}, JobFlags.Hidden);
			}
		}

		public void OnDirectoryItemIsExpandedChanged()
		{
			if (_handleExpandEvents)
			{
				ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = RepositoriesTreeView.GetExpandedItems();
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			e.Handled = true;
			base.OnDrop(e);
			// 阶段 4.5：WPF e.Data.GetData(DataFormats.FileDrop) is string[]
			// → Avalonia e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray()（参考 RepositoryUserControl.OnDrop/RepositoryManagerSourceDirectoryItem）。
			string[] source = e.Data.GetFiles()?.Select((IStorageItem f) => f.Path.LocalPath).ToArray();
			if (source != null)
			{
				string[] paths = source.CompactMap((string path) => (GitMmUserControl.IsGitMmWorkspace(path) || new ValidateRepositoryPathGitCommand().Execute(path) == RepositoryValidState.ValidRepository) ? path : null);
				RepositoryManager.Instance.AddRepositories(paths);
				RepositoryManager.Instance.Save();
				Refresh();
			}
		}

		private static bool IsRepositoryManagerPathValid(string path)
		{
			if (GitMmUserControl.IsGitMmWorkspace(path))
			{
				return true;
			}
			return new ValidateRepositoryPathGitCommand().Execute(path) != RepositoryValidState.Invalid;
		}

		private RepositoryManagerRepositoryFolderItem FindOrCreateFolder(RepositoryManagerTreeViewItem parent, string folderName)
		{
			int num = parent.Children.BinarySearchBy((MultiselectionTreeViewItem x) => (!(x is RepositoryManagerRepositoryFolderItem)) ? 1 : NumericIgnoreCaseStringComparer.Comparer.Compare(x.Title, folderName));
			if (num >= 0)
			{
				return (RepositoryManagerRepositoryFolderItem)parent.Children[num];
			}
			RepositoryManagerRepositoryFolderItem repositoryManagerRepositoryFolderItem = new RepositoryManagerRepositoryFolderItem(this, folderName, parent);
			parent.Children.Insert(~num, repositoryManagerRepositoryFolderItem);
			return repositoryManagerRepositoryFolderItem;
		}

		private void RepositoryNameChanged(object sender, EventArgs<string> e)
		{
			Refresh();
			RepositoryDetailsUserControl.RefreshRepositoryName();
		}

		private void RepositoryColorChanged(object sender, EventArgs<RepositoryManager.Repository> e)
		{
			Refresh();
		}

		private void RepositoriesChanged(object sender, EventArgs e)
		{
			Refresh();
		}

		private void RepositoriesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			RepositoryManagerTreeViewItem[] array = ClickedItems(RepositoriesTreeView);
			if (array.ContainsItem((RepositoryManagerTreeViewItem x) => x is RepositoryManagerSectionItem repositoryManagerSectionItem && repositoryManagerSectionItem == _recent))
			{
				e.Handled = true;
				RepositoriesTreeView.ContextMenu.IsOpen = false;
			}
			else
			{
				RepositoriesTreeView.ContextMenu.SetItems(CreateRepositoryContextMenuItems(array));
			}
		}

		// 阶段 4.5：WPF MouseDoubleClick + MouseButtonEventArgs → Avalonia DoubleTapped + RoutedEventArgs（参考 FileListUserControl/MultiselectionTreeView.OnDoubleTapped）。
		private void RepositoriesListBox_MouseDoubleClick(object sender, RoutedEventArgs e)
		{
			MultiselectionTreeViewItem lastClickedItem = RepositoriesTreeView.LastClickedItem;
			if (lastClickedItem is RepositoryManagerRepositoryItem repositoryManagerRepositoryItem)
			{
				Commands.OpenRepository.Execute(repositoryManagerRepositoryItem.Repository);
			}
			else if (lastClickedItem is RepositoryManagerRepositoryFolderItem repositoryManagerRepositoryFolderItem)
			{
				repositoryManagerRepositoryFolderItem.IsExpanded = !repositoryManagerRepositoryFolderItem.IsExpanded;
			}
			else if (lastClickedItem is RepositoryManagerSectionItem repositoryManagerSectionItem)
			{
				repositoryManagerSectionItem.IsExpanded = !repositoryManagerSectionItem.IsExpanded;
			}
		}

		private void RepositoriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			e.Handled = true;
			MultiselectionTreeViewItem[] items = RepositoriesTreeView.SelectedItems.CompactMap((object x) => x as RepositoryManagerRepositoryItem);
			items.RefreshSelectionType();
			RepositoryDetailsUserControl.ShowDetails(SelectedRepository?.Repository);
		}

		public void SelectRepositoryWithPath(string path)
		{
			SelectRepositoryItemWithPath(path, RepositoriesTreeView.RootItem);
		}

		public void SelectFirstRepository()
		{
			SelectItem(_repositories.FlatIndex() + 1);
		}

		private void SelectItem(int index)
		{
			if (RepositoriesTreeView.Items.Count > index)
			{
				RepositoriesTreeView.SelectedIndex = index;
				RepositoriesTreeView.FocusRow(index);
			}
		}

		private void SelectFirstRecent()
		{
			SelectItem(_recent.FlatIndex() + 1);
		}

		private bool SelectRepositoryItemWithPath(string path, MultiselectionTreeViewItem parent)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				if (child is RepositoryManagerRepositoryItem repositoryManagerRepositoryItem)
				{
					if (repositoryManagerRepositoryItem.Path == path)
					{
						RepositoriesTreeView.SelectedItem = repositoryManagerRepositoryItem;
						return true;
					}
					continue;
				}
				if (child is RepositoryManagerSectionItem || child is RepositoryManagerSourceDirectoryItem || child is RepositoryManagerRepositoryFolderItem)
				{
					if (SelectRepositoryItemWithPath(path, child))
					{
						return true;
					}
					continue;
				}
				throw new InvalidOperationException("Unexpected item in RepositoriesTreeView");
			}
			return false;
		}

		private static RepositoryManagerTreeViewItem[] ClickedItems(MultiselectionTreeView treeView)
		{
			if (!(treeView.LastClickedItem is RepositoryManagerTreeViewItem repositoryManagerTreeViewItem))
			{
				return new RepositoryManagerTreeViewItem[0];
			}
			if (treeView.SelectedItems.Contains(repositoryManagerTreeViewItem))
			{
				return treeView.SelectedItems.CompactMap((object x) => x as RepositoryManagerTreeViewItem);
			}
			return new RepositoryManagerTreeViewItem[1] { repositoryManagerTreeViewItem };
		}

		private IEnumerable<Control> CreateRepositoryContextMenuItems(RepositoryManagerTreeViewItem[] selectedItems)
		{
			if (selectedItems.Length == 1)
			{
				RepositoryManagerTreeViewItem selectedItem = selectedItems[0];
				if (selectedItem is RepositoryManagerRepositoryItem repositoryItem)
				{
					yield return Commands.OpenRepository.CreateMenuItem(delegate
					{
						Commands.OpenRepository.Execute(repositoryItem.Repository);
					});
					yield return MainWindow.Commands.OpenRepositoryInFileExplorer.CreateMenuItem(delegate
					{
						MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(repositoryItem.Repository.Path);
					});
					yield return new Separator();
					yield return Commands.RenameRepository.CreateMenuItem("Rename Repository", delegate
					{
						Commands.RenameRepository.Execute(repositoryItem);
					});
					yield return Commands.RemoveRepository.CreateMenuItem(delegate
					{
						Commands.RemoveRepository.Execute(this, selectedItems);
					});
				}
				else if (selectedItem is RepositoryManagerRepositoryFolderItem folder)
				{
					List<string> repositories = new List<string>();
					GetNestedRepos(folder, repositories);
					if (repositories.Count > 0)
					{
						yield return Commands.OpenRepositoriesCommand.CreateMenuItem($"Open All ({repositories.Count})", delegate
						{
							Commands.OpenRepositoriesCommand.Execute(repositories);
						});
						yield return new Separator();
					}
					yield return Commands.RemoveRepository.CreateMenuItem(delegate
					{
						Commands.RemoveRepository.Execute(this, selectedItems);
					});
				}
			}
			else if (selectedItems.Length > 1 && SameType(selectedItems) && SameParent(selectedItems))
			{
				yield return Commands.RemoveRepository.CreateMenuItem(delegate
				{
					Commands.RemoveRepository.Execute(this, selectedItems);
				});
			}

			yield return new Separator();
			yield return Commands.RescanRepositories.CreateMenuItem(delegate
			{
				Commands.RescanRepositories.Execute(this);
			});

			if (selectedItems.Length == 1 && selectedItems[0] is RepositoryManagerRepositoryItem repositoryItemForColors)
			{
				yield return new Separator();
				yield return CreateRepositoryColorsMenuItem(repositoryItemForColors.Repository);
			}
		}

		private static void GetNestedRepos(RepositoryManagerRepositoryFolderItem folder, List<string> result)
		{
			foreach (MultiselectionTreeViewItem child in folder.Children)
			{
				if (child is RepositoryManagerRepositoryFolderItem folder2)
				{
					GetNestedRepos(folder2, result);
				}
				else if (child is RepositoryManagerRepositoryItem repositoryManagerRepositoryItem)
				{
					result.Add(repositoryManagerRepositoryItem.Repository.Path);
				}
			}
		}

		private static Control CreateRepositoryColorsMenuItem(RepositoryManager.Repository repository)
		{
			return new MenuItem
			{
				Header = new RepositoryColorsUserControl(repository),
				Style = Theme.CustomContentMenuItemStyle
			};
		}

		private static bool SameType(RepositoryManagerTreeViewItem[] items)
		{
			RepositoryManagerTreeViewItem repositoryManagerTreeViewItem = items.FirstItem();
			if (repositoryManagerTreeViewItem == null)
			{
				return true;
			}
			bool flag = repositoryManagerTreeViewItem is RepositoryManagerRepositoryFolderItem;
			for (int i = 1; i < items.Length; i++)
			{
				if (!(items[i] is RepositoryManagerRepositoryFolderItem) && flag)
				{
					return false;
				}
				if (!(items[i] is RepositoryManagerRepositoryItem) && !flag)
				{
					return false;
				}
			}
			return true;
		}

		private static bool SameParent(RepositoryManagerTreeViewItem[] items)
		{
			RepositoryManagerTreeViewItem repositoryManagerTreeViewItem = items.FirstItem()?.Parent;
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i].Parent != repositoryManagerTreeViewItem)
				{
					return false;
				}
			}
			return true;
		}

		private void RestoreTreeViewColumnWidth()
		{
			double repositoryManagerTreeViewColumnWidth = ForkPlusSettings.Default.RepositoryManagerTreeViewColumnWidth;
			RepositoryManagerGrid.ColumnDefinitions[0].Width = new GridLength(repositoryManagerTreeViewColumnWidth, GridUnitType.Pixel);
		}

		private void SaveTreeViewColumnWidth()
		{
			double value = RepositoryManagerGrid.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.RepositoryManagerTreeViewColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private string[] RemoveCommonPrefix(string[] paths)
		{
			if (paths.Length <= 1)
			{
				return paths;
			}
			string[][] array = paths.Map((string x) => x.TrimEnd("\\").Split(Consts.Chars.BackSlash));
			int num = ((array.Length != 0) ? array.Map((string[] x) => x.Length).Min() : 0);
			int commonPrefixLength = 0;
			for (int i = 0; i < num; i++)
			{
				string component = array[0][i];
				if (!array.AllItems((string[] x) => x[i] == component))
				{
					break;
				}
				commonPrefixLength = i + 1;
			}
			if (commonPrefixLength <= 0)
			{
				return paths;
			}
			return array.Map(delegate(string[] components)
			{
				string[] array2 = components.SkipFirst(commonPrefixLength);
				return (array2.Length != 0) ? array2.Joined("/") : (components.LastItem() ?? "");
			});
		}

	}
}
