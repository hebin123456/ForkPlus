using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 CreateBranch 的本地分支名校验 + 命令预览。
	// 复用 Validate() 三元组模式：重名消息 "Branch '...' already exists" 用原文（不翻译，与 LeanBranchingStart 不一致）。
	// Stash 前缀依赖 WorkingDirectoryIsDirty 由 View 传入。
	internal sealed class CreateBranchWindowViewModel
	{
		private readonly LocalBranch[] _localBranches;
		private readonly IGitPoint _gitPoint;
		private readonly bool _workingDirectoryIsDirty;

		private string _branchName = string.Empty;
		private bool _checkout = true;
		private bool _stashAndReapply;

		public CreateBranchWindowViewModel(LocalBranch[] localBranches, IGitPoint gitPoint, bool workingDirectoryIsDirty)
		{
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
			_gitPoint = gitPoint;
			_workingDirectoryIsDirty = workingDirectoryIsDirty;
		}

		public string BranchName
		{
			get => _branchName;
			set => _branchName = value ?? string.Empty;
		}

		public bool Checkout
		{
			get => _checkout;
			set => _checkout = value;
		}

		public bool StashAndReapply
		{
			get => _stashAndReapply;
			set => _stashAndReapply = value;
		}

		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage) Validate()
		{
			string branchName = _branchName.ToLower();
			if (string.IsNullOrEmpty(branchName))
			{
				return (false, ForkPlusDialogStatus.None, string.Empty);
			}
			string invalid = ReferenceNameValidator.Validate(branchName);
			if (invalid != null)
			{
				return (false, ForkPlusDialogStatus.Warning, invalid);
			}
			if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
			{
				return (false, ForkPlusDialogStatus.Warning, "Branch '" + _branchName + "' already exists");
			}
			return (true, ForkPlusDialogStatus.None, string.Empty);
		}

		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_branchName))
				{
					return null;
				}
				var parts = new List<string> { "git" };
				if (_checkout)
				{
					parts.Add("checkout");
					parts.Add("-b");
				}
				else
				{
					parts.Add("branch");
				}
				parts.Add(_branchName);
				string startPoint = _gitPoint?.FriendlyName;
				if (!string.IsNullOrEmpty(startPoint))
				{
					parts.Add(startPoint);
				}
				string command = string.Join(" ", parts);
				if (_checkout && _workingDirectoryIsDirty && _stashAndReapply)
				{
					command = "git stash\n" + command;
				}
				return command;
			}
		}
	}
}
