using System;
using System.Collections;
using System.Collections.Generic;
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
	public partial class CreatePartialStashWindow : ForkPlusDialogWindow
	{
		private GitModule _gitModule;
		private bool _aiGenerating;

		// 阶段 3：承接 partial stash 选中文件路径投影 + stash message + 命令预览。
		// PartialStashFileViewModel（含 Selected 状态）作为列表项留 View；
		// VM 仅持选中文件路径列表 + stash message。AI 生成 stash name 留 View。
		private readonly CreatePartialStashWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				PushSelectionToViewModel();
				return _viewModel.IsSubmitAllowed;
			}
		}

		private void PushSelectionToViewModel()
		{
			var paths = new System.Collections.Generic.List<string>();
			foreach (PartialStashFileViewModel item in (System.Collections.IEnumerable)PartialStashListBox.Items)
			{
				if (item.Selected)
				{
					paths.Add(item.FilePath);
				}
			}
			_viewModel.SelectedFilePaths = paths;
			_viewModel.StashMessage = StashMessageTextBox.Text;
		}

		public CreatePartialStashWindow(GitModule gitModule, ChangedFile[] filesToStash, ChangedFile[] allChangedFiles)
		{
			_viewModel = new CreatePartialStashWindowViewModel();
			InitializeComponent();
			base.DialogTitle = Translate("Save stash");
			base.DialogDescription = Translate("Save your local modifications to a new stash. BOTH staged and unstaged changes will be stashed");
			base.SubmitButtonTitle = Translate("Save Stash");
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
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
			// AI 生成 stash 名称按钮：仅在 AI 配置完毕时显示
			if (!OpenAiService.IsAiReviewConfigured())
			{
				AiGenerateStashNameButton.Collapse();
			}
			else
			{
				AiGenerateStashNameButton.ToolTip = Translate("Use AI to generate a stash message");
			}
			UpdateSubmitButton();
			base.Dispatcher.Async(delegate
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
			MessageBox.Show(
				Translate("AI is not configured. Please configure AI review settings in Preferences first."),
				Translate("AI Generate Stash Name"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
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
			MessageBox.Show(
				Translate("No files selected. Nothing to generate a stash message for."),
				Translate("AI Generate Stash Name"),
				MessageBoxButton.OK,
				MessageBoxImage.Information);
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
							Translate("No working directory changes detected for selected files. Nothing to generate a stash message for."),
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

		private void FileCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void StashMessage_TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
		{
			PushSelectionToViewModel();
			return _viewModel.CommandPreview;
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
				base.Dispatcher.Async(delegate
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
