using System;
using System.ComponentModel;
using System.Linq;
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
	public partial class FetchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private readonly Remote _predefinedRemote;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (RemoteComboBox.SelectedItem != null)
				{
					return base.IsSubmitAllowed;
				}
				return false;
			}
		}

		protected override string GetCommandPreview()
		{
			bool fetchAllRemotes = FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
			bool allTags = ForkPlusSettings.Default.FetchAllTags;
			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string> { "git", "fetch" };
			if (fetchAllRemotes)
			{
				parts.Add("--all");
			}
			else
			{
				Remote remote = RemoteComboBox.SelectedItem as Remote;
				if (remote == null)
				{
					return null;
				}
				parts.Add(remote.Name);
			}
			if (allTags)
			{
				parts.Add("--tags");
			}
			return string.Join(" ", parts);
		}

		public FetchWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			_predefinedRemote = remote;
			InitializeComponent();
			base.DialogTitle = Translate("Fetch");
		base.DialogDescription = Translate("Fetch latest changes from remote repository");
		base.SubmitButtonTitle = Translate("Fetch");
			Remote[] array = MainWindow.ActiveRepositoryUserControl.RepositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			RemoteComboBox.ItemsSource = array;
			RemoteComboBox.SelectedItem = array.FirstOrDefault((Remote x) => x.Name == _predefinedRemote?.Name) ?? array.FirstOrDefault((Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? array.FirstOrDefault();
			FetchAllRemotesCheckBox.IsChecked = ForkPlusSettings.Default.Fetch_FetchAllRemotes;
		}

		protected override void OnSubmit()
		{
			Remote remote = (Remote)RemoteComboBox.SelectedItem;
			bool fetchAllRemotes = FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
			bool fetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			GitModule gitModule = _gitModule;
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			string name = fetchAllRemotes ? Translate("Fetch all") : string.Format(Translate("Fetch '{0}'"), remote.Name);
			ForkPlusSettings.Default.Fetch_FetchAllRemotes = fetchAllRemotes;
			ForkPlusSettings.Default.Save();
			_repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult fetchResult = new FetchGitCommand().Execute(gitModule, remote, fetchAllRemotes, monitor, noPrompt: false, fetchAllTags);
				base.Dispatcher.Async(delegate
				{
					if (!fetchResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, fetchResult.Error).ShowDialog();
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

		private void FetchAllRemotesCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			RemoteComboBox.IsEnabled = !FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
			RefreshCommandPreview();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
