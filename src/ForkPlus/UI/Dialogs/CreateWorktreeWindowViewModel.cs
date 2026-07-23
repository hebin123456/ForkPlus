using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 worktree 创建前的多重校验 + 命令预览。
	// 复用 Validate() 三元组模式（GitFlowStartHotfix）：分支名校验器不通过→Warning，
	// worktree/分支重名→Warning，路径为空→静默 false。
	// RefreshPath/autocomplete/BrowseButton_Click 留 View。
	internal sealed class CreateWorktreeWindowViewModel
	{
		private readonly RepositoryWorktrees _worktrees;
		private readonly RepositoryReferences _repositoryReferences;

		private string _branchName = string.Empty;
		private string _path = string.Empty;

		public CreateWorktreeWindowViewModel(RepositoryWorktrees worktrees, RepositoryReferences repositoryReferences)
		{
			_worktrees = worktrees;
			_repositoryReferences = repositoryReferences;
		}

		public string BranchName
		{
			get => _branchName;
			set => _branchName = value ?? string.Empty;
		}

		public string Path
		{
			get => _path;
			set => _path = value ?? string.Empty;
		}

		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			if (string.IsNullOrEmpty(_branchName))
			{
				return (false, ForkPlusDialogStatus.None, string.Empty);
			}
			string invalid = ReferenceNameValidator.Validate(_branchName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			string key = "refs/heads/" + _branchName;
			if (_worktrees.WorktreesByFullReference.ContainsKey(key))
			{
				return (false, ForkPlusDialogStatus.Warning, "Worktree '" + _branchName + "' already exists");
			}
			if (_repositoryReferences.LocalBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == _branchName.ToLower()))
			{
				return (false, ForkPlusDialogStatus.Warning, "Branch '" + _branchName + "' already exists");
			}
			if (string.IsNullOrWhiteSpace(_path.Trim()))
			{
				return (false, ForkPlusDialogStatus.None, string.Empty);
			}
			return (true, ForkPlusDialogStatus.None, string.Empty);
		}

		public string CommandPreview
		{
			get
			{
				string branchName = _branchName;
				string worktreePath = _path.Trim();
				if (string.IsNullOrEmpty(branchName) || string.IsNullOrEmpty(worktreePath))
				{
					return null;
				}
				string quotedPath = worktreePath.IndexOf(' ') >= 0 ? ("\"" + worktreePath + "\"") : worktreePath;
				return "git worktree add " + quotedPath + " " + branchName;
			}
		}
	}
}
