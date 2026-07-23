using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class TagDetailsWindow : ForkPlusDialogWindow
	{

		public TagDetailsWindow(GitModule gitModule, Tag tag)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Tag Details");
			base.DialogDescription = "";
			base.ShowSubmitButton = false;
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			GitPointView.Value = tag;
			GitCommandResult<AnnotatedTagDetails> gitCommandResult = new GetTagMessageGitCommand().Execute(gitModule, tag.TargetObjectSha.Value);
			if (gitCommandResult.Succeeded)
			{
				AnnotatedTagDetails result = gitCommandResult.Result;
				TaggerAvatarImage.UserIdentity = result.Tagger;
				TaggerTextBlock.Text = result.Tagger.Name;
				TaggerEmailTextBlock.Text = result.Tagger.Email;
				TaggerDateTextBlock.Text = result.TaggerDate.ToString(Consts.FullDateTimeFormat);
				TagDetailsTextBox.Text = result.Message;
			}
			else
			{
				TaggerAvatarImage.UserIdentity = null;
				TaggerTextBlock.Text = "";
				TaggerEmailTextBlock.Text = "";
				TaggerDateTextBlock.Text = "";
				TagDetailsTextBox.Text = new GetTagMessageGitCommand().Execute(gitModule, tag.Name).Result;
			}
		}

	}
}
