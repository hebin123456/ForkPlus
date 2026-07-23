using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 TrackRemoteBranch 的本地分支名校验 + 命令预览。
	// 复用第 9 模式点 Validate() 4 元组 with RequiresTranslation：
	//   重名消息 "Branch '{0}' already exists" 需 FormatCurrent 翻译（PreferencesLocalization 是 WPF 类型，VM 不可用）。
	// ReferenceNameValidator 输出原文不翻译。Stash 前缀依赖 WorkingDirectoryIsDirty 状态由 View 传入。
	internal sealed class TrackRemoteBranchWindowViewModel
	{
		private readonly LocalBranch[] _localBranches;
		private readonly RemoteBranch _remoteBranch;
		private readonly bool _workingDirectoryIsDirty;

		private string _localBranchName = string.Empty;
		private bool _discard;
		private bool _stashAndReapply;

		public TrackRemoteBranchWindowViewModel(LocalBranch[] localBranches, RemoteBranch remoteBranch, bool workingDirectoryIsDirty)
		{
			_localBranches = localBranches ?? System.Array.Empty<LocalBranch>();
			_remoteBranch = remoteBranch;
			_workingDirectoryIsDirty = workingDirectoryIsDirty;
		}

		public string LocalBranchName
		{
			get => _localBranchName;
			set => _localBranchName = value ?? string.Empty;
		}

		public bool Discard
		{
			get => _discard;
			set => _discard = value;
		}

		public bool StashAndReapply
		{
			get => _stashAndReapply;
			set => _stashAndReapply = value;
		}

		// 返回 (IsAllowed, Status, StatusMessage, RequiresTranslation)。
		// RequiresTranslation=true 时 StatusMessage 是 FormatCurrent 翻译键（含 {0} 占位符，已格式化）。
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage, bool RequiresTranslation) Validate()
		{
			string branchName = _localBranchName.ToLower();
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
				return (false, ForkPlusDialogStatus.Warning, string.Format("Branch '{0}' already exists", _localBranchName), true);
			}
			return (true, ForkPlusDialogStatus.None, string.Empty, false);
		}

		public string CommandPreview
		{
			get
			{
				if (string.IsNullOrWhiteSpace(_localBranchName) || _remoteBranch == null)
				{
					return null;
				}
				var parts = new List<string> { "git", "checkout" };
				if (_discard)
				{
					parts.Add("--force");
				}
				parts.Add("-b");
				parts.Add(_localBranchName);
				parts.Add(_remoteBranch.Name);
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
