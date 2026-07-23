using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 LeanBranchingStart 的本地分支名校验 + 命令预览。
	// 复用第 9 模式点 Validate() 4 元组 with RequiresTranslation：
	//   重名消息 "Branch '{0}' already exists" 需 Translate+Format 翻译（PreferencesLocalization 是 WPF 类型，VM 不可用）。
	// ReferenceNameValidator 输出原文不翻译。Stash 前缀依赖 WorkingDirectoryIsDirty 由 View 传入。
	internal sealed class LeanBranchingStartWindowViewModel
	{
		private readonly LocalBranch[] _localBranches;
		private readonly Branch _mainBranch;
		private readonly bool _workingDirectoryIsDirty;

		private string _branchName = string.Empty;
		private bool _stashAndReapply;

		public LeanBranchingStartWindowViewModel(LocalBranch[] localBranches, Branch mainBranch, bool workingDirectoryIsDirty)
		{
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
			_mainBranch = mainBranch;
			_workingDirectoryIsDirty = workingDirectoryIsDirty;
		}

		public string BranchName
		{
			get => _branchName;
			set => _branchName = value ?? string.Empty;
		}

		public bool StashAndReapply
		{
			get => _stashAndReapply;
			set => _stashAndReapply = value;
		}

		// 返回 (IsAllowed, Status, StatusMessage, RequiresTranslation)。
		// RequiresTranslation=true 时 StatusMessage 是已格式化的翻译键（FormatCurrent 入参）。
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage, bool RequiresTranslation) Validate()
		{
			string branchName = _branchName.ToLower();
			if (string.IsNullOrEmpty(branchName))
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			string invalid = ReferenceNameValidator.Validate(branchName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid, false);
			}
			if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
			{
				return (false, ForkPlusDialogStatus.Warning, string.Format("Branch '{0}' already exists", _branchName), true);
			}
			return (true, ForkPlusDialogStatus.None, string.Empty, false);
		}

		public string CommandPreview
		{
			get
			{
				// LeanBranchingStartWindow 固定 checkout=true，对应 git checkout -b <branch> <mainBranch>
				if (string.IsNullOrWhiteSpace(_branchName))
				{
					return null;
				}
				var parts = new List<string> { "git", "checkout", "-b", _branchName };
				string startPoint = _mainBranch?.Name;
				if (!string.IsNullOrEmpty(startPoint))
				{
					parts.Add(startPoint);
				}
				string command = string.Join(" ", parts);
				if (_workingDirectoryIsDirty && _stashAndReapply)
				{
					command = "git stash\n" + command;
				}
				return command;
			}
		}
	}
}
