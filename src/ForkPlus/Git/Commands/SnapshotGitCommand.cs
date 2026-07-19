using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 采集当前仓库状态快照，用于 Undo/Redo。
	/// 设计原则：抓快照本身不能阻塞主操作，任何异常都吞掉返回部分快照。
	/// 采集内容：HEAD sha、当前分支名、HEAD reflog 前 50 条、ORIG_HEAD。
	/// P2 阶段会扩展采集 LocalBranches / Tags / StashShas。
	/// v3.0.2 性能优化：合并冗余 git 进程调用（status --porcelain 2次→1次，for-each-ref 2次→1次）。
	/// </summary>
	public class SnapshotGitCommand
	{
		private const int ReflogDepth = 50;

		public GitCommandResult<RepositorySnapshot> Execute(GitModule gitModule, string operationName = "")
		{
			if (gitModule == null)
			{
				return GitCommandResult<RepositorySnapshot>.Success(new RepositorySnapshot(
					operationName, DateTime.UtcNow, null, null, new string[0], null));
			}

			string headSha = ReadRevParse(gitModule, "HEAD");
			string currentBranchName = ReadCurrentBranch(gitModule);
			string[] headReflog = ReadHeadReflog(gitModule);
			string origHead = ReadRevParse(gitModule, "ORIG_HEAD");
			// v3.0.2：合并 branches + tags 到一次 for-each-ref
			Dictionary<string, string> localBranches;
			Dictionary<string, string> tags;
			ReadBranchesAndTags(gitModule, out localBranches, out tags);
			List<string> stashShas = ReadStashShas(gitModule);
			// v3.0.2：合并 isDirty + changedCount 到一次 status --porcelain
			bool isDirty;
			int changedCount;
			ReadDirtyAndCount(gitModule, out isDirty, out changedCount);

			RepositorySnapshot snapshot = new RepositorySnapshot(
				operationName,
				DateTime.UtcNow,
				headSha,
				currentBranchName,
				headReflog,
				origHead,
				localBranches,
				tags,
				stashShas,
				isDirty,
				changedCount);
			return GitCommandResult<RepositorySnapshot>.Success(snapshot);
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
				// --show-current 在 detached HEAD 时返回空字符串
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

		private static string[] ReadHeadReflog(GitModule gitModule)
		{
			try
			{
				GitRequestResult r = new GitRequest(gitModule)
					.Command("reflog", "HEAD", "--pretty=format:%H", $"--max-count={ReflogDepth}")
					.Execute(silent: true);
				if (!r.Success)
				{
					return new string[0];
				}
				return r.Stdout?.Split(Consts.Chars.NewLine).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? new string[0];
			}
			catch
			{
				return new string[0];
			}
		}

		/// <summary>
		/// v3.0.2：一次 for-each-ref 同时读取本地分支和 tag。
		/// 用 %(refname) 完整路径再按前缀分发，避免 short 形式无法区分 branch/tag。
		/// </summary>
		private static void ReadBranchesAndTags(GitModule gitModule, out Dictionary<string, string> branches, out Dictionary<string, string> tags)
		{
			branches = new Dictionary<string, string>();
			tags = new Dictionary<string, string>();
			try
			{
				GitRequestResult r = new GitRequest(gitModule)
					.Command("for-each-ref", "--format=%(refname) %(objectname)", "refs/heads/", "refs/tags/")
					.Execute(silent: true);
				if (!r.Success)
				{
					return;
				}
				foreach (string line in (r.Stdout ?? "").Split(Consts.Chars.NewLine))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					int space = line.IndexOf(' ');
					if (space <= 0 || space >= line.Length - 1)
					{
						continue;
					}
					string refname = line.Substring(0, space);
					string sha = line.Substring(space + 1).Trim();
					if (sha.Length != 40)
					{
						continue;
					}
					if (refname.StartsWith("refs/heads/"))
					{
						branches[refname.Substring("refs/heads/".Length)] = sha;
					}
					else if (refname.StartsWith("refs/tags/"))
					{
						tags[refname.Substring("refs/tags/".Length)] = sha;
					}
				}
			}
			catch
			{
				// 静默
			}
		}

		private static List<string> ReadStashShas(GitModule gitModule)
		{
			List<string> result = new List<string>();
			try
			{
				// refs/stash 不存在时 rev-parse 会失败，是正常情况
				GitRequestResult r = new GitRequest(gitModule)
					.Command("reflog", "--format=%H", "refs/stash")
					.Execute(silent: true);
				if (!r.Success)
				{
					return result;
				}
				foreach (string line in (r.Stdout ?? "").Split(Consts.Chars.NewLine))
				{
					string s = line.Trim();
					if (s.Length == 40)
					{
						result.Add(s);
					}
				}
			}
			catch
			{
				// 静默
			}
			return result;
		}

		/// <summary>v3.0.2：一次 status --porcelain 同时拿 isDirty 和 changedCount。</summary>
		private static void ReadDirtyAndCount(GitModule gitModule, out bool isDirty, out int changedCount)
		{
			isDirty = false;
			changedCount = 0;
			try
			{
				GitRequestResult r = new GitRequest(gitModule).Command("status", "--porcelain").Execute(silent: true);
				if (!r.Success)
				{
					return;
				}
				string stdout = r.Stdout ?? "";
				if (string.IsNullOrWhiteSpace(stdout))
				{
					return;
				}
				isDirty = true;
				changedCount = stdout.Split(Consts.Chars.NewLine).Count(x => !string.IsNullOrWhiteSpace(x));
			}
			catch
			{
				// 静默
			}
		}
	}
}
