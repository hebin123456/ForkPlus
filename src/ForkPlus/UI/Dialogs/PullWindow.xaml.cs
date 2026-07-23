using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class PullWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly RemoteBranch _predefinedRemoteBranch;

		private LocalBranch _activeLocalBranch;

		private IReadOnlyList<RemoteBranch> _allRemoteBranches;

		private RemoteBranch _upstreamOfActiveBranch;

		private bool _referencesLoaded;

		// 阶段 3：承接远端/远端分支选择 + rebase/tags 选项 + 命令预览。
		// LoadReferencesAndRefresh 异步流程留 View；VM 仅持选中状态 + AllTags 设置读取。
		private readonly PullWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				PushSelectionToViewModel();
				return _viewModel.IsSubmitAllowed && base.IsSubmitAllowed;
			}
		}

		private void PushSelectionToViewModel()
		{
			_viewModel.ActiveLocalBranch = _activeLocalBranch;
			_viewModel.ReferencesLoaded = _referencesLoaded;
			_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
			_viewModel.SelectedRemoteBranch = RemoteBranchesComboBox.SelectedItem as RemoteBranch;
			_viewModel.Rebase = RebaseCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.AllTags = ForkPlusSettings.Default.FetchAllTags;
		}

		protected override string GetCommandPreview()
		{
			PushSelectionToViewModel();
			return _viewModel.CommandPreview;
		}

		public PullWindow(RepositoryUserControl repositoryUserControl, RemoteBranch remoteBranch)
		{
			RepositoryData repositoryData = MainWindow.ActiveRepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				_repositoryUserControl = repositoryUserControl;
				_predefinedRemoteBranch = remoteBranch;
				_viewModel = new PullWindowViewModel();
				InitializeComponent();
				base.DialogTitle = Translate("Pull");
				base.DialogDescription = Translate("Pull remote branches and merge them into your local branch");
				base.SubmitButtonTitle = Translate("Pull");
				RebaseCheckBox.IsChecked = ForkPlusSettings.Default.Pull_Rebase;
				StashAndReapplyCheckBox.IsChecked = ForkPlusSettings.Default.Pull_StashAndReapply;
				LoadReferencesAndRefresh(repositoryData.Remotes.Items, repositoryData.References.RemoteBranches, repositoryData.References.ActiveBranch);
			}
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			RepositoryStatus repositoryStatus = _repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			Remote remote = RemotesComboBox.SelectedItem as Remote;
			RemoteBranch remoteBranch2 = RemoteBranchesComboBox.SelectedItem as RemoteBranch;
			bool rebase = RebaseCheckBox.IsChecked.GetValueOrDefault();
			bool stashAndReapply = StashAndReapplyCheckBox.IsChecked.GetValueOrDefault();
			bool allTags = ForkPlusSettings.Default.FetchAllTags;
			bool flag = repositoryData.References.ActiveBranch?.UpstreamFullReference == remoteBranch2.FullReference;
			RemoteBranch remoteBranch = (flag ? null : remoteBranch2);
			bool workingDirectoryIsDirty = repositoryStatus.WorkingDirectoryIsDirty();
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			ForkPlusSettings.Default.Pull_Rebase = rebase;
			ForkPlusSettings.Default.Pull_StashAndReapply = stashAndReapply;
			ForkPlusSettings.Default.Save();
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Pull '{0}'"), remoteBranch2.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult requestResult = PerformPull(gitModule, remote.Name, remoteBranch, rebase, allTags, stashAndReapply, workingDirectoryIsDirty, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!requestResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, requestResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Head());
				});
			});
			Close();
		}

		private void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateRemoteBranchesCombobox();
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void RemoteBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void CheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private static GitCommandResult PerformPull(GitModule gitModule, string remote, [Null] RemoteBranch remoteBranch, bool rebase, bool allTags, bool stashAndReapply, bool workingDirectoryIsDirty, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			if (stashAndReapply && workingDirectoryIsDirty)
			{
				monitor.Update(10.0, Translate("Stashing..."));
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Pull autostash {DateTime.Now}", stageNewFiles: false, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult.Error);
				}
			}
			GitCommandResult gitCommandResult2 = new PullGitCommand().Execute(gitModule, remote, remoteBranch, rebase, allTags, monitor);
			if (!gitCommandResult2.Succeeded && !monitor.IsCanceled)
			{
				if (submodulesToUpdate.Length > 0)
				{
					monitor.Update(0.0, Translate("Updating submodules..."));
					new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				}
				monitor.Fail(Translate("Pull failed"));
				return gitCommandResult2;
			}
			GitCommandResult gitCommandResult3 = GitCommandResult.Success();
			if (stashAndReapply && workingDirectoryIsDirty)
			{
				monitor.Update(10.0, Translate("Applying stash..."));
				gitCommandResult3 = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", deleteAfterApply: true, monitor);
			}
			GitCommandResult gitCommandResult4 = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0)
			{
				monitor.Update(0.0, Translate("Updating submodules..."));
				gitCommandResult4 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			if (!gitCommandResult3.Succeeded)
			{
				monitor.Fail(Translate("Apply stash failed"));
				return gitCommandResult3;
			}
			if (!gitCommandResult4.Succeeded)
			{
				monitor.Fail(Translate("Update submodules failed"));
				return gitCommandResult4;
			}
			monitor.Success(Translate("Everything is up to date"));
			return gitCommandResult2;
		}

		private void LoadReferencesAndRefresh(Remote[] initialRemotes, RemoteBranch[] initialRemoteBranches, LocalBranch initialActiveBranch)
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			Refresh(initialRemotes, initialRemoteBranches, initialActiveBranch);
			new Task(delegate
			{
				GitCommandResult<RepositoryRemotes> remotesResponse = new GetRemotesGitCommand().Execute(gitModule);
				if (remotesResponse.Succeeded)
				{
					GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
					if (gitCommandResult.Succeeded)
					{
						GitCommandResult<ReferenceStorage> gitCommandResult2 = new GetReferencesGitCommand().Execute(gitModule, gitCommandResult.Result);
						if (gitCommandResult2.Succeeded)
						{
							ReferenceStorage referenceStorage = gitCommandResult2.Result;
							LocalBranch activeBranch = referenceStorage.CreateLocalBranches().FirstItem((LocalBranch x) => x.IsActive);
							base.Dispatcher.Async(delegate
							{
								if (initialActiveBranch?.FullReference != activeBranch?.FullReference)
								{
									IReadOnlyList<RemoteBranch> remoteBranches = referenceStorage.CreateRemoteBranches();
									Refresh(remotesResponse.Result.Items, remoteBranches, activeBranch);
								}
								_referencesLoaded = true;
								UpdateSubmitButton();
							});
						}
					}
				}
			}).Start();
		}

		private void Refresh(Remote[] remotes, IReadOnlyList<RemoteBranch> remoteBranches, LocalBranch activeBranch)
		{
			if (activeBranch != null)
			{
				_activeLocalBranch = activeBranch;
				_allRemoteBranches = remoteBranches;
				_upstreamOfActiveBranch = remoteBranches.FirstItem((RemoteBranch x) => x.FullReference == activeBranch.UpstreamFullReference);
				Remote[] array = remotes.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
				RemotesComboBox.ItemsSource = array;
				RemotesComboBox.SelectedItem = GetDefaultSelectedRemote(array);
				DestinationGitPointView.Value = activeBranch;
				UpdateRemoteBranchesCombobox();
			}
		}

		private Remote GetDefaultSelectedRemote(Remote[] remotes)
		{
			return IReadOnlyListExtensions.FirstItem(remotes, (Remote x) => x.Name == _predefinedRemoteBranch?.Remote) ?? IReadOnlyListExtensions.FirstItem(remotes, (Remote x) => x.Name == _upstreamOfActiveBranch?.Remote) ?? IReadOnlyListExtensions.FirstItem(remotes, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? remotes.FirstItem();
		}

		private void UpdateRemoteBranchesCombobox()
		{
			Remote selectedRemote = RemotesComboBox.SelectedItem as Remote;
			List<RemoteBranch> list = _allRemoteBranches.Filter((RemoteBranch x) => x.Remote == selectedRemote?.Name);
			RemoteBranchesComboBox.ItemsSource = list;
			RemoteBranchesComboBox.SelectedItem = GetDefaultSelectedRemoteBranch(list);
		}

		private RemoteBranch GetDefaultSelectedRemoteBranch(IReadOnlyList<RemoteBranch> remoteBranches)
		{
			return remoteBranches.FirstItem((RemoteBranch x) => x.FullReference == _predefinedRemoteBranch?.FullReference) ?? remoteBranches.FirstItem((RemoteBranch x) => x.ShortName == _predefinedRemoteBranch?.ShortName) ?? remoteBranches.FirstItem((RemoteBranch x) => x.FullReference == _upstreamOfActiveBranch?.FullReference) ?? remoteBranches.FirstItem((RemoteBranch x) => x.ShortName == _activeLocalBranch?.Name);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
