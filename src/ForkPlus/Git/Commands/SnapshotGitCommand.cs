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
			Dictionary<string, string> localBranches = ReadLocalBranches(gitModule);
			Dictionary<string, string> tags = ReadTags(gitModule);
			List<string> stashShas = ReadStashShas(gitModule);
			bool isDirty = ReadIsDirty(gitModule);
			int changedCount = ReadChangedFilesCount(gitModule);

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

		private static Dictionary<string, string> ReadLocalBranches(GitModule gitModule)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			try
			{
				// %(refname:short) -> 分支名；%(objectname) -> sha
				GitRequestResult r = new GitRequest(gitModule)
					.Command("for-each-ref", "--format=%(refname:short) %(objectname)", "refs/heads/")
					.Execute(silent: true);
				if (!r.Success)
				{
					return result;
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
					string name = line.Substring(0, space);
					string sha = line.Substring(space + 1).Trim();
					if (sha.Length == 40)
					{
						result[name] = sha;
					}
				}
			}
			catch
			{
				// 静默
			}
			return result;
		}

		private static Dictionary<string, string> ReadTags(GitModule gitModule)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			try
			{
				GitRequestResult r = new GitRequest(gitModule)
					.Command("for-each-ref", "--format=%(refname:short) %(objectname)", "refs/tags/")
					.Execute(silent: true);
				if (!r.Success)
				{
					return result;
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
					string name = line.Substring(0, space);
					string sha = line.Substring(space + 1).Trim();
					if (sha.Length == 40)
					{
						result[name] = sha;
					}
				}
			}
			catch
			{
				// 静默
			}
			return result;
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

		private static bool ReadIsDirty(GitModule gitModule)
		{
			try
			{
				// git status --porcelain 有输出即 dirty
				GitRequestResult r = new GitRequest(gitModule).Command("status", "--porcelain").Execute(silent: true);
				if (!r.Success)
				{
					return false;
				}
				return !string.IsNullOrWhiteSpace(r.Stdout);
			}
			catch
			{
				return false;
			}
		}

		private static int ReadChangedFilesCount(GitModule gitModule)
		{
			try
			{
				GitRequestResult r = new GitRequest(gitModule).Command("status", "--porcelain").Execute(silent: true);
				if (!r.Success)
				{
					return 0;
				}
				string stdout = r.Stdout ?? "";
				if (string.IsNullOrWhiteSpace(stdout))
				{
					return 0;
				}
				return stdout.Split(Consts.Chars.NewLine).Count(x => !string.IsNullOrWhiteSpace(x));
			}
			catch
			{
				return 0;
			}
		}
	}
}
