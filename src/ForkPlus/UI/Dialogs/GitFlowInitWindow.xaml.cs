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
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitFlowInitWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				if (string.IsNullOrEmpty(MasterBranchTextBox.Text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Production branch name can't be empty"));
					return false;
				}
				if (string.IsNullOrEmpty(DevelopBranchTextBox.Text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Development branch name can't be empty"));
					return false;
				}
				if (string.IsNullOrEmpty(FeaturePrefixTextBox.Text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Feature branch prefix can't be empty"));
					return false;
				}
				if (string.IsNullOrEmpty(ReleasePrefixTextBox.Text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Release branch prefix can't be empty"));
					return false;
				}
				if (string.IsNullOrEmpty(HotfixPrefixTextBox.Text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Hotfix branch prefix can't be empty"));
					return false;
				}
				string text = ReferenceNameValidator.Validate(MasterBranchTextBox.Text);
				if (text != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text);
					return false;
				}
				string text2 = ReferenceNameValidator.Validate(DevelopBranchTextBox.Text);
				if (text2 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text2);
					return false;
				}
				string text3 = ReferenceNameValidator.ValidateGitFlow(FeaturePrefixTextBox.Text);
				if (text3 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text3);
					return false;
				}
				string text4 = ReferenceNameValidator.ValidateGitFlow(ReleasePrefixTextBox.Text);
				if (text4 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text4);
					return false;
				}
				string text5 = ReferenceNameValidator.ValidateGitFlow(HotfixPrefixTextBox.Text);
				if (text5 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text5);
					return false;
				}
				string text6 = ReferenceNameValidator.ValidateGitFlow(VersionTagPrefixTextBox.Text);
				if (text6 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text6);
					return false;
				}
				return true;
			}
		}

		public GitFlowInitWindow(GitModule gitModule)
		{
			_gitModule = gitModule;
			InitializeComponent();
			base.DialogTitle = Translate("Initialize Git Flow");
			base.DialogDescription = Translate("Start using Git Flow by initializing it inside an existing git repository");
			base.SubmitButtonTitle = Translate("Initialize Git Flow");
			MasterBranchTextBox.Text = MainBranch() ?? "master";
			DevelopBranchTextBox.Text = "develop";
			FeaturePrefixTextBox.Text = "feature/";
			ReleasePrefixTextBox.Text = "release/";
			HotfixPrefixTextBox.Text = "hotfix/";
		}

		protected override void OnSubmit()
		{
			if (!IsSubmitAllowed)
			{
				return;
			}
			GitFlowSettings gitFlowSettings = new GitFlowSettings(MasterBranchTextBox.Text, DevelopBranchTextBox.Text, FeaturePrefixTextBox.Text, ReleasePrefixTextBox.Text, HotfixPrefixTextBox.Text, VersionTagPrefixTextBox.Text);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Initializing Git Flow..."));
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Initialize Git Flow"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new InitGitFlowGitCommand().Execute(_gitModule, gitFlowSettings, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void MasterBranchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void DevelopBranchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void FeatureName_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void FeaturePrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void ReleasePrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void HotfixPrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void VersionTagPrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		[Null]
		private string MainBranch()
		{
			return IReadOnlyListExtensions.FirstItem(MainWindow.ActiveRepositoryUserControl?.RepositoryData?.References?.LocalBranches, (LocalBranch x) => x.Name == "main")?.Name;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
