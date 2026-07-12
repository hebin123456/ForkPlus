using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class FileHistoryWindow : CustomWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha? _revisionToSelect;

		[Null]
		private readonly ForkPlus.Git.Reference _targetReference;

		private readonly ShowFileHistoryWindowCommand.Mode _mode;

		private readonly DelayedAction<HistoryEntryViewModel[]> _delayedAction;

		private bool _startUpFinished;

		private RevisionWithFiles[] _revisions;

		private string[] _patches;

		private HistoryEntryViewModel[] _selectedHistoryEntries;

		private MultiselectionTreeViewItem _root = new MultiselectionTreeViewItem();

		[Null]
		private Job _showDiffJob;

		private GitModule GitModule => _repositoryUserControl.GitModule;

		public FileHistoryWindow(RepositoryUserControl repositoryUserControl, ShowFileHistoryWindowCommand.Mode mode, Sha? revisionToSelect, [Null] ForkPlus.Git.Reference targetReference)
		{
			_repositoryUserControl = repositoryUserControl;
			_mode = mode;
			_revisionToSelect = revisionToSelect;
			_targetReference = targetReference;
			base.Title = PathHelper.GetReadableFileName(mode.Path) + " - " + Translate("History");
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			InitializeComponent();
			HistoryTitleTextBlock.Text = Translate("History");
			_delayedAction = new DelayedAction<HistoryEntryViewModel[]>(RefreshDiff);
			TreeView.RootItem = _root;
			if (mode is ShowFileHistoryWindowCommand.Mode.Directory || mode is ShowFileHistoryWindowCommand.Mode.Hunk)
			{
				TreeView.SelectionMode = SelectionMode.Single;
			}
			else if (mode is ShowFileHistoryWindowCommand.Mode.File)
			{
				TreeView.SelectionMode = SelectionMode.Extended;
			}
			FileIcon.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(mode.Path));
			FileNameTextBlock.FilePath = mode.Path;
			FileNameTextBlock.ToolTip = mode.Path;
			if (targetReference != null)
			{
				TargetReferenceGitPointView.Value = targetReference;
			}
			FileDiffControl.Target = ((mode is ShowFileHistoryWindowCommand.Mode.Hunk) ? FileDiffControlTarget.HunkHistory : FileDiffControlTarget.History);
			base.SizeChanged += FileHistoryWindow_SizeChanged;
			base.Activated += FileHistoryWindow_Activated;
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current, "DiffContextSizeChanged", delegate
			{
				_delayedAction.ReinvokeNow();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffIgnoreWhitespacesChanged", delegate
			{
				_delayedAction.ReinvokeNow();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffShowEntireFileChanged", delegate
			{
				_delayedAction.ReinvokeNow();
			});
			base.CommandBindings.Add(RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateShortcutCommandBinding(delegate
			{
				HistoryEntryViewModel historyEntryViewModel4 = _selectedHistoryEntries?.FirstItem();
				if (historyEntryViewModel4 != null)
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, historyEntryViewModel4.Sha.ToString(), historyEntryViewModel4.ChangedFile);
				}
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.CopyRevisionSha.CreateShortcutCommandBinding(delegate
			{
				HistoryEntryViewModel[] selectedHistoryEntries4 = _selectedHistoryEntries;
				List<Revision> list3 = new List<Revision>((selectedHistoryEntries4 != null) ? selectedHistoryEntries4.Length : 0);
				HistoryEntryViewModel[] selectedHistoryEntries5 = _selectedHistoryEntries;
				foreach (HistoryEntryViewModel historyEntryViewModel3 in selectedHistoryEntries5)
				{
					list3.Add(historyEntryViewModel3.Revision.Revision);
				}
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(list3.ToArray());
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.CopyRevisionInfo.CreateShortcutCommandBinding(delegate
			{
				HistoryEntryViewModel[] selectedHistoryEntries2 = _selectedHistoryEntries;
				List<Revision> list2 = new List<Revision>((selectedHistoryEntries2 != null) ? selectedHistoryEntries2.Length : 0);
				HistoryEntryViewModel[] selectedHistoryEntries3 = _selectedHistoryEntries;
				foreach (HistoryEntryViewModel historyEntryViewModel2 in selectedHistoryEntries3)
				{
					list2.Add(historyEntryViewModel2.Revision.Revision);
				}
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(list2.ToArray());
			}));
			base.CommandBindings.Add(RepositoryUserControl.Commands.RunExternalDiffTool.CreateShortcutCommandBinding(delegate
			{
				HistoryEntryViewModel[] selectedHistoryEntries = _selectedHistoryEntries;
				int num = ((selectedHistoryEntries != null) ? selectedHistoryEntries.Length : 0);
				if (num > 0 && num <= 2)
				{
					HistoryEntryViewModel historyEntryViewModel = _selectedHistoryEntries.FirstItem();
					RunExternalDiffToolCommand.DiffTarget.Revision diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(changedFile: historyEntryViewModel.ChangedFile, otherSha: (num == 2) ? new Sha?(_selectedHistoryEntries[1].Sha) : null, sha: historyEntryViewModel.Sha);
					List<ExternalTool> list = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
					if (list.Count > 0)
					{
						ExternalTool diffTool = list.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list[0];
						RepositoryUserControl.Commands.RunExternalDiffTool.Execute(_repositoryUserControl, diffTarget, diffTool);
					}
				}
			}));
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetWindowLocationState(ForkPlusSettings.Default.HistoryWindowLocationState);
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.HistoryWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void FileHistoryWindow_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.HistoryWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void FileHistoryWindow_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		protected override async void OnInitialized(EventArgs e)
		{
			try
			{
				base.OnInitialized(e);
				RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
				if (repositoryData == null)
				{
					return;
				}
				ShowFileHistoryWindowCommand.Mode mode = _mode;
				ShowFileHistoryWindowCommand.Mode.Hunk hunk = mode as ShowFileHistoryWindowCommand.Mode.Hunk;
				if (hunk != null)
				{
					BusyIndicator.Show();
					GitCommandResult<(RevisionWithFiles[], string[])> gitCommandResult = await Task.Run(() => new GetFileHistoryGitCommand().Execute(GitModule, hunk.Path, hunk.LineRange, _targetReference?.Sha));
					BusyIndicator.Hide();
					if (!gitCommandResult.Succeeded)
					{
						ShowErrorFallback(gitCommandResult.Error);
						return;
					}
					_revisions = gitCommandResult.Result.Item1;
					_patches = gitCommandResult.Result.Item2;
					RevisionTimeLine.Revisions = gitCommandResult.Result.Item1;
					CodeEditorFallbackUserControl.Hide();
					ChangedFile changedFile = new ChangedFile(hunk.Path, StatusType.Modified);
					RevisionWithFiles[] item = gitCommandResult.Result.Item1;
					foreach (RevisionWithFiles revision in item)
					{
						_root.Children.Add(new HistoryEntryViewModel(revision, changedFile));
					}
				}
				else if (_mode is ShowFileHistoryWindowCommand.Mode.File || _mode is ShowFileHistoryWindowCommand.Mode.Directory)
				{
					BusyIndicator.Show();
					GitCommandResult<RevisionWithFiles[]> gitCommandResult2 = await Task.Run(() => new GetFileHistoryGitCommand().Execute(GitModule, repositoryData.Submodules.Items, _mode.Path, _targetReference?.Sha));
					BusyIndicator.Hide();
					if (!gitCommandResult2.Succeeded)
					{
						ShowErrorFallback(gitCommandResult2.Error);
						return;
					}
					_revisions = gitCommandResult2.Result;
					RevisionTimeLine.Revisions = gitCommandResult2.Result;
					CodeEditorFallbackUserControl.Hide();
					RevisionWithFiles[] item = gitCommandResult2.Result;
					foreach (RevisionWithFiles revisionWithFiles in item)
					{
						if (_mode is ShowFileHistoryWindowCommand.Mode.Directory)
						{
							FolderHistoryEntryViewModel folderHistoryEntryViewModel = new FolderHistoryEntryViewModel(revisionWithFiles, revisionWithFiles.ChangedFiles.FirstItem());
							_root.Children.Add(folderHistoryEntryViewModel);
							ChangedFile[] changedFiles = revisionWithFiles.ChangedFiles;
							foreach (ChangedFile changedFile2 in changedFiles)
							{
								folderHistoryEntryViewModel.Children.Add(new SubItemFileHistoryEntryViewModel(revisionWithFiles, changedFile2));
							}
						}
						else if (_mode is ShowFileHistoryWindowCommand.Mode.File)
						{
							_root.Children.Add(new HistoryEntryViewModel(revisionWithFiles, revisionWithFiles.ChangedFiles.FirstItem()));
						}
					}
					if (_mode is ShowFileHistoryWindowCommand.Mode.Directory)
					{
						_root.ExpandAllChildren();
					}
				}
				TreeView.Focus();
				HistoryEntryViewModel historyEntryViewModel = GetElementBySha(_revisionToSelect) ?? GetFirstItemToSelect();
				if (historyEntryViewModel != null)
				{
					TreeView.SelectedItem = historyEntryViewModel;
					TreeView.ScrollIntoView(historyEntryViewModel);
				}
			}
			catch (Exception ex)
			{
				Log.Error("OnInitialized failed", ex);
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (TreeView.LastClickedItem is HistoryEntryViewModel historyEntryViewModel)
			{
				RevealRevision(historyEntryViewModel.Sha);
			}
		}

		private void TreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			e.Handled = true;
			HistoryEntryViewModel[] array = e.RemovedItems.CompactMap((object x) => x as HistoryEntryViewModel);
			if (array != null)
			{
				HistoryEntryViewModel[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].SelectionType = ListBoxSelectionType.None;
				}
			}
			_selectedHistoryEntries = TreeView.SelectedItems.CompactMap((object x) => x as HistoryEntryViewModel);
			MultiselectionTreeViewItem[] selectedHistoryEntries = _selectedHistoryEntries;
			selectedHistoryEntries.RefreshSelectionType();
			if (_selectedHistoryEntries.Length == 2)
			{
				_selectedHistoryEntries = SortEntriesByIndex(_selectedHistoryEntries);
			}
			RefreshRevisionTimeLine(_selectedHistoryEntries);
			_delayedAction.InvokeWithDelay(_selectedHistoryEntries);
		}

		private void TreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			HistoryEntryViewModel[] array = ClickedItems(TreeView);
			if (array.Length == 2)
			{
				ChangedFile changedFile = array[0].ChangedFile;
				List<ExternalTool> list = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (list.Count > 0)
				{
					Sha sha = array[0].Sha;
					Sha sha2 = array[1].Sha;
					RunExternalDiffToolCommand.DiffTarget.Revision diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(sha, sha2, changedFile);
					bool isEnabled = !changedFile.IsDirectory;
					TreeView.ContextMenu.SetItems(CreateDiffToolContextMenuItems(_repositoryUserControl, diffTarget, list, isEnabled));
				}
			}
			else if (array.Length == 1)
			{
				HistoryEntryViewModel entry = array[0];
				List<Control> list2 = new List<Control>();
				MenuItem item = RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateMenuItem(delegate
				{
					RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(entry.Sha);
					string fileToSelect = ((!(entry is FolderHistoryEntryViewModel)) ? entry.Revision.ChangedFiles.FirstItem().Path : IReadOnlyListExtensions.FirstItem(entry.Revision.ChangedFiles, (ChangedFile x) => x.Path == _selectedHistoryEntries.FirstItem()?.Path)?.Path);
					RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(GitModule, target, fileToSelect);
				}, isEnabled: true, showShortcut: false);
				list2.Add(item);
				MenuItem menuItem = new MenuItem();
				menuItem.Header = Translate("Reveal in Fork");
				menuItem.Click += delegate
				{
					RevealRevision(entry.Sha, entry.Path);
				};
				list2.Add(menuItem);
				if (entry is FolderHistoryEntryViewModel)
				{
					list2.AddRange(CreateRevisionContextMenuItems(entry));
				}
				else if (entry is SubItemFileHistoryEntryViewModel)
				{
					list2.AddRange(CreateFileContextMenuItems(entry));
				}
				else
				{
					list2.AddRange(CreateFileContextMenuItems(entry));
					list2.AddRange(CreateRevisionContextMenuItems(entry));
				}
				TreeView.ContextMenu.SetItems(list2);
			}
			else
			{
				e.Handled = true;
			}
		}

		private void OpenRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: HistoryEntryViewModel dataContext })
			{
				RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(dataContext.Sha);
				string fileToSelect = ((!(dataContext is FolderHistoryEntryViewModel)) ? dataContext.Revision.ChangedFiles.FirstItem().Path : IReadOnlyListExtensions.FirstItem(dataContext.Revision.ChangedFiles, (ChangedFile x) => x.Path == _selectedHistoryEntries.FirstItem()?.Path)?.Path);
				RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(GitModule, target, fileToSelect);
			}
		}

		private HistoryEntryViewModel[] SortEntriesByIndex(HistoryEntryViewModel[] entries)
		{
			List<int> list = new List<int>(2);
			foreach (HistoryEntryViewModel entry in entries)
			{
				list.Add(_revisions.IndexOf((RevisionWithFiles x) => x.Sha == entry.Sha));
			}
			list.Sort();
			List<HistoryEntryViewModel> list2 = new List<HistoryEntryViewModel>(2);
			foreach (int index in list)
			{
				list2.Add(IReadOnlyListExtensions.FirstItem(entries, (HistoryEntryViewModel x) => x.Sha == _revisions[index].Sha));
			}
			return list2.ToArray();
		}

		private HistoryEntryViewModel GetFirstItemToSelect()
		{
			if (_root.Children.Count < 1)
			{
				return null;
			}
			HistoryEntryViewModel historyEntryViewModel = _root.Children[0] as HistoryEntryViewModel;
			if (historyEntryViewModel is FolderHistoryEntryViewModel)
			{
				return historyEntryViewModel.Children[0] as SubItemFileHistoryEntryViewModel;
			}
			return historyEntryViewModel;
		}

		[Null]
		private HistoryEntryViewModel GetElementBySha(Sha? sha)
		{
			if (!sha.HasValue)
			{
				return null;
			}
			for (int i = 0; i < _root.Children.Count; i++)
			{
				HistoryEntryViewModel historyEntryViewModel = _root.Children[i] as HistoryEntryViewModel;
				Sha sha2 = historyEntryViewModel.Sha;
				Sha? sha3 = sha;
				if (sha2 == sha3)
				{
					if (historyEntryViewModel is FolderHistoryEntryViewModel)
					{
						return historyEntryViewModel.Children.First() as SubItemFileHistoryEntryViewModel;
					}
					return historyEntryViewModel;
				}
			}
			return null;
		}

		private static HistoryEntryViewModel[] ClickedItems(MultiselectionTreeView treeView)
		{
			if (!(treeView.LastClickedItem is HistoryEntryViewModel historyEntryViewModel))
			{
				return new HistoryEntryViewModel[0];
			}
			if (treeView.SelectedItems.Contains(historyEntryViewModel))
			{
				return treeView.SelectedItems.CompactMap((object x) => x as HistoryEntryViewModel);
			}
			return new HistoryEntryViewModel[1] { historyEntryViewModel };
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private IEnumerable<Control> CreateFileContextMenuItems(HistoryEntryViewModel entry)
		{
			ChangedFile changedFile = entry.ChangedFile;
			bool isEnabled = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(GitModule, entry.Path);
			yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, entry.Sha.ToString(), changedFile);
			}, isEnabled);
			List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
			if (diffTools.Count > 0)
			{
				RunExternalDiffToolCommand.DiffTarget.Revision diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(entry.Sha, null, changedFile);
				bool diffIsEnabled = !changedFile.IsDirectory;
				CreateDiffToolContextMenuItems(_repositoryUserControl, diffTarget, diffTools, diffIsEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(GitModule, entry.Path);
			});
			yield return new Separator();
			MenuItem menuItem = RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("Reset File to State at Commit...", delegate
			{
				RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(_repositoryUserControl, new ChangedFile[1] { changedFile }, entry.Sha.ToString());
			});
			menuItem.IsEnabled = !changedFile.IsDirectory;
			yield return menuItem;
			yield return new Separator();
			yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.SaveFile.Execute(_repositoryUserControl, changedFile, entry.Sha.ToString());
			});
			CustomCommand[] fileCustomCommands = CustomCommandManager.Current.GetCustomCommands(_repositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(GitModule, changedFile.Path, entry.Sha);
			if (changedFile is SubmoduleChangedFile submoduleChangedFile)
			{
				fileCustomCommands = CustomCommandManager.Current.GetCustomCommands(_repositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(submoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(GitModule, parameters);
			}
			if (fileCustomCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> customMenuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in fileCustomCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(_repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, customMenuItems);
					}
				}
				foreach (MenuItem customMenuItem in customMenuItems)
				{
					yield return customMenuItem;
				}
			}
		}

		private IEnumerable<Control> CreateRevisionContextMenuItems(HistoryEntryViewModel entry)
		{
			CustomCommand[] revisionCustomCommands = CustomCommandManager.Current.GetCustomCommands(_repositoryUserControl.RepositoryData, CustomCommandTarget.Revision);
			if (revisionCustomCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> customMenuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in revisionCustomCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(_repositoryUserControl, new CustomCommandEnvironment(GitModule, entry.Sha), customCommand.Name.Split(Consts.Chars.Slash), 0, customMenuItems);
					}
				}
				foreach (MenuItem menuItem in customMenuItems)
				{
					yield return menuItem;
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(new Revision[1] { entry.Revision.Revision });
			});
			yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(new Revision[1] { entry.Revision.Revision });
			});
		}

		private IEnumerable<Control> CreateDiffToolContextMenuItems(RepositoryUserControl repositoryUserControl, RunExternalDiffToolCommand.DiffTarget diffTarget, IReadOnlyList<ExternalTool> diffTools, bool isEnabled)
		{
			if (diffTools.Count == 1)
			{
				ExternalTool diffTool = diffTools[0];
				yield return RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItemFormat("Diff in {0}", new object[1] { diffTool.Name }, delegate
				{
					RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool);
				}, isEnabled);
			}
			else if (diffTools.Count > 1)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = PreferencesLocalization.MenuHeader("External Diff"),
					IsEnabled = isEnabled
				};
				foreach (ExternalTool diffTool in diffTools)
				{
					ExternalTool capturedDiffTool = diffTool;
					MenuItem newItem = RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem(capturedDiffTool.Name ?? "", delegate
					{
						RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, capturedDiffTool);
					}, isEnabled: true, null, capturedDiffTool.IsPrimary);
					menuItem.Items.Add(newItem);
				}
				yield return menuItem;
			}
		}

		private void RefreshDiff(HistoryEntryViewModel[] historyEntries)
		{
			if (historyEntries.Length > 2)
			{
				FallbackUserControl fallbackUserControl = new FallbackUserControl();
				fallbackUserControl.FallbackTitle = Translate("Select two commits to see difference between them");
				fallbackUserControl.FallbackMessage = string.Format(Translate("{0} commits selected"), historyEntries.Length);
				FileDiffControl.ShowSubView(() => fallbackUserControl, delegate(FallbackUserControl c, FileControlHeaderUserControl h)
				{
					h.Collapse();
				});
				return;
			}
			HistoryEntryViewModel historyEntry = historyEntries[0];
			int tabWidth = GitModule.Settings.TabWidth;
			_showDiffJob?.Monitor.Cancel();
			_showDiffJob = null;
			if (_mode is ShowFileHistoryWindowCommand.Mode.Hunk)
			{
				int selectedIndex = TreeView.SelectedIndex;
				if (selectedIndex != -1)
				{
					if (selectedIndex >= _patches.Length)
					{
						Log.Error($"Cannot find patch for selected revision: {selectedIndex}");
						return;
					}
					string diffString = _patches[selectedIndex];
					FileDiffControl.RepositoryUserControl = _repositoryUserControl;
					FileDiffControl.Content = ParsePatch(diffString, historyEntry.ChangedFile, GitModule, tabWidth);
					FileDiffControl.Target = FileDiffControlTarget.HunkHistory;
				}
				return;
			}
			_showDiffJob = _repositoryUserControl.JobQueue.Add(Translate("Load diff for history"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					RevisionDiffTarget target = new RevisionDiffTarget.Revision(historyEntry.Sha);
					if (historyEntries.Length == 2)
					{
						target = new RevisionDiffTarget.Range(historyEntry.Sha, historyEntries[1].Sha);
					}
					GitCommandResult<DiffContent> diffPresentationResult = new GetRevisionFileChangesGitCommand().Execute(GitModule, target, historyEntry.ChangedFile, ForkPlusSettings.Default.DiffContextSize, tabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, ForkPlusSettings.Default.DiffShowEntireFile);
					if (!monitor.IsCanceled)
					{
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								FileDiffControl.RepositoryUserControl = _repositoryUserControl;
								FileDiffControl.Content = diffPresentationResult;
								FileDiffControl.Target = FileDiffControlTarget.History;
								_showDiffJob = null;
							}
						});
					}
				}
			}, JobFlags.Hidden);
		}

		private GitCommandResult<DiffContent> ParsePatch(string diffString, ChangedFile changedFile, GitModule gitModule, int tabWidth)
		{
			GitCommandResult<Patch> gitCommandResult = new PatchParser().Parse(diffString, "a/", "b/");
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<DiffContent>.Failure(gitCommandResult.Error);
			}
			Patch result = gitCommandResult.Result;
			return GitCommandResult<DiffContent>.Success(new ParsedDiffContent(gitModule, changedFile, result.Diffs.FirstItem(), tabWidth, entireFile: false));
		}

		private void RevealRevision(Sha sha, [Null] string filePath = null)
		{
			Application.Current.MainWindow.Activate();
			if (MainWindow.ActiveRepositoryUserControl?.GitModule != GitModule)
			{
				Application.Current.TabManager()?.OpenRepository(GitModule.Path);
			}
			MainWindow.ActiveRepositoryUserControl?.SelectRevision(sha, filePath);
		}

		private void ShowErrorFallback(GitCommandError error)
		{
			ShowErrorFallback(error.ToString());
		}

		private void ShowErrorFallback(string errorString)
		{
			CodeEditorFallbackUserControl.Show();
			CodeEditorFallbackUserControl.FallbackTitle = Translate("Error");
			CodeEditorFallbackUserControl.FallbackMessage = errorString;
		}

		private void RefreshRevisionTimeLine(HistoryEntryViewModel[] selectedHistoryEntries)
		{
			RevisionTimeLine.ActiveRevision = selectedHistoryEntries[0].Revision.Sha;
			RevisionTimeLine.ActiveRevision2 = ((selectedHistoryEntries.Length == 2) ? new Sha?(selectedHistoryEntries[1].Revision.Sha) : null);
		}

	}
}
