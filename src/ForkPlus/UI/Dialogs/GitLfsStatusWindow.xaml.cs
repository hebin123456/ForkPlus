using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitLfsStatusWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly DelayedAction<bool> _refreshLfsFilesList;

		private string[] _lfsFiles = new string[0];

		private Dictionary<string, string> _lfsLocks = new Dictionary<string, string>();

		private LfsFileViewModel[] _selectedItems = new LfsFileViewModel[0];

		[Null]
		private Job _activeRefresLocksJob;

		public GitLfsStatusWindow(RepositoryUserControl repositoryUserControl)
		{
			GitLfsStatusWindow gitLfsStatusWindow = this;
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			_repositoryUserControl = repositoryUserControl;
			_refreshLfsFilesList = new DelayedAction<bool>(RefreshLfsFilesList);
			InitializeComponent();
			base.DialogTitle = Translate("LFS Files Status");
			base.DialogDescription = "";
			base.ShowSubmitButton = false;
			base.CancelButtonTitle = Translate("Close");
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.F && Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.LeftShift))
				{
					gitLfsStatusWindow.FilterTextBox.Focus();
					e.Handled = true;
				}
			};
			FilterTextBox.FilterRequestChanged += FilterTextBox_FilterRequestChanged;
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading LFS files..."));
			repositoryUserControl.JobQueue.Add(Translate("LFS files"), delegate(JobMonitor monitor)
			{
				GitCommandResult<string[]> lfsFilesResponse = new GetLfsFilesGitCommand().Execute(gitModule, monitor);
				gitLfsStatusWindow.Dispatcher.Async(delegate
				{
					if (!lfsFilesResponse.Succeeded && !monitor.IsCanceled)
					{
						gitLfsStatusWindow.FallbackUserControl.Collapse();
						new ErrorWindow(repositoryUserControl, lfsFilesResponse.Error).ShowDialog();
						gitLfsStatusWindow.SetStatus(ForkPlusDialogStatus.None, "");
					}
					else
					{
						gitLfsStatusWindow._lfsFiles = lfsFilesResponse.Result;
						gitLfsStatusWindow.SetStatus(ForkPlusDialogStatus.None, "");
						gitLfsStatusWindow.Refresh();
					}
				});
			});
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape && !string.IsNullOrEmpty(FilterTextBox.Text))
			{
				FilterTextBox.Clear();
				e.Handled = true;
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		private void FilterTextBox_FilterRequestChanged(object sender, EventArgs e)
		{
			_refreshLfsFilesList.InvokeWithDelay(parameter: true);
		}

		private void LfsFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			e.Handled = true;
			_selectedItems = LfsFilesListBox.SelectedItems.CompactMap((object x) => x as LfsFileViewModel);
			IRoundedSelectionListBoxViewModel[] selectedItems = _selectedItems;
			selectedItems.RefreshSelectionType();
		}

		private void LockButton_Click(object sender, RoutedEventArgs e)
		{
			Lock(_selectedItems.Map((LfsFileViewModel x) => x.Path));
		}

		private void UnlockButton_Click(object sender, RoutedEventArgs e)
		{
			Unlock(_selectedItems.Map((LfsFileViewModel x) => x.Path));
		}

		private void Refresh()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			_activeRefresLocksJob?.Monitor.Cancel();
			_activeRefresLocksJob = null;
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Refreshing LFS locks..."));
			_activeRefresLocksJob = _repositoryUserControl.JobQueue.Add(Translate("LFS Locks"), delegate(JobMonitor monitor)
			{
				GitCommandResult<Dictionary<string, string>> locksResponse = new GetLfsLocksGitCommand().Execute(gitModule, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!monitor.IsCanceled)
					{
						_activeRefresLocksJob = null;
						if (!locksResponse.Succeeded)
						{
							FallbackUserControl.Collapse();
							new ErrorWindow(repositoryUserControl, locksResponse.Error).ShowDialog();
							SetStatus(ForkPlusDialogStatus.None, "");
						}
						else
						{
							FallbackUserControl.Collapse();
							_lfsLocks = locksResponse.Result;
							_refreshLfsFilesList.InvokeNow(parameter: true);
							SetStatus(ForkPlusDialogStatus.None, "");
						}
					}
				});
			}, JobFlags.Hidden);
		}

		private void RefreshLfsFilesList(bool dummy = false)
		{
			string selectedPath = _selectedItems.FirstItem()?.Path;
			LfsFileViewModel[] array = CreateViewModels(_lfsFiles, _lfsLocks);
			LfsFileViewModel[] array2 = array;
			string filterString = FilterTextBox.FilterRequest;
			if (!string.IsNullOrEmpty(filterString))
			{
				array2 = array.Filter(delegate(LfsFileViewModel x)
				{
					if (x.Path.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1)
					{
						return true;
					}
					string owner = x.Owner;
					return (owner != null && owner.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1) ? true : false;
				}).ToArray();
			}
			LfsFilesListBox.ItemsSource = array2;
			if (selectedPath != null)
			{
				LfsFilesListBox.SelectedItem = IReadOnlyListExtensions.FirstItem(array2, (LfsFileViewModel x) => x.Path == selectedPath);
			}
		}

		private static LfsFileViewModel[] CreateViewModels(string[] lfsFiles, Dictionary<string, string> lfsLocks)
		{
			List<LfsFileViewModel> list = new List<LfsFileViewModel>(lfsFiles.Length);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(lfsLocks);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(lfsLocks);
			list.AddRange(lfsFiles.Map((string path) => new LfsFileViewModel(path)));
			for (int i = 0; i < lfsFiles.Length; i++)
			{
				if (dictionary.TryGetValue(list[i].Path, out var value))
				{
					list[i].Owner = value;
					dictionary2.Remove(list[i].Path);
				}
				else
				{
					list[i].Owner = null;
				}
			}
			foreach (KeyValuePair<string, string> item in dictionary2)
			{
				list.Add(new LfsFileViewModel(item.Key, item.Value));
			}
			list.Sort((LfsFileViewModel x, LfsFileViewModel y) => NaturalStringComparer.Instance.Compare(x.Path, y.Path));
			int num = 0;
			foreach (LfsFileViewModel item2 in list)
			{
				item2.Row = num;
				num++;
			}
			return list.ToArray();
		}

		private void Unlock(string[] filepaths)
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null || filepaths.Length == 0)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			string message = ((filepaths.Length == 1) ? string.Format(Translate("Unlocking '{0}'"), filepaths[0]) : string.Format(Translate("Unlocking {0} files"), filepaths.Length));
			SetStatus(ForkPlusDialogStatus.InProgress, message);
			_repositoryUserControl.JobQueue.Add(Translate("LFS Unlock"), delegate(JobMonitor monitor)
			{
				GitCommandResult unlockResult = new GitLfsUnlockGitCommand().Execute(gitModule, filepaths, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!unlockResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, unlockResult.Error).ShowDialog();
						SetStatus(ForkPlusDialogStatus.None, "");
					}
					Refresh();
				});
			});
		}

		private void Lock(string[] filepaths)
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null || filepaths.Length == 0)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			string message = ((filepaths.Length == 1) ? string.Format(Translate("Locking '{0}'"), filepaths[0]) : string.Format(Translate("Locking {0} files"), filepaths.Length));
			SetStatus(ForkPlusDialogStatus.InProgress, message);
			_repositoryUserControl.JobQueue.Add(Translate("LFS Lock"), delegate(JobMonitor monitor)
			{
				GitCommandResult lockResult = new GitLfsLockGitCommand().Execute(gitModule, filepaths, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!lockResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, lockResult.Error).ShowDialog();
						SetStatus(ForkPlusDialogStatus.None, "");
					}
					Refresh();
				});
			});
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
