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
	public partial class PushMultipleTagsWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Tag[] _tags;

		[Null]
		private readonly Remote _remoteToSelect;

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

		public PushMultipleTagsWindow(RepositoryUserControl repositoryUserControl, Tag[] tags, Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_tags = tags;
			_remoteToSelect = remote;
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
