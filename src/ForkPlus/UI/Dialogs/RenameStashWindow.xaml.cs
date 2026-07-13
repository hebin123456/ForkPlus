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
	public partial class RenameStashWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly StashRevision _stash;

		public Sha? OutResultSha { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string text = StashNameTextBox.Text;
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text != _stash.Message;
				}
				return false;
			}
		}

		public RenameStashWindow(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			_repositoryUserControl = repositoryUserControl;
			_stash = stash;
			InitializeComponent();
			base.DialogTitle = Translate("Rename Stash");
			base.DialogDescription = Translate("Update stash message");
			base.SubmitButtonTitle = Translate("Rename");
			StashNameTextBox.Text = stash.Message;
		StashNameTextBox.SelectAll();
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		if (_stash == null || string.IsNullOrEmpty(_stash.ReflogName))
		{
			return null;
		}
		string newMessage = StashNameTextBox.Text;
		if (string.IsNullOrWhiteSpace(newMessage))
		{
			return null;
		}
		string quotedMessage = newMessage.IndexOf(' ') >= 0 ? ("\"" + newMessage + "\"") : newMessage;
		return "git stash rename " + _stash.ReflogName + " " + quotedMessage;
	}

	protected override void OnSubmit()
	{
		GitModule gitModule = _repositoryUserControl.GitModule;
		StashRevision stash = _stash;
		string newMessage = StashNameTextBox.Text;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating message..."));
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Rename stash '{0}'"), _stash.Message), delegate(JobMonitor monitor)
			{
				GitCommandResult<Sha> renameResult = new RenameStashGitCommand().Execute(gitModule, stash.ReflogName, newMessage, monitor);
				OutResultSha = renameResult.Result;
				base.Dispatcher.Async(delegate
				{
					Close(renameResult.ToGitCommandResult());
				});
			}, JobFlags.ShowOnToolbar);
		}

		private void StashName_TextChanged(object sender, TextChangedEventArgs e)
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
