using Avalonia.Controls.Selection;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class PushMultipleTagsWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Tag[] _tags;

		[Null]
		private readonly Remote _remoteToSelect;

		// 阶段 3：承接 remote 选择校验与命令预览（与 PushTagWindow 同构）。
		private readonly PushMultipleTagsWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				// VM 只判 remote 已选；基类提交前置条件由 View 合并。
				_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
				return _viewModel.IsRemoteSelected && base.IsSubmitAllowed;
			}
		}

		protected override string GetCommandPreview()
		{
			_viewModel.SelectedRemote = RemotesComboBox.SelectedItem as Remote;
			return _viewModel.CommandPreview;
		}

		public PushMultipleTagsWindow(RepositoryUserControl repositoryUserControl, Tag[] tags, Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_tags = tags;
			_remoteToSelect = remote;
			_viewModel = new PushMultipleTagsWindowViewModel(tags);
			InitializeComponent();
			base.DialogTitle = Translate("Push");
			base.DialogDescription = string.Format(Translate("Push {0} tags to remote repository"), _tags.Length);
			base.SubmitButtonTitle = string.Format(Translate("Push {0} tags"), _tags.Length);
			Refresh();
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			object selectedItem = RemotesComboBox.SelectedItem;
			Remote remote = selectedItem as Remote;
			if (remote == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			Tag[] tags = _tags;
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Push {0} tags to '{1}'"), tags.Length, remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult pushResult = new PushMultipleTagsGitCommand().Execute(gitModule, remote.Name, tags, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
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
			TagsItemsControl.ItemsSource = _tags.Map((Tag x) => x.Name);
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
