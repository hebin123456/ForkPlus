using Avalonia.Controls;
using System;
using System.Threading.Tasks;
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

	// 阶段 3：承接 pattern 非空校验 + 命令预览。UpdatePreview（Task/TaskScheduler）留 View。
	private readonly GitLfsTrackWindowViewModel _viewModel = new GitLfsTrackWindowViewModel();

	protected override bool IsSubmitAllowed
	{
		get
		{
			_viewModel.Pattern = PatternTextBox.Text;
			return _viewModel.IsSubmitAllowed;
		}
	}

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
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 PatternTextBox 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
	{
		_viewModel.Pattern = PatternTextBox.Text;
		return _viewModel.CommandPreview;
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
			RefreshCommandPreview();
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
