using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Undo;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// v3.3.0：抓取当前仓库的轻量 entry（HEAD sha + 当前分支名）。
	///
	/// 重构说明（v3.3.0）：
	/// - 旧版抓 11 字段（HEAD/branch/reflog/ORIG_HEAD/branches/tags/stashes/dirty/count），调用 7 次 git 进程
	/// - 新版只抓 2 字段（HEAD sha + 当前分支名），调用 2 次 git 进程
	/// - 性能提升 ~70%（大仓库尤其明显）
	/// - 重建分支/tag/stash 的能力不再需要：reflog 兜底 + 旧逻辑副作用大
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

			UndoEntry entry = new UndoEntry(headSha, currentBranchName, operationName, DateTime.UtcNow);
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
	}
}
