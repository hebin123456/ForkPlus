using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CreateWorktreeWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private readonly RepositoryWorktrees _worktrees;

		private readonly RepositoryReferences _repositoryReferences;

		private string _worktreesContainerPath;

		// 阶段 3：承接 worktree 创建前的多重校验 + 命令预览。
		// 复用 Validate() 三元组模式：分支名校验/worktree 重名/分支重名→Warning，路径为空→静默 false。
		// RefreshPath/autocomplete/BrowseButton_Click 留 View。
		private readonly CreateWorktreeWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				_viewModel.BranchName = BranchNameTextBox.Text;
				_viewModel.Path = PathTextBox.Text;
				(bool isAllowed, ForkPlusDialogStatus status, string statusMessage) = _viewModel.Validate();
				if (status != ForkPlusDialogStatus.None)
				{
					SetStatus(status, statusMessage);
				}
				return isAllowed;
			}
		}

		public CreateWorktreeWindow(RepositoryUserControl repositoryUserControl, LocalBranch startBranch)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = repositoryUserControl.GitModule;
			_worktrees = repositoryUserControl.RepositoryData.Worktrees;
			_repositoryReferences = repositoryUserControl.RepositoryData.References;
			_viewModel = new CreateWorktreeWindowViewModel(_worktrees, _repositoryReferences);
			string directoryName = Path.GetDirectoryName(_gitModule.CommonGitDir);
			_worktreesContainerPath = Path.Combine(Path.GetDirectoryName(directoryName), Path.GetFileName(directoryName) + "-worktrees");
			InitializeComponent();
			base.DialogTitle = Translate("Create Worktree");
			base.DialogDescription = Translate("Create branch and checkout it in a separate worktree");
			base.SubmitButtonTitle = Translate("Create");
			LocalBranchesComboBox.ItemsSource = _repositoryReferences.LocalBranches;
			LocalBranchesComboBox.SelectedItem = startBranch;
			ReferenceTextBox branchNameTextBox = BranchNameTextBox;
			ForkPlus.Git.Reference[] references = _repositoryReferences.Items.CompactMap((ForkPlus.Git.Reference x) => x as LocalBranch);
			branchNameTextBox.SetAutocompleteProvider(new ReferenceNameAutocompleteProvider(references));
			RefreshPath();
		UpdateSubmitButton();
		base.Loaded += delegate
		{
			BranchNameTextBox.Focus();
		};
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		_viewModel.BranchName = BranchNameTextBox.Text;
		_viewModel.Path = PathTextBox.Text;
		return _viewModel.CommandPreview;
	}

	protected override void OnSubmit()
	{
		object selectedItem = LocalBranchesComboBox.SelectedItem;
			LocalBranch selectedBranch = selectedItem as LocalBranch;
			if (selectedBranch == null)
			{
				return;
			}
			string branchName = BranchNameTextBox.Text;
			string worktreePath = PathHelper.NormalizeUnix(PathTextBox.Text.Trim());
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating worktree..."));
			_repositoryUserControl.JobQueue.Add(Translate("Create Worktree"), delegate(JobMonitor monitor)
			{
				GitCommandResult createWorktreeResult = new AddWorktreeGitCommand().Execute(_gitModule, worktreePath, branchName, selectedBranch.Sha, monitor);
				if (!createWorktreeResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(createWorktreeResult);
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
							GitCommandResult updateSubmodulesResult = UpdateSubmodules(result, submodulesToUpdate, _gitModule.CommonGitDir, monitor);
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
							Close(createWorktreeResult);
						});
					}
				}
			});
		}

		private void BranchName_TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshPath();
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = (Directory.Exists(_worktreesContainerPath) ? _worktreesContainerPath : Path.GetDirectoryName(_worktreesContainerPath));
			if (OpenDialog.SelectDirectory(this, "Select location", initialDirectory, out var directoryPath))
			{
				_worktreesContainerPath = directoryPath;
				RefreshPath();
				UpdateSubmitButton();
			}
		}

		private void RefreshPath()
		{
			string text = BranchNameTextBox.Text.Replace('/', '-');
			if (!string.IsNullOrEmpty(text))
			{
				PathTextBox.Text = Path.Combine(_worktreesContainerPath, text);
			}
			else
			{
				PathTextBox.Text = _worktreesContainerPath;
			}
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
