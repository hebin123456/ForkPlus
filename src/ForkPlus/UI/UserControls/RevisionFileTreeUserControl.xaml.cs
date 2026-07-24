// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/ListBoxItem/Separator/MenuItem/ContextMenu/ContextMenuEventArgs/SelectionChangedEventArgs/GridLength/GridUnitType）
// - using System.Windows.Input → using Avalonia.Input（Key/TappedEventArgs）
// - using System.Windows.Markup → 移除
// - CommandBindings.Add(command.CreateShortcutCommandBinding(h)) → KeyBindings.Add(command.CreateShortcutKeyBinding(h))（参考 IUICommandExtension/RevisionListViewUserControl）
// - MouseDoubleClick + MouseButtonEventArgs → DoubleTapped + RoutedEventArgs（参考 FileListUserControl.TreeView.DoubleTapped += TreeView_MouseDoubleClick）
//   构造函数中显式订阅 FilesTreeView.DoubleTapped += FilesTreeView_MouseDoubleClick（XAML 的 MouseDoubleClick 事件在 Avalonia 不存在，由 XAML 迁移阶段移除）
// - e.OriginalSource → e.Source（参考 MultiselectionTreeView）
// - (e.Source as DependencyObject)?.GetParent<ListBoxItem>() → (e.Source as AvaloniaObject)?.GetParent<ListBoxItem>()（参考 DependencyObjectExtensions）
// - ItemContainerGenerator.ItemFromContainer → 保留（Avalonia API 兼容，参考 ItemsControlExtensions）
// - GridSplitter.DragCompleted 保留（已在 RepositoryContentUserControl/CommitUserControl 等迁移文件中验证可用）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionFileTreeUserControl : UserControl
	{
		private Sha? _sha;

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		public RevisionFileTreeUserControl()
		{
			InitializeComponent();
			RestoreFilesTreeViewColumnWidth();
			GridSplitter.DragCompleted += delegate
			{
				SaveFilesTreeViewColumnWidth();
			};
			// 阶段 4.5：WPF MouseDoubleClick → Avalonia DoubleTapped（参考 FileListUserControl.TreeView.DoubleTapped += TreeView_MouseDoubleClick）。
			FilesTreeView.DoubleTapped += FilesTreeView_MouseDoubleClick;
			// 阶段 4.5：WPF CommandBindings.Add(CreateShortcutCommandBinding) → Avalonia KeyBindings.Add(CreateShortcutKeyBinding)（参考 RevisionListViewUserControl）。
			base.KeyBindings.Add(RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateShortcutKeyBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem3 = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem3 != null)
				{
					ChangedFile changedFile = new ChangedFile(revisionFileTreeViewItem3.FileTreeItem.FilePath, staged: true);
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(RevisionDetailsUserControl.GitModule, _sha.ToString(), changedFile);
				}
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.CopyFilePaths.CreateShortcutKeyBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem2 = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem2 != null)
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { revisionFileTreeViewItem2.FileTreeItem.FilePath });
				}
			}));
			base.KeyBindings.Add(RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateShortcutKeyBinding(delegate
			{
				RevisionFileTreeViewItem revisionFileTreeViewItem = FilesTreeView.SelectedItems.FirstItem<RevisionFileTreeViewItem>();
				if (revisionFileTreeViewItem != null)
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(RevisionDetailsUserControl.GitModule, new string[1] { revisionFileTreeViewItem.FileTreeItem.FilePath });
				}
			}));
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

		// 阶段 4.5：WPF MouseDoubleClick(MouseButtonEventArgs) → Avalonia DoubleTapped(RoutedEventArgs)（参考 FileListUserControl）。
		// e.OriginalSource → e.Source（参考 MultiselectionTreeView）；DependencyObject → AvaloniaObject（参考 DependencyObjectExtensions）。
		private void FilesTreeView_MouseDoubleClick(object sender, RoutedEventArgs e)
		{
			ListBoxItem listBoxItem = (e.Source as AvaloniaObject)?.GetParent<ListBoxItem>();
			if (listBoxItem != null && FilesTreeView.ItemContainerGenerator.ItemFromContainer(listBoxItem) is RevisionFileTreeViewItem revisionFileTreeViewItem)
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
			FileContentControl.Content = null;
			Task<GitCommandResult<Content>> task = new Task<GitCommandResult<Content>>(() => new GetFileContentGitCommand().Execute(gitModule, sha, fileTreeItem.FileTreeItem.FilePath));
			task.ContinueWith(delegate(Task<GitCommandResult<Content>> getFileContentTask)
			{
				if (FilesTreeView.SelectedItems.Count > 0 && FilesTreeView.SelectedItems[0] == fileTreeItem)
				{
					FileContentControl.Content = getFileContentTask.Result;
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			task.Start();
		}

		public void Refresh(Sha sha)
		{
			_sha = sha;
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			RepositoryUserControl repositoryUserControl = RevisionDetailsUserControl.RepositoryUserControl;
			FileContentControl.RepositoryUserControl = repositoryUserControl;
			FileContentControl.Content = null;
			Sha shaValue = sha;
			// 异步执行 `git ls-tree` 避免阻塞 UI 线程，完成后回到 UI 线程构建树。
			Task<GitCommandResult<FileTreeItem[]>> task = new Task<GitCommandResult<FileTreeItem[]>>(() => new GetRevisionFileTreeGitCommand().Execute(gitModule, "", shaValue));
			task.ContinueWith(delegate(Task<GitCommandResult<FileTreeItem[]>> treeTask)
			{
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
				string text = obj?.FileTreeItem.FilePath;
				if (text != null)
				{
					string[] pathComponents = text.Split('/');
					Expand(pathComponents, FilesTreeView.RootItem.Children);
				}
				// 若有待展开的文件路径（来自 ShowRevisionDetails），现在 RootItem 已就绪，执行展开。
				if (_pendingFilePath != null)
				{
					string[] pathComponents = _pendingFilePath.Split('/');
					Expand(pathComponents, FilesTreeView.RootItem.Children);
					_pendingFilePath = null;
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			task.Start();
		}

		private string _pendingFilePath;

		public void ShowRevisionDetails(string filePath)
		{
			// RootItem 可能尚未就绪（Refresh 是异步的），保存路径等异步回调完成后展开。
			if (FilesTreeView.RootItem == null)
			{
				_pendingFilePath = filePath;
				return;
			}
			string[] pathComponents = filePath.Split('/');
			Expand(pathComponents, FilesTreeView.RootItem.Children);
		}

		private void FilesTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!(FilesTreeView.SelectedItem is RevisionFileTreeViewItem revisionFileTreeViewItem))
			{
				e.Handled = true;
				return;
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
							FilesTreeView.ContextMenu.SetItems(CreateFileTreeViewContextMenuItems(repositoryUserControl, repositoryData, gitModule, valueOrDefault, revisionFileTreeViewItem.FileTreeItem));
							return;
						}
					}
				}
			}
			e.Handled = true;
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
						FilesTreeView.SelectedItem = revisionFileTreeViewItem;
						FilesTreeView.ScrollIntoView(revisionFileTreeViewItem);
					}
					break;
				}
			}
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
