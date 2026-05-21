using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitLfsTrackWindow : ForkPlusDialogWindow
	{
		private readonly DelayedAction<string> _updatePreviewAction;

		private readonly GitModule _gitModule;

		private readonly string _initialPattern;

		protected override bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(PatternTextBox.Text);

		public GitLfsTrackWindow(GitModule gitModule, string initialPattern)
		{
			InitializeComponent();
			_updatePreviewAction = new DelayedAction<string>(UpdatePreview, 0.3);
			_gitModule = gitModule;
			_initialPattern = initialPattern;
			base.DialogTitle = Translate("Add tracking patterns to Git LFS");
			base.DialogDescription = Translate("Add file path patterns to .gitattributes");
			base.SubmitButtonTitle = Translate("Track");
			PatternLabelTextBlock.Text = Translate("(one pattern per line)");
			PreviewLabelTextBlock.Text = Translate("0 files match");
			PatternTextBox.Text = _initialPattern;
			_updatePreviewAction.InvokeNow(_initialPattern);
		}

		protected override void OnSubmit()
		{
			string[] patterns = PatternTextBox.Text.Trim().Split(new string[1] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			GitCommandResult gitResult = new AddGitLfsTrackPatternGitCommand().Execute(_gitModule, patterns);
			Close(gitResult);
		}

		private void PatternTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updatePreviewAction.InvokeWithDelay(PatternTextBox.Text);
			UpdateSubmitButton();
		}

		private void UpdatePreview(string pattern)
		{
			string[] patterns = pattern.Trim().Split(new string[1] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			Task<GitCommandResult<string[]>> task = new Task<GitCommandResult<string[]>>(() => new GitLfsGetPreviewFilesGitCommand().Execute(_gitModule, patterns));
			task.ContinueWith(delegate(Task<GitCommandResult<string[]>> taskResult)
			{
				if (!(PatternTextBox.Text != pattern))
				{
					SetStatus(ForkPlusDialogStatus.None, "");
					GitCommandResult<string[]> result = taskResult.Result;
					string text = "";
					string text2 = "";
					if (result.Succeeded)
					{
						string[] result2 = result.Result;
						text = string.Join(Environment.NewLine, result2);
						text2 = ((result2.Length == 1) ? Translate("1 file matches") : string.Format(Translate("{0} files match"), result2.Length));
					}
					else
					{
						text = "";
						text2 = Translate("0 files match");
					}
					PreviewTextBox.Text = text;
					PreviewLabelTextBlock.Text = text2;
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			SetStatus(ForkPlusDialogStatus.InProgress, "");
			task.Start();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
