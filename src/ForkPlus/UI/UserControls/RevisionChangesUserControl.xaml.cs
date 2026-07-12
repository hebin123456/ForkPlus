using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git.Interaction;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionChangesUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ChangedFile[] _changedFiles = new ChangedFile[0];

		private int _diffRequestId;

		[Null]
		private RevisionDiffTarget _target;

		[Null]
		private DiffPopupWindow _diffPopupWindow;

		private readonly DelayedAction<string> _refreshFilterAction;

		private readonly DelayedAction<ChangedFile> _updateDiffAction;

		public FileListMode FileListsMode
		{
			get
			{
				return FileListUserControl.Mode;
			}
			set
			{
				FileListUserControl.Mode = value;
				FileListUserControl.Refresh();
			}
		}

		private bool DiffShowEntireFile
		{
			get
			{
				switch (FileDiffControl.Target)
				{
				case FileDiffControlTarget.Revision:
				case FileDiffControlTarget.Commit:
				case FileDiffControlTarget.Popup:
				case FileDiffControlTarget.History:
				case FileDiffControlTarget.HunkHistory:
					return ForkPlusSettings.Default.DiffShowEntireFile;
				case FileDiffControlTarget.RevisionWindow:
					return ForkPlusSettings.Default.RevisionWindowDiffShowEntireFile;
				default:
					return ForkPlusSettings.Default.DiffShowEntireFile;
				}
			}
		}

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		[Null]
		public ChangedFile SelectedFile => FileListUserControl.SelectedItems.FirstItem();

		public RevisionChangesUserControl()
		{
			InitializeComponent();
			FileListUserControl fileListUserControl = FileListUserControl;
			fileListUserControl.SelectionChanged = (EventHandler<FileListEventArgs>)Delegate.Combine(fileListUserControl.SelectionChanged, new EventHandler<FileListEventArgs>(FileListUserControl_SelectionChanged));
			FilterTextBox.FilterRequestChanged += FilterTextBox_FilterRequestChanged;
			_refreshFilterAction = new DelayedAction<string>(UpdateFilter);
			_updateDiffAction = new DelayedAction<ChangedFile>(UpdateDiff, 0.05);
			RefreshFileListMode();
			RestoreFileListColumnWidth();
			base.Loaded += delegate
			{
				FileDiffControl.RepositoryUserControl = RevisionDetailsUserControl.RepositoryUserControl;
				FileDiffControl.Content = null;
				if (RevisionDetailsUserControl.Mode == RevisionDetailsUserControlMode.DetachedWindow || RevisionDetailsUserControl.Mode == RevisionDetailsUserControlMode.AiReview)
				{
					FileDiffControl.Target = FileDiffControlTarget.RevisionWindow;
				}
				else
				{
					FileDiffControl.Target = FileDiffControlTarget.Revision;
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
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
			FileListUserControl.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Space && !Keyboard.IsKeyDown(Key.LeftCtrl))
				{
					ShowDiffPopup();
					e.Handled = true;
				}
			};
			FileListUserControl.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return && _target != null && (RevisionDetailsUserControl.Mode == RevisionDetailsUserControlMode.MainWindow || RevisionDetailsUserControl.Mode == RevisionDetailsUserControlMode.InteractiveRebase))
				{
					RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(RevisionDetailsUserControl.GitModule, _target, FileListUserControl.SelectedItems.FirstItem()?.Path);
				}
			};
			GridSplitter.DragCompleted += delegate
			{
				SaveFileListColumnWidth();
			};
			WeakEventManager<NotificationCenter, EventArgs<FileListMode>>.AddHandler(NotificationCenter.Current, "FileListModeChanged", delegate
			{
				RefreshFileListMode();
			});
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current, "DiffContextSizeChanged", delegate
			{
				_updateDiffAction.InvokeWithDelay(SelectedFile);
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffIgnoreWhitespacesChanged", delegate
			{
				_updateDiffAction.InvokeWithDelay(SelectedFile);
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffShowEntireFileChanged", delegate
			{
				_updateDiffAction.InvokeWithDelay(SelectedFile);
			});
			base.CommandBindings.Add(RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateShortcutCommandBinding(delegate
			{
				ChangedFile changedFile2 = FileListUserControl.SelectedItems.FirstItem();
				if (changedFile2 != null)
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(RevisionDetailsUserControl.GitModule, _target.Sha.ToString(), changedFile2);
				}
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.CopyFilePaths.CreateShortcutCommandBinding(delegate
			{
				string[] filePaths2 = FileListUserControl.SelectedItems.CompactMap((ChangedFile x) => x.Path);
				RepositoryUserControl.Commands.CopyFilePaths.Execute(filePaths2);
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateShortcutCommandBinding(delegate
			{
				string[] filePaths = FileListUserControl.SelectedItems.CompactMap((ChangedFile x) => x.Path);
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(RevisionDetailsUserControl.GitModule, filePaths);
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.RunExternalDiffTool.CreateShortcutCommandBinding(delegate
			{
				ChangedFile[] selectedItems = FileListUserControl.SelectedItems;
				ChangedFile changedFile = FileListUserControl.SelectedItems.FirstItem();
				List<ExternalTool> list = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (selectedItems.Length == 1 && !changedFile.IsDirectory && list.Count > 0)
				{
					RunExternalDiffToolCommand.DiffTarget diffTarget;
					if (_target is RevisionDiffTarget.WorkingDirectory)
					{
						diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectorySha(changedFile, _target.Sha.ToString());
					}
					else
					{
						Sha? otherSha = (_target as RevisionDiffTarget.Range)?.OtherSha;
						diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(_target.Sha, otherSha, changedFile);
					}
					ExternalTool diffTool = list.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list[0];
					RepositoryUserControl.Commands.RunExternalDiffTool.Execute(RevisionDetailsUserControl.RepositoryUserControl, diffTarget, diffTool);
				}
			}));
		}

		public void ApplyLocalization()
		{
			Preferences.PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			FileDiffControl.ApplyLocalization();
		}

		public void Refresh(RevisionDiffTarget target, [Null] string fileToSelect)
		{
			_target = target;
			_changedFiles = RevisionDetailsUserControl.FullRevisionDetails.ChangedFiles;
			UpdateFileList(fileToSelect);
		}

		private void FileListUserControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (!HasSelectedItems(FileListUserControl))
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
						RevisionDiffTarget target = _target;
						if (target != null)
						{
							FileListUserControl.ContextMenu.SetItems(CreateFileListContextMenuItems(RevisionDetailsUserControl, repositoryUserControl, repositoryData, gitModule, target, FileListUserControl.SelectedItems));
							return;
						}
					}
				}
			}
			e.Handled = true;
		}

		private static IEnumerable<Control> CreateFileListContextMenuItems(RevisionDetailsUserControl revisionDetailsUserControl, RepositoryUserControl repositoryUserControl, RepositoryData repositoryData, GitModule gitModule, RevisionDiffTarget target, ChangedFile[] changedFiles)
		{
			ChangedFile changedFile = changedFiles.FirstItem();
			bool isSubmodule = false;
			SubmoduleChangedFile submoduleChangedFile = null;
			if (changedFiles.Length == 1)
			{
				submoduleChangedFile = changedFile as SubmoduleChangedFile;
				if (submoduleChangedFile != null)
				{
					isSubmodule = true;
					yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem(delegate
					{
						RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, new Submodule[1] { submoduleChangedFile.Submodule });
					});
					yield return new Separator();
				}
			}
			if (!isSubmodule)
			{
				bool editorIsEnabled = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(gitModule, changedFile.Path);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(gitModule, changedFile.Path);
				}, editorIsEnabled);
				List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (diffTools.Count > 0)
				{
					bool diffIsEnabled = changedFiles.Length == 1 && !changedFile.IsDirectory;
					RunExternalDiffToolCommand.DiffTarget diffTarget;
					if (target is RevisionDiffTarget.WorkingDirectory)
					{
						diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectorySha(changedFile, target.Sha.ToString());
					}
					else
					{
						Sha? otherSha = (target as RevisionDiffTarget.Range)?.OtherSha;
						diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(target.Sha, otherSha, changedFile);
					}
					if (diffTools.Count == 1)
					{
						ExternalTool diffTool = diffTools[0];
						yield return RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem("Diff in " + diffTool.Name, delegate
						{
							RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool);
						}, diffIsEnabled);
					}
					else if (diffTools.Count > 1)
					{
						MenuItem diffMenuItem = new MenuItem
						{
							Header = Preferences.PreferencesLocalization.MenuHeader("External Diff"),
							IsEnabled = diffIsEnabled
						};
						foreach (ExternalTool diffTool in diffTools)
						{
							ExternalTool capturedDiffTool = diffTool;
							diffMenuItem.Items.Add(RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem(capturedDiffTool.Name ?? "", delegate
							{
								RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, capturedDiffTool);
							}, isEnabled: true, null, capturedDiffTool.IsPrimary));
						}
						yield return diffMenuItem;
					}
				}
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, changedFile.Path);
			});
			yield return new Separator();
			yield return CreateResetToRevisionMenuItem(repositoryUserControl, gitModule, target, changedFiles);
			yield return new Separator();
			if (!isSubmodule)
			{
				bool blameIsEnabled = changedFiles.Length == 1 && !changedFile.IsDirectory;
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(repositoryUserControl, changedFile.Path, target.Sha);
				}, blameIsEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, changedFile.Mode(), target.Sha);
			}, changedFiles.Length == 1);
			if (target is RevisionDiffTarget.Revision)
			{
				bool showInFileTreeIsEnabled = changedFiles.Length == 1 && !changedFile.IsDirectory;
				yield return RepositoryUserControl.Commands.ShowFileInFileTree.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowFileInFileTree.Execute(revisionDetailsUserControl, changedFile.Path);
				}, showInFileTreeIsEnabled);
			}
			if (!isSubmodule && repositoryData.GitLfsInitialized && repositoryData.Remotes.HasLfsCompatibleRemotes())
			{
				MenuItem lfsMenuItem = new MenuItem();
				lfsMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("LFS");
				lfsMenuItem.IsEnabled = changedFiles.Length == 1 && !changedFile.IsDirectory;
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsLockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsUnlockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				yield return new Separator();
				yield return lfsMenuItem;
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, changedFile.Path, target.Sha);
			if (changedFile is SubmoduleChangedFile customCommandSubmoduleChangedFile)
			{
				customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(customCommandSubmoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(gitModule, parameters);
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
			if (!isSubmodule && !(target is RevisionDiffTarget.WorkingDirectory))
			{
				bool saveIsEnabled = changedFiles.Length == 1 && !changedFile.IsDirectory;
				yield return new Separator();
				yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.SaveFile.Execute(repositoryUserControl, changedFile, target.Sha.ToString());
				}, saveIsEnabled);
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(changedFiles.CompactMap((ChangedFile x) => x.Path));
			});
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, changedFiles.CompactMap((ChangedFile x) => x.Path));
			});
		}

		private static MenuItem CreateResetToRevisionMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, RevisionDiffTarget target, ChangedFile[] selectedFiles)
		{
			bool isEnabled = !selectedFiles.Any((ChangedFile x) => x.IsDirectory);
			MenuItem menuItem = new MenuItem
			{
				Header = ((selectedFiles.Length == 1) ? "Reset File to" : $"Reset {selectedFiles.Length} Files to"),
				IsEnabled = isEnabled
			};
			RevisionDiffTarget.Revision revision = target as RevisionDiffTarget.Revision;
			if (revision != null)
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State At Commit...", delegate
				{
					RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, selectedFiles, revision.Sha.ToString());
				}));
				menuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State Before Commit...", delegate
				{
					RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, selectedFiles, revision.Sha.ToString() + "~");
				}));
			}
			else
			{
				RevisionDiffTarget.Range range = target as RevisionDiffTarget.Range;
				if (range != null)
				{
					menuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State at Commit '" + range.Sha.ToAbbreviatedString() + "'...", delegate
					{
						RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, selectedFiles, range.Sha.ToString());
					}));
					menuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State at Commit '" + range.OtherSha.ToAbbreviatedString() + "'...", delegate
					{
						RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, selectedFiles, range.OtherSha.ToString());
					}));
				}
			}
			return menuItem;
		}

		private void FileListUserControl_SelectionChanged(object sender, FileListEventArgs e)
		{
			_updateDiffAction.InvokeWithDelay(e.SelectedFile);
		}

		private void FilterTextBox_FilterRequestChanged(object sender, EventArgs e)
		{
			_refreshFilterAction.InvokeWithDelay(FilterTextBox.FilterRequest);
		}

		private void UpdateFileList([Null] string fileToSelect)
		{
			bool restoreSelection = fileToSelect == null && _changedFiles.Length != 0;
			FileListUserControl.SetItemSource(_changedFiles, forceRefresh: true, restoreSelection);
			if (fileToSelect != null)
			{
				FileListUserControl.SelectFile(fileToSelect);
			}
			if (FileListUserControl.SelectedItems.Length == 0)
			{
				FileListUserControl.SelectFirstAvailableFile();
			}
			if (FileListUserControl.SelectedItems.Length == 0)
			{
				_updateDiffAction.Cancel();
				FileDiffControl.Content = null;
				_diffPopupWindow?.UpdateDiff(null);
			}
			else
			{
				_updateDiffAction.InvokeWithDelay(FileListUserControl.SelectedItems.FirstItem());
			}
		}

		private void UpdateDiff(ChangedFile changedFile)
		{
			if (changedFile == null || changedFile.IsDirectory)
			{
				FileDiffControl.Content = null;
				_diffPopupWindow?.UpdateDiff(null);
				return;
			}
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RevisionDiffTarget target = _target;
			if (target == null)
			{
				return;
			}
			int requestId = ++_diffRequestId;
			int contextSize = ForkPlusSettings.Default.DiffContextSize;
			int tabWidth = gitModule.Settings.TabWidth;
			bool ignoreWhitespaces = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
			bool showEntireFile = DiffShowEntireFile;
			ChangedFile capturedFile = changedFile;
			// 异步执行 diff 计算，避免大文件 diff 阻塞 UI 线程。
			Task<GitCommandResult<DiffContent>> task = new Task<GitCommandResult<DiffContent>>(() => new GetRevisionFileChangesGitCommand().Execute(gitModule, target, capturedFile, contextSize, tabWidth, ignoreWhitespaces, showEntireFile));
			task.ContinueWith(delegate(Task<GitCommandResult<DiffContent>> diffTask)
			{
				if (diffTask.IsFaulted || diffTask.IsCanceled)
				{
					return;
				}
				// 期间若又切换了文件或 target，丢弃本次过期结果。
				if (requestId != _diffRequestId)
				{
					return;
				}
				GitCommandResult<DiffContent> gitCommandResult = diffTask.Result;
				FileDiffControl.Content = gitCommandResult;
				_diffPopupWindow?.UpdateDiff(gitCommandResult);
			}, TaskScheduler.FromCurrentSynchronizationContext());
			task.Start();
		}

		private void UpdateFilter(string filterString)
		{
			FileListUserControl.FilterString = filterString;
		}

		private void RefreshFileListMode()
		{
			FileListsMode = ForkPlusSettings.Default.FileListMode;
		}

		private void ShowDiffPopupButton_Click(object sender, RoutedEventArgs e)
		{
			ShowDiffPopup();
		}

		private void FileListSettingsDropdownButton_ContextMenuOpened(object sender, RoutedEventArgs e)
		{
			UpdateFileListSettingsDropdownMenuItems(FileListSettingsDropdownButton.ContextMenu);
		}

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			if (FilterTextBox.IsAnimationPlaceholderVisible)
			{
				FilterTextBox.HideWithAnimation();
			}
			else
			{
				FilterTextBox.ShowWithAnimation();
			}
		}

		private static bool HasSelectedItems(FileListUserControl fileList)
		{
			return fileList.TreeView.SelectedItems.Count > 0;
		}

		private void ShowDiffPopup()
		{
			if (_diffPopupWindow == null)
			{
				_diffPopupWindow = CreateNewDiffPopupWindow();
				_diffPopupWindow.ShowAtCenter(Application.Current.MainWindow);
				_diffPopupWindow.UpdateDiff(FileDiffControl.Content);
			}
			else
			{
				_diffPopupWindow.UpdateDiff(FileDiffControl.Content);
				_diffPopupWindow.Focus();
			}
		}

		private DiffPopupWindow CreateNewDiffPopupWindow()
		{
			DiffPopupWindow diffPopupWindow = DiffPopupWindow.CreateRevisionDiff(RevisionDetailsUserControl.RepositoryUserControl);
			diffPopupWindow.Closed += delegate
			{
				_diffPopupWindow = null;
			};
			diffPopupWindow.SelectPrevious = (EventHandler)Delegate.Combine(diffPopupWindow.SelectPrevious, (EventHandler)delegate
			{
				FileListUserControl.SelectPreviousFile();
			});
			diffPopupWindow.SelectNext = (EventHandler)Delegate.Combine(diffPopupWindow.SelectNext, (EventHandler)delegate
			{
				FileListUserControl.SelectNextFile();
			});
			return diffPopupWindow;
		}

		private void RestoreFileListColumnWidth()
		{
			double revisionDetailsChangesColumnWidth = ForkPlusSettings.Default.RevisionDetailsChangesColumnWidth;
			ContainerGrid.ColumnDefinitions[0].Width = new GridLength(revisionDetailsChangesColumnWidth, GridUnitType.Pixel);
		}

		private void SaveFileListColumnWidth()
		{
			double value = ContainerGrid.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.RevisionDetailsChangesColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void UpdateFileListSettingsDropdownMenuItems(ContextMenu menu)
		{
			menu.Items.Clear();
			FileListMode fileListMode = ForkPlusSettings.Default.FileListMode;
			MenuItem menuItem = new MenuItem();
			menuItem.Header = Preferences.PreferencesLocalization.MenuHeader("View as Tree");
			menuItem.IsChecked = fileListMode == FileListMode.Tree;
			menuItem.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.Tree;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.Tree);
			};
			menu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = Preferences.PreferencesLocalization.MenuHeader("View as List");
			menuItem2.IsChecked = fileListMode == FileListMode.List;
			menuItem2.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.List;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.List);
			};
			menu.Items.Add(menuItem2);
			MenuItem menuItem3 = new MenuItem();
			menuItem3.Header = Preferences.PreferencesLocalization.MenuHeader("View as Combined List");
			menuItem3.IsChecked = fileListMode == FileListMode.CombinedList;
			menuItem3.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.CombinedList;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.CombinedList);
			};
			menu.Items.Add(menuItem3);
		}

	}
}
