using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class AddGitIgnorePatternWindow : ForkPlusDialogWindow
	{
		private readonly DelayedAction<string> _updatePreviewAction;

		private readonly GitModule _gitModule;

		private readonly string _initialPattern;

		protected override bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(PatternTextBox.Text);

		public AddGitIgnorePatternWindow(GitModule gitModule, string initialPattern)
		{
			InitializeComponent();
			_updatePreviewAction = new DelayedAction<string>(UpdatePreview, 0.3);
			_gitModule = gitModule;
			_initialPattern = initialPattern;
			base.DialogTitle = Translate("Add Pattern to .gitignore");
			base.DialogDescription = Translate("A gitignore file specifies intentionally untracked files that Git should ignore. Files already tracked by Git will be untracked.");
			base.SubmitButtonTitle = Translate("Add to .gitignore");
			PatternLabelTextBlock.Text = Translate("(one pattern per line)");
			PreviewLabelTextBlock.Text = Translate("0 files match");
			PatternTextBox.Text = _initialPattern;
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 PatternTextBox 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
			// 修复（2026-09-12，"首开 Pattern 已填字但 Preview 未自动生成"）：
			// UpdatePreview 已改为 Task.Run 后台跑 git + Dispatcher.UIThread.Post 显式回 UI，
			// 不再依赖构造期的同步上下文——此处构造期立即预填一次（热启动预览），
			// 并在 Opened 后再刷一次兜底（窗口显示时 UI 线程 Dispatcher 必定就绪，
			// 规避某些时机下构造期线程池回填竞态）。
			_updatePreviewAction.InvokeNow(_initialPattern);
			base.Opened += delegate
			{
				_updatePreviewAction.InvokeNow(PatternTextBox.Text);
			};
		}

		protected override string GetCommandPreview()
		{
			// 与 IgnoreFilesGitCommand 对应：把 pattern 写入 .gitignore，并对已跟踪文件执行 git rm --cached
			string text = PatternTextBox.Text;
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			return "# .gitignore\ngit rm --cached -r .";
		}

		protected override void OnSubmit()
		{
			string pattern = PatternTextBox.Text.Trim();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Adding files to .gitignore..."));
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Add files to .gitignore"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new IgnoreFilesGitCommand().Execute(_gitModule, pattern, monitor);
				base.Dispatcher.Post(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void PatternTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updatePreviewAction.InvokeWithDelay(PatternTextBox.Text);
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void UpdatePreview(string pattern)
		{
			string[] patterns = pattern.Trim().Split(Consts.Chars.NewLine);
			Task<GitCommandResult<string[]>> task = Task.Run(delegate
			{
				return new GetFilesToIgnoreGitCommand().Execute(_gitModule, patterns);
			});
			task.ContinueWith(delegate(Task<GitCommandResult<string[]>> taskResult)
			{
				// 修复（2026-09-12，与 Opened 首刷配套）：原用
				// TaskScheduler.FromCurrentSynchronizationContext() 抓构造期/线程池的同步上下文
				// 回 UI 线程回填，首开时不可靠导致 Preview 空白。改为 Task.Run 后台跑 git、
				// 经 Dispatcher.UIThread.Post 显式回 UI 线程更新控件，路径确定、线程安全。
				GitCommandResult<string[]> result;
				if (taskResult.IsFaulted || taskResult.IsCanceled)
				{
					result = GitCommandResult<string[]>.Failure(new InvalidOperationException("GetFilesToIgnoreGitCommand failed"));
				}
				else
				{
					result = taskResult.Result;
				}
				Dispatcher.UIThread.Post(delegate
				{
					if (PatternTextBox.Text != pattern)
					{
						return;
					}
					string text = "";
					string text2 = "";
					if (result.Succeeded)
					{
						string[] result2 = result.Result;
						text = string.Join("\n", result2);
						text2 = ((result2.Length == 1) ? Translate("1 file matches") : string.Format(Translate("{0} files match"), result2.Length));
					}
					else
					{
						text = "";
						text2 = Translate("0 files match");
					}
					PreviewTextBox.Text = text;
					PreviewLabelTextBlock.Text = text2;
					SetStatus(ForkPlusDialogStatus.None, "");
				});
			});
			SetStatus(ForkPlusDialogStatus.InProgress, "");
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
