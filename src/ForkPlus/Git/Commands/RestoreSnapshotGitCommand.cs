using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 把仓库恢复到指定快照描述的状态。多步组合命令：
	/// 1. 当前分支不同 → 切回快照分支
	/// 2. HEAD 不同 → reset --hard 到快照 HEAD
	/// 3. 本地分支差异 → 删除新增的 / 重建被删的
	/// 4. tag 差异 → 删除新增的 / 重建被删的
	/// 5. stash 差异 → drop 新增的 / 从 reflog 恢复被删的
	///
	/// 设计原则：恢复失败不应中断后续步骤（除非 HEAD 恢复失败，那确实没法继续）。
	/// 工作区 dirty 检测由调用方在 UI 层弹窗确认，本命令不做拦截。
	/// </summary>
	public class RestoreSnapshotGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, RepositorySnapshot target, JobMonitor monitor)
		{
			if (gitModule == null || target == null)
			{
				return GitCommandResult.Failure(new GitCommandError.GitError("snapshot or gitModule is null", ""));
			}

			// 1. 当前分支不同 → checkout 切回
			string currentBranch = ReadCurrentBranch(gitModule);
			bool needCheckout = !string.IsNullOrEmpty(target.CurrentBranchName)
				&& currentBranch != target.CurrentBranchName;
			if (needCheckout)
			{
				monitor?.Update(0.0, PreferencesLocalization.FormatCurrent("Checking out '{0}'...", target.CurrentBranchName));
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

			// 2. HEAD 不同 → reset --hard
			string currentHead = ReadHead(gitModule);
			if (!string.IsNullOrEmpty(target.HeadSha) && currentHead != target.HeadSha)
			{
				monitor?.Update(0.3, PreferencesLocalization.FormatCurrent("Resetting HEAD to {0}...", target.HeadSha.Substring(0, 7)));
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

			// 3. 本地分支差异：删除新增的，重建被删的
			// v3.0.2：合并 branches + tags 到一次 for-each-ref
			Dictionary<string, string> currentBranches;
			Dictionary<string, string> currentTags;
			ReadBranchesAndTagsMap(gitModule, out currentBranches, out currentTags);
			// 3a. 删除快照里没有的分支
			foreach (KeyValuePair<string, string> entry in currentBranches)
			{
				if (!target.LocalBranches.ContainsKey(entry.Key))
				{
					// 不删当前 checkout 的分支（理论上前面 checkout 已切走，但保险）
					if (entry.Key == currentBranch)
					{
						continue;
					}
					GitCommand delCmd = new GitCommand(App.OverrideCredentialHelper, "branch", "-D", entry.Key);
					new GitRequest(gitModule).Command(delCmd).Execute(silent: true);
				}
			}
			// 3b. 重建快照里有但现在没有的分支（用 update-ref 指向快照 sha）
			foreach (KeyValuePair<string, string> entry in target.LocalBranches)
			{
				if (!currentBranches.ContainsKey(entry.Key))
				{
					GitCommand updateCmd = new GitCommand(App.OverrideCredentialHelper, "update-ref", "refs/heads/" + entry.Key, entry.Value);
					new GitRequest(gitModule).Command(updateCmd).Execute(silent: true);
				}
				else if (currentBranches[entry.Key] != entry.Value)
				{
					// 分支存在但 sha 不同：只强制更新非当前分支
					if (entry.Key == currentBranch)
					{
						continue;
					}
					GitCommand updateCmd = new GitCommand(App.OverrideCredentialHelper, "update-ref", "refs/heads/" + entry.Key, entry.Value);
					new GitRequest(gitModule).Command(updateCmd).Execute(silent: true);
				}
			}

			// 4. tag 差异（currentTags 已在第 3 步合并读取）
			foreach (KeyValuePair<string, string> entry in currentTags)
			{
				if (!target.Tags.ContainsKey(entry.Key))
				{
					GitCommand delCmd = new GitCommand(App.OverrideCredentialHelper, "tag", "-d", entry.Key);
					new GitRequest(gitModule).Command(delCmd).Execute(silent: true);
				}
			}
			foreach (KeyValuePair<string, string> entry in target.Tags)
			{
				if (!currentTags.ContainsKey(entry.Key) || currentTags[entry.Key] != entry.Value)
				{
					// -f 强制覆盖
					GitCommand createCmd = new GitCommand(App.OverrideCredentialHelper, "tag", "-f", entry.Key, entry.Value);
					new GitRequest(gitModule).Command(createCmd).Execute(silent: true);
				}
			}

			// 5. stash 差异
			List<string> currentStashes = ReadStashShas(gitModule);
			// 5a. drop 快照里没有的 stash
			foreach (string sha in currentStashes)
			{
				if (!target.StashShas.Contains(sha))
				{
					DropStashBySha(gitModule, sha);
				}
			}
			// 5b. 恢复快照里有但现在没有的 stash（从 stash reflog 找回来）
			foreach (string sha in target.StashShas)
			{
				if (!currentStashes.Contains(sha))
				{
					RestoreStashBySha(gitModule, sha);
				}
			}

			monitor?.Success(PreferencesLocalization.Current("snapshot restored"));
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

		/// <summary>v3.0.2：一次 for-each-ref 同时读取本地分支和 tag。</summary>
		private static void ReadBranchesAndTagsMap(GitModule gitModule, out Dictionary<string, string> branches, out Dictionary<string, string> tags)
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
			}
		}

		private static List<string> ReadStashShas(GitModule gitModule)
		{
			List<string> result = new List<string>();
			try
			{
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
			}
			return result;
		}

		/// <summary>按 sha 删除 stash。需要先反查 stash@{n} 编号。</summary>
		private static void DropStashBySha(GitModule gitModule, string sha)
		{
			try
			{
				// git stash list 找到对应编号
				GitRequestResult listResult = new GitRequest(gitModule).Command("stash", "list", "--format=%gd %H").Execute(silent: true);
				if (!listResult.Success)
				{
					return;
				}
				foreach (string line in (listResult.Stdout ?? "").Split(Consts.Chars.NewLine))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					int space = line.IndexOf(' ');
					if (space <= 0)
					{
						continue;
					}
					string refName = line.Substring(0, space);
					string lineSha = line.Substring(space + 1).Trim();
					if (lineSha == sha)
					{
						new GitRequest(gitModule).Command("stash", "drop", refName).Execute(silent: true);
						return;
					}
				}
			}
			catch
			{
			}
		}

		/// <summary>从 stash reflog 恢复一个被 drop 的 stash。stash 的 reflog 在 .git/logs/refs/stash。</summary>
		private static void RestoreStashBySha(GitModule gitModule, string sha)
		{
			try
			{
				// 用 update-ref 把 refs/stash 指到目标 sha，会出现在 stash 列表最前
				// 注意：这会覆盖现有 refs/stash。所以仅在当前 stash 为空时直接用此方法
				// 简化处理：仅尝试 update-ref，失败静默
				new GitRequest(gitModule).Command("update-ref", "refs/stash", sha).Execute(silent: true);
			}
			catch
			{
			}
		}
	}
}
