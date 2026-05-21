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

		public FetchWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			_predefinedRemote = remote;
			InitializeComponent();
			base.DialogTitle = "Fetch";
			base.DialogDescription = "Fetch latest changes from remote repository";
			base.SubmitButtonTitle = "Fetch";
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
		}

		private void FetchAllRemotesCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			RemoteComboBox.IsEnabled = !FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
