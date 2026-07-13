using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class ApplyPatchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly byte[] _patchData;

		private bool _patchContainsCommitHeader;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (_patchData == null)
				{
					return File.Exists(PathTextBox.Text.Trim());
				}
				return true;
			}
		}

		public ApplyPatchWindow(RepositoryUserControl repositoryUserControl, string patchPath)
		{
			_repositoryUserControl = repositoryUserControl;
			_patchContainsCommitHeader = PatchContainsCommitHeader(patchPath);
			InitializeComponent();
			base.DialogTitle = Translate("Apply Patch");
			base.DialogDescription = Translate("Apply Patch");
			base.SubmitButtonTitle = Translate("Apply");
			PathTextBox.Text = patchPath;
		PathTextBox.SelectAll();
		RefreshCreateCommitsCheckBoxVisibility();
		TestForConflicts();
		RefreshCommandPreview();
	}

		public ApplyPatchWindow(RepositoryUserControl repositoryUserControl, byte[] patchData)
		{
			_repositoryUserControl = repositoryUserControl;
			_patchData = patchData;
			string @string = Encoding.UTF8.GetString(patchData);
			_patchContainsCommitHeader = @string.StartsWith("From ");
			InitializeComponent();
			base.DialogTitle = Translate("Apply Patch");
			base.DialogDescription = Translate("Apply patch from clipboard");
			base.SubmitButtonTitle = Translate("Apply");
			LocationLabel.Collapse();
		PathTextBox.Collapse();
		BrowseButton.Collapse();
		RefreshCreateCommitsCheckBoxVisibility();
		TestForConflicts();
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		bool createCommits = CreateCommitsCheckBox.Visibility == Visibility.Visible && CreateCommitsCheckBox.IsChecked.GetValueOrDefault();
		string command = createCommits ? "git am" : "git apply";
		if (_patchData != null)
		{
			return command;
		}
		string filePath = PathTextBox.Text.Trim();
		if (string.IsNullOrEmpty(filePath))
		{
			return null;
		}
		string quotedPath = filePath.IndexOf(' ') >= 0 ? ("\"" + filePath + "\"") : filePath;
		return command + " " + quotedPath;
	}

	protected override void OnSubmit()
	{
		GitModule gitModule = _repositoryUserControl.GitModule;
		bool createCommits = CreateCommitsCheckBox.Visibility == Visibility.Visible && CreateCommitsCheckBox.IsChecked.GetValueOrDefault();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Applying patch..."));
			if (_patchData != null)
			{
				byte[] patchData = _patchData;
				_repositoryUserControl.JobQueue.Add(Translate("Apply patch"), delegate(JobMonitor monitor)
				{
					GitCommandResult result2 = (createCommits ? new AmGitCommand().Execute(gitModule, patchData, monitor) : new ApplyPatchGitCommand().Execute(gitModule, patchData, monitor));
					base.Dispatcher.Async(delegate
					{
						Close(result2);
					});
				});
				return;
			}
			string filePath = PathTextBox.Text.Trim();
			_repositoryUserControl.JobQueue.Add(Translate("Apply patch"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = (createCommits ? new AmGitCommand().Execute(gitModule, filePath, monitor) : new ApplyPatchGitCommand().Execute(gitModule, filePath, monitor));
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			});
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? RepositoryManager.Instance.DefaultSourceDir();
			if (OpenDialog.SelectFile(this, "Select patch", initialDirectory, "Git Patch", "*.*", out var filePath))
			{
				PathTextBox.Text = filePath;
				PathTextBox.Focus();
				PathTextBox.SelectAll();
				_patchContainsCommitHeader = PatchContainsCommitHeader(PathTextBox.Text.Trim());
				RefreshCreateCommitsCheckBoxVisibility();
				UpdateSubmitButton();
				TestForConflicts();
			}
		}

		private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		_patchContainsCommitHeader = PatchContainsCommitHeader(PathTextBox.Text.Trim());
		RefreshCreateCommitsCheckBoxVisibility();
		UpdateSubmitButton();
		TestForConflicts();
		RefreshCommandPreview();
	}

	private void CreateCommitsCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		RefreshCommandPreview();
	}

		private void TestForConflicts()
		{
			GitCommandResult<ApplyPatchTestGitCommand.TestResult> gitCommandResult;
			if (_patchData != null)
			{
				gitCommandResult = new ApplyPatchTestGitCommand().Execute(_repositoryUserControl.GitModule, _patchData);
			}
			else
			{
				string text = PathTextBox.Text.Trim();
				if (!File.Exists(text))
				{
					return;
				}
				gitCommandResult = new ApplyPatchTestGitCommand().Execute(_repositoryUserControl.GitModule, text);
			}
			if (gitCommandResult.Succeeded)
			{
				if (gitCommandResult.Result == ApplyPatchTestGitCommand.TestResult.Success)
				{
					SetStatus(ForkPlusDialogStatus.Success, Translate("Patch can be applied without conflicts"));
				}
				else if (gitCommandResult.Result == ApplyPatchTestGitCommand.TestResult.Conflict)
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Patch will cause conflicts"));
				}
			}
		}

		private void RefreshCreateCommitsCheckBoxVisibility()
		{
			if (_patchContainsCommitHeader)
			{
				CreateCommitsCheckBox.Show();
			}
			else
			{
				CreateCommitsCheckBox.Collapse();
			}
		}

		private bool PatchContainsCommitHeader(string filePath)
		{
			try
			{
				return File.ReadAllText(filePath).StartsWith("From ");
			}
			catch (Exception ex)
			{
				Log.Error("Cannot apply patch", ex);
				return false;
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
