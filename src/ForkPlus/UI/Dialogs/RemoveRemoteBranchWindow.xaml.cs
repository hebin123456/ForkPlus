using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveRemoteBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly RepositoryReferences _references;

		private readonly RemoteBranch[] _remoteBranches;

		private readonly LocalBranch[] _localBranches;

		public RemoveRemoteBranchWindow(RepositoryUserControl repositoryUserControl, RemoteBranch[] remoteBranches, RepositoryReferences repositoryReferences)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_references = repositoryReferences;
			_remoteBranches = remoteBranches;
			_localBranches = repositoryReferences.LocalBranches;
			if (remoteBranches.Length == 1)
			{
				GitPointsContainer.Collapse();
				GitPointView.Show();
				GitPointView.Value = remoteBranches.FirstItem();
				base.DialogTitle = PreferencesLocalization.Current("Delete Branch");
				base.DialogDescription = PreferencesLocalization.Current("Delete branch from remote repository");
				StartPointTextBlock.Text = PreferencesLocalization.Current("Branch:");
				base.SubmitButtonTitle = PreferencesLocalization.Current("Delete");
			}
			else
			{
				GitPointView.Collapse();
				GitPointsContainer.Show();
				GitPoints.ItemsSource = _remoteBranches;
				base.DialogTitle = PreferencesLocalization.Current("Delete Branches");
				base.DialogDescription = PreferencesLocalization.Current("Delete branches from remote repository");
				StartPointTextBlock.Text = PreferencesLocalization.Current("Branches:");
				base.SubmitButtonTitle = PreferencesLocalization.FormatCurrent("Delete {0} branches", remoteBranches.Length);
			}
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 _remoteBranches 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			if (_remoteBranches == null || _remoteBranches.Length == 0)
			{
				return null;
			}
			// 与 RemoveRemoteBranchGitCommand 实际执行的 git push <remote> --delete refs/heads/<branch> 一致。
			var lines = new System.Collections.Generic.List<string>(_remoteBranches.Length);
			foreach (RemoteBranch b in _remoteBranches)
			{
				lines.Add("git push " + b.Remote + " --delete refs/heads/" + b.ShortName);
			}
			return string.Join("\n", lines);
		}

		protected override void OnSubmit()
		{
			DisableEditableControls();
			GitModule gitModule = _repositoryUserControl.GitModule;
			string name = ((_remoteBranches.Length > 1) ? $"Delete {_remoteBranches.Length} branches" : ("Delete '" + _remoteBranches[0].Name + "'"));
			_repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				for (int i = 0; i < _remoteBranches.Length; i++)
				{
					RemoteBranch remoteBranch = _remoteBranches[i];
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Deleting '" + remoteBranch.Name + "'...");
					});
					GitCommandResult removeRemoteBranchResult = new RemoveRemoteBranchGitCommand().Execute(gitModule, remoteBranch, monitor);
					if (!removeRemoteBranchResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(removeRemoteBranchResult);
						});
						return;
					}
					LocalBranch localBranch = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.UpstreamFullReference == remoteBranch.FullReference);
					if (localBranch != null)
					{
						GitCommandResult removeTrackingReferenceResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, localBranch, null, monitor);
						if (!removeTrackingReferenceResult.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								Close(removeTrackingReferenceResult);
							});
							return;
						}
					}
				}
				gitModule.Settings.PinnedReferences = _references.PinnedReferences.Filter((string p) => !_remoteBranches.ContainsItem((RemoteBranch b) => b.FullReference == p)).ToArray();
				gitModule.Settings.FilterReferences = _references.FilterReferences.Filter((string p) => !_remoteBranches.ContainsItem((RemoteBranch b) => b.FullReference == p)).ToArray();
				gitModule.Settings.Save();
				base.Dispatcher.Async(delegate
				{
					Close(GitCommandResult.Success());
				});
			}, JobFlags.SaveToLog);
		}

	}
}
