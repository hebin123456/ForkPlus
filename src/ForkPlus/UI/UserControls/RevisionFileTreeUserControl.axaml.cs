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
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionFileTreeUserControl : UserControl
	{
		private Sha? _sha;

		private int _treeRequestId;

		private int _contentRequestId;

		private bool _isRefreshingTree;

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		public RevisionFileTreeUserControl()
		{
			InitializeComponent();
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
