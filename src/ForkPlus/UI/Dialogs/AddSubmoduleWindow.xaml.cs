using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Dialogs
{
	public partial class AddSubmoduleWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly SubmodulesToUpdate _submodulesToUpdate;

	// 阶段 3：承接 url+path 非空 + 路径合法性（MakePath）校验 + 命令预览。
	// IsPathValid 由 VM 在 Path 设置时 try MakePath 计算。剪贴板 URL 探测/GitUrl 解析留 View。
	private readonly AddSubmoduleWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.RepositoryUrl = RepositoryUrlTextBox.Text;
				_viewModel.Path = PathTextBox.Text;
				return _viewModel.IsSubmitAllowed;
			}
		}

		protected override string GetCommandPreview()
		{
			_viewModel.RepositoryUrl = RepositoryUrlTextBox.Text;
			_viewModel.Path = PathTextBox.Text;
			return _viewModel.CommandPreview;
		}

		public AddSubmoduleWindow(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate)
		{
			_gitModule = gitModule;
			_submodulesToUpdate = submodulesToUpdate;
			_viewModel = new AddSubmoduleWindowViewModel(_gitModule);
			InitializeComponent();
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			base.DialogTitle = Translate("Add Submodule");
			base.DialogDescription = Translate("Add new submodule repository reference");
			base.SubmitButtonTitle = Translate("Add Submodule");
			string text = TryGetClipboardRepositoryUrl();
			if (!string.IsNullOrEmpty(text))
			{
				GitUrl gitUrl = new GitUrl(text);
				if (gitUrl.IsValid)
				{
					PathTextBox.Text = gitUrl.RepositoryName;
					RepositoryUrlTextBox.Text = gitUrl.UrlString;
				}
			}
			RepositoryUrlTextBox.SelectAll();
		}

		protected override void OnSubmit()
		{
			string url = RepositoryUrlTextBox.Text;
			string path = PathHelper.NormalizeUnix(PathTextBox.Text);
			bool fetchNestedSubmodules = FetchNestedSubmodulesCheckBox.IsChecked.Value;
			SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Adding submodule..."));
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(string.Format(Translate("Add submodule '{0}'"), PathHelper.GetReadableFileName(path)), delegate(JobMonitor monitor)
			{
				GitCommandResult addSubmoduleResult = new AddSubmoduleGitCommand().Execute(_gitModule, url, path, monitor);
				if (!addSubmoduleResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(addSubmoduleResult);
					});
				}
				else
				{
					if (fetchNestedSubmodules && submodulesToUpdate.Length > 0)
					{
						base.Dispatcher.Async(delegate
						{
							SetStatus(ForkPlusDialogStatus.InProgress, Translate("Fetching nested submodules..."));
						});
						GitCommandResult updateSubmoduleResult = new UpdateSubmodulesGitCommand().Execute(_gitModule, submodulesToUpdate, monitor);
						if (!updateSubmoduleResult.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								Close(updateSubmoduleResult);
							});
							return;
						}
					}
					base.Dispatcher.Async(delegate
					{
						Close(addSubmoduleResult);
					});
				}
			});
		}

		private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_viewModel.Path = PathTextBox.Text;
			if (string.IsNullOrEmpty(PathTextBox.Text))
			{
				FinalPathHintTextBlock.Collapse();
			}
			else if (_viewModel.IsPathValid)
			{
				FinalPathHintTextBlock.Text = PathHelper.Normalize(_viewModel.NormalizedPathHint);
				FinalPathHintTextBlock.Show();
			}
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void RepositoryUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private static string TryGetClipboardRepositoryUrl()
		{
			string text = ServiceLocator.Clipboard.GetText();
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			text = NormalizeClipboardRepositoryUrl(text);
			try
			{
				GitUrl gitUrl = new GitUrl(text);
				if (!gitUrl.IsValid || !LooksLikeRepositoryReference(text))
				{
					return null;
				}
			}
			catch (ArgumentException)
			{
				return null;
			}
			return text;
		}

		private static string NormalizeClipboardRepositoryUrl(string clipboardText)
		{
			string text = clipboardText.Trim();
			const string prefix = "git clone ";
			if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(prefix.Length);
			}
			return text.Trim().Trim('"');
		}

		private static bool LooksLikeRepositoryReference(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			if (text.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || text.Contains("@vs-ssh.visualstudio.com") || text.Contains("://"))
			{
				return true;
			}
			if (!Path.IsPathRooted(text) && !text.StartsWith(".", StringComparison.Ordinal) && !text.StartsWith("\\\\", StringComparison.Ordinal))
			{
				return false;
			}
			if (Directory.Exists(text))
			{
				return true;
			}
			return text.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
