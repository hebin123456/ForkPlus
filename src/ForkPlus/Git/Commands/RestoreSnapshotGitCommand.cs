using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Undo;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// v3.3.0：把仓库恢复到指定 UndoEntry 描述的状态。
	///
	/// 重构说明（v3.3.0）：
	/// - 旧版用 5 步组合命令（checkout + reset --hard + 重建分支 + 重建 tag + 重建 stash）
	/// - 新版简化为 2 步：checkout 切回原分支 + reset --hard 到目标 sha
	/// - 原因：HEAD sha 是恢复真相源，所有 ref 状态都跟着 sha 走
	///   - 重建分支/tag/stash 不再需要：CLI 操作和 UI 操作都通过 reflog 兜底恢复
	///   - 旧版重建分支/tag/stash 的逻辑反而可能产生副作用（比如重建已被用户故意删除的分支）
	///
	/// v3.4.0 Layer 2 扩展：
	/// - 新增第 3 步：如果有 PreOperationStashSha，用 git stash apply --index 恢复工作区 + index 状态
	/// - 用于 undo discard/stage/unstage/delete branch 等工作区级操作
	/// - HEAD 移动类操作 stash sha 为 null，跳过此步，行为与 v3.3.0 一致
	/// - stash apply 失败不阻断（HEAD 已恢复，工作区可能部分冲突，让用户手动解决）
	///
	/// 设计原则：恢复失败立即返回，不继续后续步骤。
	/// 工作区 dirty 检测由调用方在 UI 层弹窗确认，本命令不做拦截。
	/// </summary>
	public class RestoreSnapshotGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, UndoEntry target, JobMonitor monitor)
		{
			if (gitModule == null || target == null)
			{
				return GitCommandResult.Failure(new GitCommandError.GitError("target or gitModule is null", ""));
			}

			// 1. 切回原分支（如果不同）。避免 reset --hard 后进入 detached HEAD。
			if (!string.IsNullOrEmpty(target.CurrentBranchName))
			{
				string currentBranch = ReadCurrentBranch(gitModule);
				if (currentBranch != target.CurrentBranchName)
				{
					monitor?.Update(0.0, ServiceLocator.Localization.FormatCurrent("Checking out '{0}'...", target.CurrentBranchName));
					GitCommand checkoutCmd = new GitCommand(App.OverrideCredentialHelper, "checkout", target.CurrentBranchName);
					monitor?.Append(null, checkoutCmd);
					GitRequestResult checkoutResult = new GitRequest(gitModule).Command(checkoutCmd).Execute(monitor);
					if (monitor != null && monitor.IsCanceled)
					{
						return GitCommandResult.Failure(new GitCommandError.Cancelled());
					}
					if (!checkoutResult.Success)
					{
						return GitCommandResult.Failure(checkoutResult.ToGitCommandError());
					}
				}
			}

			// 2. reset --hard 到目标 sha（让当前分支指针移动到目标 commit）
			if (!string.IsNullOrEmpty(target.HeadSha))
			{
				string currentHead = ReadHead(gitModule);
				if (currentHead != target.HeadSha)
				{
					monitor?.Update(0.5, ServiceLocator.Localization.FormatCurrent("Resetting HEAD to {0}...", target.HeadSha.Substring(0, 7)));
					GitCommand resetCmd = new GitCommand(App.OverrideCredentialHelperBt, "reset", "--hard", target.HeadSha);
					monitor?.Append(null, resetCmd);
					ProcessOutputHandler handler = new ProcessOutputHandler(monitor);
					ExecuteWithCallbackResponse resp = new GitRequest(gitModule).Command(resetCmd).ExecuteWithCallbackBt(handler.StdoutHandler, handler.StderrHandler, monitor);
					if (monitor != null && monitor.IsCanceled)
					{
						return GitCommandResult.Failure(new GitCommandError.Cancelled());
					}
					ISpawnError error = resp.Error;
					if (error != null)
					{
						return GitCommandResult.Failure(error.ToGitCommandError());
					}
					if (!resp.Result.Success)
					{
						return GitCommandResult.Failure(new GitCommandError.GitError(handler.FullOutput(), handler.Stderr()));
					}
				}
			}

			// 3. v3.4.0 Layer 2：恢复工作区 + index 状态（如果有 stash 快照）
			// 用于 undo discard/stage/unstage/delete branch 等工作区级操作
			if (!string.IsNullOrEmpty(target.PreOperationStashSha))
			{
				monitor?.Update(0.8, ServiceLocator.Localization.FormatCurrent("Restoring working tree..."));
				GitCommand stashApplyCmd = new GitCommand(App.OverrideCredentialHelper, "stash", "apply", "--index", target.PreOperationStashSha);
				monitor?.Append(null, stashApplyCmd);
				GitRequestResult stashResult = new GitRequest(gitModule).Command(stashApplyCmd).Execute(monitor);
				if (monitor != null && monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				// stash apply 失败不阻断恢复（HEAD 已恢复，工作区冲突让用户手动解决）
				// 仅记录到 monitor 日志，返回成功
				if (!stashResult.Success)
				{
					monitor?.Append(ServiceLocator.Localization.FormatCurrent("Working tree restore skipped: {0}", stashResult.Stderr?.Trim() ?? "unknown error"), null);
				}
			}

			monitor?.Success(ServiceLocator.Localization.Current("snapshot restored"));
			return GitCommandResult.Success();
		}

		private static string ReadCurrentBranch(GitModule gitModule)
		{
			try
			{
				GitRequestResult r = new GitRequest(gitModule).Command("symbolic-ref", "--short", "-q", "HEAD").Execute(silent: true);
				if (!r.Success)
				{
					return null;
				}
				return r.Stdout?.Trim() ?? "";
			}
			catch
			{
				return null;
			}
		}

		private static string ReadHead(GitModule gitModule)
		{
			try
			{
				GitRequestResult r = new GitRequest(gitModule).Command("rev-parse", "HEAD").Execute(silent: true);
				if (!r.Success)
				{
					return null;
				}
				string s = r.Stdout?.Trim() ?? "";
				return s.Length == 40 ? s : null;
			}
			catch
			{
				return null;
			}
		}
	}
}
