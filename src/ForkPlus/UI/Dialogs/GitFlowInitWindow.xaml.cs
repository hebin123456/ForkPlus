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

	// 阶段 3：承接 6 个分支名/前缀"非空 + ReferenceNameValidator"校验 + 命令预览。
	// 新模式点：Validate() 返回 (IsAllowed, Status, StatusMessage, RequiresTranslation)，
	// 非空失败消息需 View 翻译（PreferencesLocalization 是 WPF 类型，VM 不可用），校验器原文原样透传。
	private readonly GitFlowInitWindowViewModel _viewModel = new GitFlowInitWindowViewModel();

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.MasterBranch = MasterBranchTextBox.Text;
				_viewModel.DevelopBranch = DevelopBranchTextBox.Text;
				_viewModel.FeaturePrefix = FeaturePrefixTextBox.Text;
				_viewModel.ReleasePrefix = ReleasePrefixTextBox.Text;
				_viewModel.HotfixPrefix = HotfixPrefixTextBox.Text;
				_viewModel.VersionTagPrefix = VersionTagPrefixTextBox.Text;
				(bool isAllowed, ForkPlusDialogStatus status, string statusMessage, bool requiresTranslation) = _viewModel.Validate();
				string message = requiresTranslation ? Translate(statusMessage ?? string.Empty) : (statusMessage ?? string.Empty);
				SetStatus(status, message);
				return isAllowed;
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
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		_viewModel.MasterBranch = MasterBranchTextBox.Text;
		_viewModel.DevelopBranch = DevelopBranchTextBox.Text;
		return _viewModel.CommandPreview;
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
		RefreshCommandPreview();
	}

		private void DevelopBranchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void FeatureName_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void FeaturePrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void ReleasePrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void HotfixPrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void VersionTagPrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		RefreshCommandPreview();
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
