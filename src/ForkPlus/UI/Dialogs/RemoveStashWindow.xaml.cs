using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveStashWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly StashRevision[] _stashes;

		public RemoveStashWindow(RepositoryUserControl repositoryUserControl, StashRevision[] stashes)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_stashes = stashes;
			if (_stashes.Length == 1)
			{
				GitPointsContainer.Collapse();
				GitPointView.Show();
				GitPointView.Value = _stashes.FirstItem();
				base.DialogTitle = Translate("Delete Stash");
				base.DialogDescription = Translate("Delete stash from your repository");
				StartPointTextBlock.Text = Translate("Stash:");
				base.SubmitButtonTitle = Translate("Delete");
			}
			else
			{
				GitPointView.Collapse();
				GitPointsContainer.Show();
				GitPoints.ItemsSource = _stashes;
				base.DialogTitle = Translate("Delete Stashes");
				base.DialogDescription = Translate("Delete stashes from your repository");
				StartPointTextBlock.Text = Translate("Stashes:");
				base.SubmitButtonTitle = string.Format(Translate("Delete {0} stashes"), _stashes.Length);
			}
		}

		protected override void OnSubmit()
		{
			DisableEditableControls();
			GitModule gitModule = _repositoryUserControl.GitModule;
			StashRevision[] stashes = _stashes;
			string name = ((_stashes.Length > 1) ? string.Format(Translate("Delete {0} stashes"), _stashes.Length) : string.Format(Translate("Delete '{0}'"), _stashes[0].ReflogName));
			_repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult result = new RemoveStashGitCommand().Execute(gitModule, stashes, monitor);
				if (!result.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(result);
					});
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						Close(GitCommandResult.Success());
					});
				}
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
