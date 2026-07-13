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
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CheckoutBranchAsWorktreeWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private readonly LocalBranch _branch;

		private readonly RepositoryWorktrees _worktrees;

		private string _worktreesContainerPath;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(PathTextBox.Text.Trim()))
				{
					return false;
				}
				if (_worktrees.WorktreesByFullReference.ContainsKey(_branch.FullReference))
				{
					return false;
				}
				return true;
			}
		}

		public CheckoutBranchAsWorktreeWindow(RepositoryUserControl repositoryUserControl, LocalBranch branch)
		{
			_repositoryUserControl = repositoryUserControl;
			_branch = branch;
			_gitModule = repositoryUserControl.GitModule;
			_worktrees = repositoryUserControl.RepositoryData.Worktrees;
			string directoryName = Path.GetDirectoryName(_gitModule.CommonGitDir);
			_worktreesContainerPath = Path.Combine(Path.GetDirectoryName(directoryName), Path.GetFileName(directoryName) + "-worktrees");
			InitializeComponent();
			base.DialogTitle = Translate("Checkout Branch as Worktree");
			base.DialogDescription = Translate("Checkout branch in separate worktree");
			base.SubmitButtonTitle = Translate("Create");
			GitPointView.Value = branch;
		RefreshPath();
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		if (_branch == null || string.IsNullOrEmpty(_branch.Name))
		{
			return null;
		}
		string worktreePath = PathTextBox.Text.Trim();
		if (string.IsNullOrEmpty(worktreePath))
		{
			return null;
		}
		string quotedPath = worktreePath.IndexOf(' ') >= 0 ? ("\"" + worktreePath + "\"") : worktreePath;
		return "git worktree add " + quotedPath + " " + _branch.Name;
	}

	protected override void OnSubmit()
	{
		GitModule gitModule = _gitModule;
		string worktreePath = PathHelper.NormalizeUnix(PathTextBox.Text.Trim());
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating worktree..."));
			_repositoryUserControl.JobQueue.Add(Translate("Checkout Branch As Worktree"), delegate(JobMonitor monitor)
			{
				GitCommandResult checkoutBranchAsWorktreeResult = new AddWorktreeGitCommand().Execute(_gitModule, worktreePath, _branch.Name, monitor);
				if (!checkoutBranchAsWorktreeResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(checkoutBranchAsWorktreeResult);
					});
				}
				else
				{
					GitCommandResult<GitModule> openWorktreeResult = new OpenGitRepositoryGitCommand().Execute(worktreePath);
					if (!openWorktreeResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(openWorktreeResult.ToGitCommandResult());
						});
					}
					else
					{
						GitModule result = openWorktreeResult.Result;
						if (submodulesToUpdate.Length > 0)
						{
							base.Dispatcher.Async(delegate
							{
								SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
							});
							GitCommandResult updateSubmodulesResult = UpdateSubmodules(result, submodulesToUpdate, gitModule.CommonGitDir, monitor);
							if (!updateSubmodulesResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(updateSubmodulesResult);
								});
								return;
							}
						}
						base.Dispatcher.Async(delegate
						{
							MainWindow.Instance.TabManager.OpenRepository(worktreePath);
							Close(checkoutBranchAsWorktreeResult);
						});
					}
				}
			});
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
	{
		string initialDirectory = (Directory.Exists(_worktreesContainerPath) ? _worktreesContainerPath : Path.GetDirectoryName(_worktreesContainerPath));
		if (OpenDialog.SelectDirectory(this, "Select location", initialDirectory, out var directoryPath))
		{
			_worktreesContainerPath = directoryPath;
			RefreshPath();
			UpdateSubmitButton();
			RefreshCommandPreview();
		}
	}

	private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void RefreshPath()
		{
			string path = _branch.Name.Replace('/', '-');
			PathTextBox.Text = Path.Combine(_worktreesContainerPath, path);
			PathTextBox.CaretIndex = PathTextBox.Text.Length;
		}

		private static GitCommandResult UpdateSubmodules(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, string referenceGitDir, JobMonitor monitor)
		{
			GitCommandResult gitCommandResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor, referenceGitDir);
			if (!gitCommandResult.Succeeded && gitCommandResult.Error is GitCommandError.UnsafeRepository unsafeRepository)
			{
				GitCommandResult gitCommandResult2 = new AddRepositoryToSafeDirectoriesListGitCommand().Execute(unsafeRepository.ProposedRepositoryPath);
				if (!gitCommandResult2.Succeeded)
				{
					return gitCommandResult2;
				}
				return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor, referenceGitDir);
			}
			return gitCommandResult;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
