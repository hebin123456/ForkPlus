using System;
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
	public partial class GitLfsFetchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (RemotesComboBox.SelectedItem is Remote)
				{
					return base.IsSubmitAllowed;
				}
				return false;
			}
		}

		public GitLfsFetchWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			InitializeComponent();
			base.DialogTitle = Translate("Fetch");
			base.DialogDescription = Translate("Download Git LFS objects from the specified remotes");
			base.SubmitButtonTitle = Translate("Fetch");
			Refresh();
		}

		private void Refresh()
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			Remote[] array = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			RemotesComboBox.ItemsSource = array;
			Remote remote = null;
			string upstreamFullReference = repositoryData.References.ActiveBranch?.UpstreamFullReference;
			if (upstreamFullReference != null)
			{
				RemoteBranch activeUpstream = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
				if (activeUpstream != null)
				{
					remote = IReadOnlyListExtensions.FirstItem(array, (Remote x) => x.Name == activeUpstream.Remote);
				}
			}
			Remote selectedItem = remote ?? IReadOnlyListExtensions.FirstItem(array, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? array.FirstItem();
			RemotesComboBox.SelectedItem = selectedItem;
		}

		protected override void OnSubmit()
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			Remote remote = (Remote)RemotesComboBox.SelectedItem;
			repositoryUserControl.JobQueue.Add(string.Format(Translate("LFS Fetch {0}"), remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult fetchResult = new GitLfsFetchGitCommand().Execute(_gitModule, remote, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!fetchResult.Succeeded && !(fetchResult.Error is GitCommandError.Cancelled))
					{
						new ErrorWindow(repositoryUserControl, fetchResult.Error).ShowDialog();
					}
				});
			});
			Close();
		}

		private void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
