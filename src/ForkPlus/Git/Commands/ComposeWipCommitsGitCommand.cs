using System.Collections.Generic;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	/// <summary>AI Commit Composer (WIP拆分)：按 AI 生成的分组方案，逐组 stage + commit。
	/// 执行流程：
	/// 1. 用 <c>git reset HEAD --</c> 清空当前 staging area（空仓库时容错忽略 ambiguous HEAD 错误）。
	/// 2. 对每个分组：stage 该组文件 → commit 该组 message。
	/// 3. 任何分组失败立即中止并返回错误（已提交的分组不会回滚——这与手动 commit 行为一致）。
	/// 4. 取消信号在每次分组开始前检查；StageFileGitCommand / CommitGitCommand 内部也会响应取消。
	/// 注意：StageFileGitCommand 内部会调 monitor.Success，本命令在 stage 之后显式 Update 回 InProgress，
	/// 保证进度条 / 状态栏在执行多个分组期间不会"提前结束"。</summary>
	public class ComposeWipCommitsGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, WipCommitPlan plan, bool commitAndPush, JobMonitor monitor)
		{
			if (plan?.Groups == null || plan.Groups.Count == 0)
			{
				return GitCommandResult<string[]>.Failure(new GitCommandError.GitError("Empty WIP commit plan", ""));
			}

			// 1. 清空 staging area（用 git reset HEAD -- 不通过 UnstageGitCommand，
			//    避免子命令污染 monitor 的 Success/Fail 终态）
			monitor.Update(0.0, PreferencesLocalization.Current("Unstaging all files..."));
			GitRequestResult resetResult = new GitRequest(gitModule).Command(new GitCommand("reset", "HEAD", "--")).Execute();
			if (!resetResult.Success && !IsAmbiguousHeadError(resetResult.Stderr))
			{
				monitor.Fail(PreferencesLocalization.Current("unstage failed"));
				return GitCommandResult<string[]>.Failure(new GitCommandError.GitError(resetResult.Stdout, resetResult.Stderr));
			}

			List<string> committedSubjects = new List<string>();
			int totalGroups = plan.Groups.Count;
			int stageGroups = 0;
			for (int i = 0; i < totalGroups; i++)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult<string[]>.Failure(new GitCommandError.Cancelled());
				}
				WipCommitGroup group = plan.Groups[i];
				if (group.MatchedFiles.Count == 0)
				{
					monitor.AppendOutputLine(PreferencesLocalization.FormatCurrent("Skipping group {0}/{1}: no matched files", i + 1, totalGroups));
					continue;
				}

				// 阶段进度：每组占用 [0, 1] 区间的 1/totalGroups；组内 stage 占前半、commit 占后半
				double groupStart = (double)i / totalGroups;
				double groupMid = (i + 0.5) / totalGroups;
				double groupEnd = (double)(i + 1) / totalGroups;

				monitor.Update(groupStart, PreferencesLocalization.FormatCurrent("Composing commit {0}/{1}: {2}", i + 1, totalGroups, group.Subject));

				// 2a. Stage this group's files
				GitCommandResult stageResult = new StageFileGitCommand().Execute(gitModule, group.MatchedFiles.ToArray(), monitor);
				if (!stageResult.Succeeded)
				{
					return GitCommandResult<string[]>.Failure(stageResult.Error);
				}
				// StageFileGitCommand 调用了 monitor.Success，重置为 InProgress 让进度条继续走
				monitor.Update(groupMid, PreferencesLocalization.FormatCurrent("Composing commit {0}/{1}: {2}", i + 1, totalGroups, group.Subject));

				// 2b. Commit with this group's message
				string message = group.BuildFullMessage();
				GitCommandResult commitResult = new CommitGitCommand().Execute(gitModule, message, amend: false, commitAndPush: commitAndPush, monitor: monitor);
				if (!commitResult.Succeeded)
				{
					return GitCommandResult<string[]>.Failure(commitResult.Error);
				}
				committedSubjects.Add(group.Subject);
				stageGroups++;
				monitor.Update(groupEnd, PreferencesLocalization.FormatCurrent("Composed commit {0}/{1}", stageGroups, totalGroups));
			}

			monitor.Success(PreferencesLocalization.FormatCurrent("Composed {0} commits", committedSubjects.Count));
			return GitCommandResult<string[]>.Success(committedSubjects.ToArray());
		}

		/// <summary>空仓库（无 HEAD）执行 <c>git reset HEAD --</c> 会报 <c>ambiguous argument 'HEAD'</c>。
		/// 这种情况下 staged 文件都是新文件（Added），不需要 unstage，直接 stage + commit 即可。</summary>
		private static bool IsAmbiguousHeadError(string stderr)
		{
			return stderr != null && stderr.Contains("ambiguous argument 'HEAD'");
		}
	}
}
