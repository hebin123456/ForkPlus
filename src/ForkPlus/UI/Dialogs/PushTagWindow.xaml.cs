using Avalonia.Controls.Selection;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;

namespace ForkPlus.UI.Dialogs
{
	public partial class PushTagWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		[Null]
		private readonly Remote _remoteToSelect;

		private readonly Tag _tag;

		// 阶段 3：承接 remote 选择校验与命令预览。OnSubmit 的 JobQueue/Dispatcher 耦合暂留 View。
		private readonly PushTagWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				// VM 只判 remote 已选；基类自己的提交前置条件（base.IsSubmitAllowed）由 View 合并。
				_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
				return _viewModel.IsRemoteSelected && base.IsSubmitAllowed;
			}
		}

		protected override string GetCommandPreview()
		{
			_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
			return _viewModel.CommandPreview;
		}

		public PushTagWindow(RepositoryUserControl repositoryUserControl, Tag tag, [Null] Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_remoteToSelect = remote;
			_tag = tag;
			_viewModel = new PushTagWindowViewModel(tag);
			InitializeComponent();
			base.DialogTitle = Translate("Push Tag");
			base.DialogDescription = Translate("Push tag to remote repository");
			base.SubmitButtonTitle = Translate("Push");
			Refresh();
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			object selectedItem = RemotesComboBox.SelectedItem;
			Remote remote = selectedItem as Remote;
			if (remote == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			GitModule gitModule = _repositoryUserControl.GitModule;
			Tag tag = _tag;
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Push '{0}' to '{1}'"), tag.Name, remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult pushResult = new PushTagGitCommand().Execute(gitModule, remote.Name, tag.FullReference, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.References);
				});
			});
			Close();
		}

		private void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void Refresh()
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			TagGitPointView.Value = _tag;
			Remote[] array = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
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
			RemotesComboBox.ItemsSource = array;
			Remote selectedItem = _remoteToSelect ?? remote ?? IReadOnlyListExtensions.FirstItem(array, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? array.FirstItem();
			RemotesComboBox.SelectedItem = selectedItem;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
