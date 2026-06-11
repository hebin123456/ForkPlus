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

		private bool _repositoryPathValid;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(RepositoryUrlTextBox.Text) && !string.IsNullOrWhiteSpace(PathTextBox.Text))
				{
					return _repositoryPathValid;
				}
				return false;
			}
		}

		public AddSubmoduleWindow(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate)
		{
			_gitModule = gitModule;
			_submodulesToUpdate = submodulesToUpdate;
			InitializeComponent();
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			base.DialogTitle = "Add Submodule";
			base.DialogDescription = "Add new submodule repository reference";
			base.SubmitButtonTitle = "Add Submodule";
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
			_repositoryPathValid = true;
			try
			{
				string text = PathTextBox.Text;
				if (string.IsNullOrEmpty(text))
				{
					FinalPathHintTextBlock.Collapse();
				}
				else
				{
					FinalPathHintTextBlock.Text = PathHelper.Normalize(_gitModule.MakePath(text));
					FinalPathHintTextBlock.Show();
				}
			}
			catch
			{
				_repositoryPathValid = false;
			}
			UpdateSubmitButton();
		}

		private void RepositoryUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
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
