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
	public partial class GitLfsPullWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		// 阶段 3：承接 remote 选择校验与命令预览（与 GitLfsFetch 同构）。
		private readonly GitLfsPullWindowViewModel _viewModel = new GitLfsPullWindowViewModel();

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
				return _viewModel.IsRemoteSelected && base.IsSubmitAllowed;
			}
		}

		public GitLfsPullWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Pull");
			base.DialogDescription = Translate("Download Git LFS objects for the currently checked out ref, and update the working directory with the downloaded content if required");
			base.SubmitButtonTitle = Translate("Pull");
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			Refresh();
		}

		private void Refresh()
		{
			RepositoryData repositoryData = MainWindow.ActiveRepositoryUserControl?.RepositoryData;
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
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
			return _viewModel.CommandPreview;
		}

		protected override void OnSubmit()
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			Remote remote = (Remote)RemotesComboBox.SelectedItem;
			repositoryUserControl.JobQueue.Add(string.Format(Translate("LFS Pull {0}"), remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult pullResult = new GitLfsPullGitCommand().Execute(_gitModule, remote, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!pullResult.Succeeded && !(pullResult.Error is GitCommandError.Cancelled))
					{
						new ErrorWindow(repositoryUserControl, pullResult.Error).ShowDialog();
					}
				});
			});
			Close();
		}

		private void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
