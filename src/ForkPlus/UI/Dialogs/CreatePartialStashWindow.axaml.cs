using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class CreatePartialStashWindow : ForkPlusDialogWindow
	{
		private GitModule _gitModule;
		private bool _aiGenerating;

		protected override bool IsSubmitAllowed => GetFirstSelectedFile() != null;

		public CreatePartialStashWindow(GitModule gitModule, ChangedFile[] filesToStash, ChangedFile[] allChangedFiles)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Save stash");
			base.DialogDescription = Translate("Save your local modifications to a new stash. BOTH staged and unstaged changes will be stashed");
			base.SubmitButtonTitle = Translate("Save Stash");
			ResizeMode = ResizeMode.CanResizeWithGrip;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			_gitModule = gitModule;
			HashSet<string> hashSet = new HashSet<string>();
			List<PartialStashFileViewModel> list = new List<PartialStashFileViewModel>();
			foreach (ChangedFile changedFile in allChangedFiles)
			{
				string filePath = changedFile.Path;
				if (!hashSet.Contains(filePath))
				{
					hashSet.Add(filePath);
					bool selected = filesToStash.ContainsItem((ChangedFile x) => x.Path == filePath);
					list.Add(new PartialStashFileViewModel(changedFile, filePath, selected));
				}
			}
			list.Sort((PartialStashFileViewModel x, PartialStashFileViewModel y) => NaturalStringComparer.Instance.Compare(x.FilePath, y.FilePath));
			PartialStashListBox.ItemsSource = list;
			PartialStashFileViewModel firstSelectedFile = GetFirstSelectedFile();
			if (firstSelectedFile != null)
			{
				PartialStashListBox.ScrollIntoView(firstSelectedFile);
			}
			AiGenerateStashNameButton.RefreshVisibility();
			global::Avalonia.Controls.ToolTip.SetTip(AiGenerateStashNameButton,Translate("Use AI to generate a stash message"));
			UpdateSubmitButton();
			base.Dispatcher.Post(delegate
			{
				StashMessageTextBox.Focus();
			});
		RefreshCommandPreview();
	}

	/// <summary>AI 生成 stash message：读取选中文件相对 HEAD 的 diff，发送给 AI，流式写入 StashMessageTextBox。</summary>
	private void AiGenerateStashNameButton_Click(object sender, RoutedEventArgs e)
	{
		if (_aiGenerating)
		{
			return;
		}
		if (!OpenAiService.IsAiReviewConfigured())
		{
				new MessageBoxWindow("AI Generate Stash Name", "AI is not configured. Please configure AI review settings in Preferences first.", "OK", showCancelButton: false, showWarningIcon: true).ShowDialog();
			return;
		}
		// 收集当前选中的文件路径
		List<string> selectedPaths = new List<string>();
		foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
		{
			if (item.Selected)
			{
				selectedPaths.Add(item.FilePath);
			}
		}
		if (selectedPaths.Count == 0)
		{
			new MessageBoxWindow("AI Generate Stash Name", "No files selected. Nothing to generate a stash message for.", "OK", showCancelButton: false).ShowDialog();
			return;
		}
		_aiGenerating = true;
		AiGenerateStashNameButton.SetBusy(true, Translate("AI is generating..."));
		StashMessageTextBox.Text = "";
		StringBuilder liveMsg = new StringBuilder();
		MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("AI Generate Stash Name"), delegate(JobMonitor monitor)
		{
			try
			{
				// 拉取选中文件相对 HEAD 的 diff（staged + unstaged 都包含）
				List<string> args = new List<string>
				{
					"diff", "--find-renames", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=50", "HEAD", "--"
				};
				args.AddRange(selectedPaths);
				GitCommand gitCommand = new GitCommand(args.ToArray());
				GitRequestResult gitRequestResult = new GitRequest(_gitModule).Command(gitCommand).Execute();
				if (gitRequestResult.ExitCode >= 2)
				{
					base.Dispatcher.Post(delegate
					{
						new ErrorWindow(gitRequestResult.ToGitCommandError().FriendlyDescription).ShowDialog();
					});
					return;
				}
				string patch = gitRequestResult.Stdout;
				if (string.IsNullOrWhiteSpace(patch))
				{
					base.Dispatcher.Post(delegate
					{
						new MessageBoxWindow("AI Generate Stash Name", "No working directory changes detected for selected files. Nothing to generate a stash message for.", "OK", showCancelButton: false).ShowDialog();
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
					base.Dispatcher.Post(delegate
					{
						StashMessageTextBox.Text = snapshot;
					});
				});
				base.Dispatcher.Post(delegate
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
				base.Dispatcher.Post(delegate
				{
					_aiGenerating = false;
				AiGenerateStashNameButton.SetBusy(false);
				});
			}
		}, JobFlags.SaveToLog);
	}

		private void FileCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		// Migration note（迁移回归修复，2026-09-05 E2E 模块14 探针实证）：
		// WPF 下 Checked/Unchecked 触发时 TwoWay 绑定已把 IsChecked 写回
		// ViewModel.Selected；Avalonia 的 IsCheckedChanged 先于绑定回写触发——
		// 此时从 VM 读到的是旧值，导致取消全部勾选后提交按钮仍启用、命令预览
		// 仍显示旧文件集（OnSubmit 读 VM 又会 stash 错误的文件集合）。
		// 故此处直接读 CheckBox.IsChecked 同步推回 VM，消除对绑定回写时序的依赖。
		if (sender is CheckBox checkBox && checkBox.DataContext is PartialStashFileViewModel viewModel)
		{
			viewModel.Selected = checkBox.IsChecked == true;
		}
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void StashMessage_TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		List<string> files = new List<string>();
		foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
		{
			if (item.Selected)
			{
				files.Add(item.FilePath);
			}
		}
		if (files.Count == 0)
		{
			return null;
		}
		List<string> parts = new List<string> { "git", "stash", "push" };
		string stashMessage = StashMessageTextBox.Text;
		if (!string.IsNullOrWhiteSpace(stashMessage))
		{
			string quoted = stashMessage.IndexOf(' ') >= 0 ? ("\"" + stashMessage + "\"") : stashMessage;
			parts.Add("-m");
			parts.Add(quoted);
		}
		parts.Add("--");
		foreach (string f in files)
		{
			parts.Add(f.IndexOf(' ') >= 0 ? ("\"" + f + "\"") : f);
		}
		return string.Join(" ", parts);
	}

	protected override void OnSubmit()
	{
		List<ChangedFile> filesToStash = new List<ChangedFile>();
			foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
			{
				if (item.Selected)
				{
					filesToStash.Add(item.ChangedFile);
				}
			}
			string stashMessage = (string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Partial stash"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, filesToStash.ToArray(), monitor);
				base.Dispatcher.Post(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private PartialStashFileViewModel GetFirstSelectedFile()
		{
			foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
			{
				if (item.Selected)
				{
					return item;
				}
			}
			return null;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
