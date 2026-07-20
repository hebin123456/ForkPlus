using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// Fork 工作流同步冲突预检结果。
	/// </summary>
	public enum ForkSyncStatus
	{
		/// <summary>upstream 无新提交（或本地已包含 upstream 全部提交），可直接 push 到 fork。</summary>
		SafeToPush,
		/// <summary>upstream 有新提交但无冲突，建议同步（fast-forward 或干净合并）后再 push。</summary>
		ShouldSyncNoConflict,
		/// <summary>upstream 有新提交且会产生冲突，必须先 pull 并解决冲突才能继续。</summary>
		MustSyncWithConflict,
		/// <summary>未配置 upstream 远端，或 upstream 上不存在对应分支。</summary>
		NoUpstreamBranch,
		/// <summary>检测过程出错，无法判断。</summary>
		Unknown
	}

	/// <summary>
	/// Fork 工作流同步冲突预检命令：
	/// 1. 拉取 upstream 远端引用（fetch，不合并）；
	/// 2. 通过 merge-base 判断 upstream 是否有新提交；
	/// 3. 用 legacy 3-arg merge-tree 检测合并是否会产生冲突。
	/// 调用方通常传入 localBranch.Name 作为 upstream 上的目标分支名（fork 工作流约定同名）。
	/// </summary>
	public class CheckForkSyncStatusGitCommand
	{
		public GitCommandResult<ForkSyncStatus> Execute(
			GitModule gitModule,
			Remote upstreamRemote,
			LocalBranch localBranch,
			string upstreamBranchName,
			JobMonitor monitor)
		{
			if (upstreamRemote == null || localBranch == null || string.IsNullOrWhiteSpace(upstreamBranchName))
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.NoUpstreamBranch);
			}

			// Step 1: fetch upstream（仅更新 refs/remotes/upstream/*，不修改工作区）
			GitCommandResult fetchResult = new FetchGitCommand().Execute(
				gitModule, upstreamRemote, fetchAllRemotes: false, monitor, noPrompt: true);
			if (!fetchResult.Succeeded)
			{
				return GitCommandResult<ForkSyncStatus>.Failure(fetchResult.Error);
			}

			// Step 2: 解析 upstream/<branch> 的当前 sha
			string upstreamRef = "refs/remotes/" + upstreamRemote.Name + "/" + upstreamBranchName;
			GitRequestResult revParseResult = new GitRequest(gitModule)
				.Command("rev-parse", "--verify", upstreamRef)
				.Execute();
			if (!revParseResult.Success || string.IsNullOrWhiteSpace(revParseResult.Stdout))
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.NoUpstreamBranch);
			}
			string upstreamSha = revParseResult.Stdout.Trim();
			if (upstreamSha.Length != 40)
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.NoUpstreamBranch);
			}

			// Step 3: 计算 merge-base
			GitRequestResult mergeBaseResult = new GitRequest(gitModule)
				.Command("merge-base", localBranch.FullReference, upstreamRef)
				.Execute();
			if (!mergeBaseResult.Success || string.IsNullOrWhiteSpace(mergeBaseResult.Stdout))
			{
				// 无共同祖先，视为冲突（极端情况）
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.MustSyncWithConflict);
			}
			string mergeBaseSha = mergeBaseResult.Stdout.Trim();

			// Step 4: 若 upstream sha == merge-base，说明 upstream 没有本地尚未包含的提交，可直接 push
			if (string.Equals(mergeBaseSha, upstreamSha, System.StringComparison.OrdinalIgnoreCase))
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.SafeToPush);
			}

			// Step 5: upstream 有新提交，用 legacy 3-arg merge-tree 检测合并冲突
			// 用法：git merge-tree <base> <source> <destination>
			// 这里模拟"把 upstream 合并进 local"：base=merge-base, source=upstream, destination=local
			GitRequestResult mergeTreeResult = new GitRequest(gitModule)
				.Command("merge-tree", mergeBaseSha, upstreamRef, localBranch.FullReference)
				.Execute();
			if (!mergeTreeResult.Success)
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.Unknown);
			}
			string stdout = mergeTreeResult.Stdout;
			if (stdout.Contains("+>>>>>>>") || stdout.Contains("+<<<<<<<")
				|| stdout.Contains("->>>>>>>") || stdout.Contains("-<<<<<<<"))
			{
				return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.MustSyncWithConflict);
			}
			return GitCommandResult<ForkSyncStatus>.Success(ForkSyncStatus.ShouldSyncNoConflict);
		}
	}
}
