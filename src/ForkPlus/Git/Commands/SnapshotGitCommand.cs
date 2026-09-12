using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Undo;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 抓取当前仓库的轻量 entry（HEAD sha + 当前分支名 + 工作区 stash sha）。
	///
	/// v3.3.0：抓 2 字段（HEAD sha + 当前分支名），调用 2 次 git 进程。
	/// v3.4.0 Layer 2：增加 stash create 抓工作区快照，用于 undo discard/stage/unstage/删 branch。
	///   - 工作区干净时 stash create 返回空字符串，PreOperationStashSha = null（节省一次 stash apply）
	///   - 失败永不抛出，PreOperationStashSha = null（降级到 v3.3.0 行为）
	/// </summary>
	public class SnapshotGitCommand
	{
		/// <summary>抓取当前仓库状态 entry。失败时返回仅含 operationName 的 entry（永不抛出）。</summary>
		public GitCommandResult<UndoEntry> Execute(GitModule gitModule, string operationName = "")
		{
			if (gitModule == null)
			{
				return GitCommandResult<UndoEntry>.Success(new UndoEntry(null, null, operationName, DateTime.UtcNow));
			}

			string headSha = ReadRevParse(gitModule, "HEAD");
			string currentBranchName = ReadCurrentBranch(gitModule);
			string stashSha = ReadStashCreate(gitModule);

			UndoEntry entry = new UndoEntry(headSha, currentBranchName, operationName, DateTime.UtcNow, stashSha);
			return GitCommandResult<UndoEntry>.Success(entry);
		}

		private static string ReadRevParse(GitModule gitModule, string rev)
		{
			try
			{
				GitRequestResult r = new GitRequest(gitModule).Command("rev-parse", "--verify", rev + "^{commit}").Execute(silent: true);
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

		/// <summary>
		/// v3.4.0：用 git stash create --include-untracked 抓工作区快照。
		/// 返回 stash commit sha（40 字符），或 null（工作区干净 / 失败 / 空仓库）。
		/// stash create 不写入 stash list，只创建悬空 commit，对仓库无副作用。
		/// </summary>
		private static string ReadStashCreate(GitModule gitModule)
		{
			try
			{
				// 问题7（fsmonitor 系列第 7 处，版本号不变）：stash create 写入侧四件套对齐——与 status/diff/add/reset 同源
				// （见 ReliableGitFlags）。根因：repo 开启 core.fsmonitor 且 daemon 运行时，
				// git stash create 计算工作区快照 diff 前会先问 fsmonitor daemon"哪些文件脏了"；
				// daemon 的脏文件列表可能过期漏报（inotify 事件合并/守护进程重启窗口期/index
				// stat 缓存陈旧/时序竞争），stash 信了 → 该文件的变更被静默排除出快照 commit。
				// 快照是 Undo discard/stage/unstage/删分支 等破坏性操作的前置防线（PreOperationStashSha），
				// 快照缺了 → Undo 时 stash apply 恢复不回来那部分工作区变更，被永久丢弃（"丢失变更"）。
				// --include-untracked：捕获 untracked 文件（需 git >= 2.35，老版本回退到不带该选项）
				GitRequestResult r = new GitRequest(gitModule).Command(new GitCommand(ReliableGitFlags.Prefix, "stash", "create", "--include-untracked")).Execute(silent: true);
				if (!r.Success)
				{
					// 回退：不带 --include-untracked（仅捕获 tracked 文件变更 + index 状态）
					r = new GitRequest(gitModule).Command(new GitCommand(ReliableGitFlags.Prefix, "stash", "create")).Execute(silent: true);
					if (!r.Success)
					{
						return null;
					}
				}
				string s = r.Stdout?.Trim() ?? "";
				// 工作区干净时 stash create 输出空字符串
				return s.Length == 40 ? s : null;
			}
			catch
			{
				return null;
			}
		}
	}
}
