using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;

namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 LeanBranchingFinish 的多重 behind/ahead 校验 + 命令预览。
	// 此为依赖运行时 git 命令（GetBehindAheadCountGitCommand）的复杂校验：
	// active 落后 remote→Warning（"You must sync '...' first"）
	// local main 与其 remote 不同步→Warning
	// active 与 local main 不同步→Warning
	// 调用 GitCommandResult 是 ForkPlus.Git 类型，VM 可用；SetStatus 副作用留 View。
	// Translate 翻译键通过 RequiresTranslation 标志委托给 View。
	internal sealed class LeanBranchingFinishWindowViewModel
	{
		private readonly GitModule _gitModule;
		private readonly RepositoryData _repositoryData;
		private readonly CommitGraphCache _commitGraphCache;

		public LeanBranchingFinishWindowViewModel(GitModule gitModule, RepositoryData repositoryData, CommitGraphCache commitGraphCache)
		{
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			_commitGraphCache = commitGraphCache;
		}

		// 返回 (IsAllowed, Status, StatusMessage, RequiresTranslation)
		// RequiresTranslation=true 时 StatusMessage 是翻译键，View 需 Translate。
		public (bool IsAllowed, ForkPlusDialogStatus Status, string StatusMessage, bool RequiresTranslation) Validate()
		{
			if (_gitModule == null || _repositoryData == null || _commitGraphCache == null)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			LocalBranch localBranch = _repositoryData.References.LocalMain(_gitModule);
			if (localBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			RemoteBranch remoteBranch = _repositoryData.References.Upstream(localBranch);
			if (remoteBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			LocalBranch activeBranch = _repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			RemoteBranch activeUpstream = _repositoryData.References.Upstream(activeBranch);
			if (activeUpstream != null)
			{
				GitCommandResult<BehindAheadCount> activeVsUpstream = new GetBehindAheadCountGitCommand().Execute(_gitModule, activeBranch.Sha, activeUpstream.Sha, _commitGraphCache);
				if (!activeVsUpstream.Succeeded)
				{
					return (false, ForkPlusDialogStatus.None, string.Empty, false);
				}
				if (activeVsUpstream.Result.Right > 0)
				{
					return (false, ForkPlusDialogStatus.Warning, string.Format("You must sync '{0}' first", activeBranch.Name), true);
				}
			}
			GitCommandResult<BehindAheadCount> mainVsRemote = new GetBehindAheadCountGitCommand().Execute(_gitModule, localBranch.Sha, remoteBranch.Sha, _commitGraphCache);
			if (!mainVsRemote.Succeeded)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			if (!mainVsRemote.Result.AreInSync())
			{
				return (false, ForkPlusDialogStatus.Warning, string.Format("You must checkout and sync '{0}' first", localBranch.Name), true);
			}
			GitCommandResult<BehindAheadCount> activeVsMain = new GetBehindAheadCountGitCommand().Execute(_gitModule, activeBranch.Sha, localBranch.Sha, _commitGraphCache);
			if (!activeVsMain.Succeeded)
			{
				return (false, ForkPlusDialogStatus.None, string.Empty, false);
			}
			if (!activeVsMain.Result.AreInSync())
			{
				return (false, ForkPlusDialogStatus.Warning, string.Format("You must sync '{0}' with '{1}' first", activeBranch.Name, localBranch.Name), true);
			}
			return (true, ForkPlusDialogStatus.None, string.Empty, false);
		}

		public string CommandPreview
		{
			get
			{
				if (_gitModule == null || _repositoryData == null)
				{
					return null;
				}
				LocalBranch localMain = _repositoryData.References.LocalMain(_gitModule);
				LocalBranch activeBranch = _repositoryData.References.ActiveBranch;
				if (localMain == null || activeBranch == null)
				{
					return null;
				}
				var lines = new List<string>();
				RemoteBranch remoteMain = _repositoryData.References.Upstream(localMain);
				if (remoteMain != null)
				{
					lines.Add("git fetch " + remoteMain.Remote + " " + remoteMain.ShortName);
				}
				lines.Add("git checkout " + localMain.Name);
				lines.Add("git merge " + activeBranch.Name);
				return string.Join("\n", lines);
			}
		}
	}
}
