using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs
{
	public partial class SaveStashWindow : ForkPlusDialogWindow
	{
		private GitModule _gitModule;
		private bool _aiGenerating;

		protected override string GetCommandPreview()
		{
			bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
			string stashMessage = StashMessageTextBox.Text;
			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string> { "git", "stash", "push" };
			if (!string.IsNullOrWhiteSpace(stashMessage))
			{
				parts.Add("-m");
				parts.Add("\"" + stashMessage + "\"");
			}
			if (stageNewFiles)
			{
				parts.Add("--include-untracked");
			}
			return string.Join(" ", parts);
		}

		public SaveStashWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Save stash");
			base.DialogDescription = Translate("Save your local changes to a new stash");
			base.SubmitButtonTitle = Translate("Save Stash");
			_gitModule = gitModule;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			// AI 生成 stash 名称按钮：仅在 AI 配置完毕时显示
			if (!OpenAiService.IsAiReviewConfigured())
			{
				AiGenerateStashNameButton.Collapse();
			}
			else
			{
				AiGenerateStashNameButton.ToolTip = Translate("Use AI to generate a stash message");
			}
		}

		/// <summary>AI 生成 stash message：读取工作区 diff，发送给 AI，流式写入 StashMessageTextBox。</summary>
		private void AiGenerateStashNameButton_Click(object sender, RoutedEventArgs e)
		{
			if (_aiGenerating)
			{
				return;
			}
			if (!OpenAiService.IsAiReviewConfigured())
			{
				MessageBox.Show(
					Translate("AI is not configured. Please configure AI review settings in Preferences first."),
					Translate("AI Generate Stash Name"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}
			_aiGenerating = true;
			AiGenerateStashNameButton.IsEnabled = false;
			string originalToolTip = AiGenerateStashNameButton.ToolTip?.ToString();
			AiGenerateStashNameButton.ToolTip = Translate("AI is generating...");
			StashMessageTextBox.Text = "";
			StringBuilder liveMsg = new StringBuilder();
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("AI Generate Stash Name"), delegate(JobMonitor monitor)
			{
				try
				{
					// 获取工作区全量 diff（staged + unstaged，相对 HEAD）
					GitCommand gitCommand = new GitCommand("diff", "--find-renames", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=50", "HEAD");
					GitRequestResult gitRequestResult = new GitRequest(_gitModule).Command(gitCommand).Execute();
					if (gitRequestResult.ExitCode >= 2)
					{
						base.Dispatcher.Async(delegate
						{
							new ErrorWindow(gitRequestResult.ToGitCommandError().FriendlyDescription).ShowDialog();
						});
						return;
					}
					string patch = gitRequestResult.Stdout;
					if (string.IsNullOrWhiteSpace(patch))
					{
						base.Dispatcher.Async(delegate
						{
							MessageBox.Show(
								Translate("No working directory changes detected. Nothing to generate a stash message for."),
								Translate("AI Generate Stash Name"),
								MessageBoxButton.OK,
								MessageBoxImage.Information);
						});
						return;
					}
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.GenerateStashName(patch, monitor, delegate(string chunk)
					{
						if (string.IsNullOrEmpty(chunk))
						{
							return;
						}
						liveMsg.Append(chunk);
						string snapshot = liveMsg.ToString();
						base.Dispatcher.Async(delegate
						{
							StashMessageTextBox.Text = snapshot;
						});
					});
					base.Dispatcher.Async(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!response.Succeeded)
						{
							new ErrorWindow(response.Error.FriendlyMessage).ShowDialog();
						}
						else
						{
							// stash message 应该是单行，去掉可能的换行
							string msg = response.Result.Message?.Trim() ?? "";
							msg = msg.Replace("\r", " ").Replace("\n", " ");
							while (msg.Contains("  "))
							{
								msg = msg.Replace("  ", " ");
							}
							StashMessageTextBox.Text = msg;
						}
					});
				}
				finally
				{
					// 任务结束（无论成功失败/取消）恢复按钮状态
					base.Dispatcher.Async(delegate
					{
						_aiGenerating = false;
						AiGenerateStashNameButton.IsEnabled = true;
						AiGenerateStashNameButton.ToolTip = originalToolTip ?? Translate("Use AI to generate a stash message");
					});
				}
			}, JobFlags.SaveToLog);
		}

		protected override void OnSubmit()
		{
			bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
			string stashMessage = (string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text);
			ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Stash local changes"), delegate(JobMonitor monitor)
			{
				GitCommandResult<bool> result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, stageNewFiles, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(GitCommandResult.Failure(result.Error));
				});
			}, JobFlags.SaveToLog);
		}

		private void StashMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void CheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
