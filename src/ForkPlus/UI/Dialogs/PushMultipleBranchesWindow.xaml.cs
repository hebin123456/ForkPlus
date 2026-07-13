using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class PushMultipleBranchesWindow : ForkPlusDialogWindow
	{
		public class PushBranchItem
		{
			public string BranchName { get; }

			public string UpstreamName { get; }

			public Remote Remote { get; }

			public PushBranchItem(LocalBranch localBranch, [Null] RemoteBranch remoteBranch, Remote remote)
			{
				BranchName = localBranch.Name;
				UpstreamName = ((remoteBranch != null) ? remoteBranch.Name : string.Format(PushMultipleBranchesWindow.Translate("{0} (new)"), remote.Name + "/" + localBranch.Name));
				Remote = remote;
			}
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch[] _localBranches;

		private readonly Remote _remote;

		protected override string GetCommandPreview()
		{
			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string> { "git", "push", _remote.Name };
			foreach (LocalBranch localBranch in _localBranches)
			{
				parts.Add(localBranch.Name);
			}
			return string.Join(" ", parts);
		}

		public PushMultipleBranchesWindow(RepositoryUserControl repositoryUserControl, LocalBranch[] localBranches, Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_localBranches = localBranches;
			_remote = remote;
			InitializeComponent();
			base.DialogTitle = Translate("Push");
			base.DialogDescription = string.Format(Translate("Push {0} branches to remote repository"), _localBranches.Length);
			base.SubmitButtonTitle = string.Format(Translate("Push {0} branches"), _localBranches.Length);
			Refresh();
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			Remote remote = _remote;
			LocalBranch[] localBranches = _localBranches;
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Push {0} branches to '{1}'"), localBranches.Length, remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult pushResult = new PushMultipleBranchesGitCommand().Execute(gitModule, remote.Name, localBranches, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Head());
				});
			});
			Close();
		}

		private void Refresh()
		{
			RemoteBranch[] array = _repositoryUserControl.RepositoryData?.References.RemoteBranches;
			if (array != null)
			{
				List<RemoteBranch> remoteBranches = array.Filter((RemoteBranch x) => x.Remote == _remote.Name);
				List<PushBranchItem> list = new List<PushBranchItem>(4);
				LocalBranch[] localBranches = _localBranches;
				foreach (LocalBranch localBranch in localBranches)
				{
					RemoteBranch remoteBranch = FindUpstream(localBranch, remoteBranches);
					list.Add(new PushBranchItem(localBranch, remoteBranch, _remote));
				}
				BranchesItemsControl.ItemsSource = list.ToArray();
			}
		}

		[Null]
		private static RemoteBranch FindUpstream(LocalBranch localBranch, IReadOnlyList<RemoteBranch> remoteBranches)
		{
			string upstreamFullReference = localBranch?.UpstreamFullReference;
			if (upstreamFullReference == null)
			{
				return null;
			}
			return remoteBranches.FirstItem((RemoteBranch x) => x.FullReference == upstreamFullReference);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
