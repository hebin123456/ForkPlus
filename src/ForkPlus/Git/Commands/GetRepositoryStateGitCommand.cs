using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetRepositoryStateGitCommand
	{
		public GitCommandResult<RepositoryState> Execute(GitModule gitModule, ChangedFile[] changedFiles)
		{
			// 空仓库快速路径（v2.1.4 修复）：刚 git init 完毕的仓库没有任何 commit，
			// 不可能有 merge/rebase/cherry-pick/revert/sequencer/squash/bisect 等状态。
			// 直接返回 OK，跳过 GetReferencesGitCommand 调用——该命令直接调用 native
			// bt_get_references，在空仓库时行为不确定（可能返回错误导致整个 status 刷新
			// 失败，UI 看不到 untracked 文件；用户反馈"新建文件夹感知不到"即此问题）。
			// 检测方式同 RefreshRepositoryReferencesGitCommand.IsEmptyRepository。
			if (IsEmptyRepository(gitModule))
			{
				return GitCommandResult<RepositoryState>.Success(new RepositoryState.OK());
			}
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			GitCommandResult<ReferenceStorage> gitCommandResult2 = new GetReferencesGitCommand().Execute(gitModule, gitCommandResult.Result);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult2.Error);
			}
			ReferenceStorage result = gitCommandResult2.Result;
			List<ChangedFile> list = changedFiles.Filter((ChangedFile x) => x.IsUnmerged());
			if (File.Exists(Path.Combine(gitModule.GitDir(), "MERGE_HEAD")))
			{
				return GetMergeState(gitModule, result, list);
			}
			if (Directory.Exists(Path.Combine(gitModule.GitDir(), "rebase-apply")))
			{
				GitCommandResult<RepositoryState> rebaseState = GetRebaseState(gitModule, result, list);
				if (!IsFileNotFoundException(rebaseState))
				{
					return rebaseState;
				}
			}
			if (Directory.Exists(Path.Combine(gitModule.GitDir(), "rebase-merge")))
			{
				GitCommandResult<RepositoryState> interactiveRebaseState = GetInteractiveRebaseState(gitModule, result, list);
				if (!IsFileNotFoundException(interactiveRebaseState))
				{
					return interactiveRebaseState;
				}
			}
			if (File.Exists(Path.Combine(gitModule.GitDir(), "CHERRY_PICK_HEAD")))
			{
				return GetCherryPickState(result, list);
			}
			if (File.Exists(Path.Combine(gitModule.GitDir(), "REVERT_HEAD")))
			{
				return GetRevertState(result, list);
			}
			if (Directory.Exists(Path.Combine(gitModule.GitDir(), "sequencer")))
			{
				return GitCommandResult<RepositoryState>.Success(new RepositoryState.SequencerInProgress());
			}
			if (File.Exists(Path.Combine(gitModule.GitDir(), "SQUASH_MSG")))
			{
				return GetSquashState(result, list);
			}
			if (list.Count > 0)
			{
				GitCommandResult<Reference> gitCommandResult3 = ResolveRootRef("HEAD", result);
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<RepositoryState>.Failure(gitCommandResult3.Error);
				}
				return GitCommandResult<RepositoryState>.Success(new RepositoryState.UnmergedIndex(gitCommandResult3.Result, list.ToArray()));
			}
			if (File.Exists(Path.Combine(gitModule.GitDir(), "BISECT_START")))
			{
				return GetBisectState(gitModule, result);
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.OK());
		}

		private static bool IsFileNotFoundException(GitCommandResult<RepositoryState> result)
		{
			return (result.Error as GitCommandError.UnknownException)?.Exception is FileNotFoundException;
		}

		private static GitCommandResult<RepositoryState> GetRebaseState(GitModule gitModule, ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			string path = Path.Combine(gitModule.GitDir(), "rebase-apply");
			GitCommandResult<Reference> gitCommandResult = ResolveRootRef("ORIG_HEAD", referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			int num = ReadIntFromFile(Path.Combine(path, "next"));
			int total = ReadIntFromFile(Path.Combine(path, "last"));
			GitCommandResult<Reference> gitCommandResult2 = FindReferenceByShaFile(Path.Combine(path, "onto"), referenceStorage);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Success(new RepositoryState.AmInProgress(gitCommandResult.Result, num - 1, total, unmergedFiles.ToArray()));
			}
			GitCommandResult<Sha> gitCommandResult3 = ReadShaFromFile(Path.Combine(path, "original-commit"));
			if (!gitCommandResult3.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(new GitCommandError.NotFound("Cannot read active SHA in 'original-commit'"));
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.RebaseInProgress(gitCommandResult2.Result, gitCommandResult.Result, gitCommandResult3.Result, num - 1, total, unmergedFiles.ToArray(), interactive: false, null));
		}

		private static GitCommandResult<RepositoryState> GetInteractiveRebaseState(GitModule gitModule, ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			string path = Path.Combine(gitModule.GitDir(), "rebase-merge");
			GitCommandResult<Reference> gitCommandResult = FindReferenceByShaFile(Path.Combine(path, "orig-head"), referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			GitCommandResult<Reference> gitCommandResult2 = FindReferenceByShaFile(Path.Combine(path, "onto"), referenceStorage);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult2.Error);
			}
			int rebaseTodoListCount = GetRebaseTodoListCount(Path.Combine(path, "git-rebase-todo"));
			string[] rebaseTodoList = GetRebaseTodoList(Path.Combine(path, "done"));
			string text = ReadStringFromFile(Path.Combine(path, "amend"))?.TrimEnd();
			bool flag = File.Exists(Path.Combine(path, "current-fixups"));
			int num = rebaseTodoList.Length;
			Sha? activeSha = null;
			string text2 = rebaseTodoList.LastItem();
			if (text2 != null && Sha.TryParse(text2, out var result))
			{
				activeSha = result;
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.RebaseInProgress(gitCommandResult2.Result, gitCommandResult.Result, activeSha, num - 1, num + rebaseTodoListCount, unmergedFiles.ToArray(), interactive: true, (!flag) ? text : null));
		}

		private static int GetRebaseTodoListCount(string todoListPath)
		{
			int result = 1;
			if (!File.Exists(todoListPath))
			{
				return result;
			}
			try
			{
				int num = 0;
				string[] array = File.ReadAllLines(todoListPath);
				foreach (string text in array)
				{
					if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("^"))
					{
						num++;
					}
				}
				return num;
			}
			catch (Exception arg)
			{
				Log.Warn($"Cannot get read rebase todo list count: '{arg}'");
				return result;
			}
		}

		private static string[] GetRebaseTodoList(string todoListPath)
		{
			if (!File.Exists(todoListPath))
			{
				return new string[0];
			}
			try
			{
				string[] array = File.ReadAllLines(todoListPath);
				List<string> list = new List<string>(array.Length);
				string[] array2 = array;
				foreach (string text in array2)
				{
					if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("#") && text.Length >= 45)
					{
						int num = text.IndexOf(" ");
						string item = text.Substring(num + 1, 40);
						list.Add(item);
					}
				}
				return list.ToArray();
			}
			catch (Exception arg)
			{
				Log.Warn($"Cannot get read rebase todo list count: '{arg}'");
				return new string[0];
			}
		}

		private static GitCommandResult<Sha> ReadShaFromFile(string filePath)
		{
			string text;
			try
			{
				text = File.ReadAllText(filePath).Trim();
			}
			catch (Exception ex)
			{
				Log.Warn($"Cannot read SHA from '{filePath}':\n{ex}");
				return GitCommandResult<Sha>.Failure(ex);
			}
			if (!Sha.TryParse(text, out var result))
			{
				return GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Cannot parse SHA in '" + text + "'"));
			}
			return GitCommandResult<Sha>.Success(result);
		}

		private static GitCommandResult<Reference> FindReferenceByShaFile(string filePath, ReferenceStorage referenceStorage)
		{
			string text;
			try
			{
				text = File.ReadAllText(filePath).Trim();
			}
			catch (Exception ex)
			{
				Log.Warn($"Cannot read HEAD reference from file: '{ex}'");
				return GitCommandResult<Reference>.Failure(ex);
			}
			if (!Sha.TryParse(text, out var result))
			{
				return GitCommandResult<Reference>.Failure(new GitCommandError.NotFound("Failed to parse SHA in '" + text + "'"));
			}
			for (int i = 0; i < referenceStorage.Shas.Length; i++)
			{
				if (referenceStorage.Shas[i] != result)
				{
					continue;
				}
				string text2 = referenceStorage.Refs[i];
				if (text2.StartsWith("refs/"))
				{
					string text3 = ReferenceShortName(text2);
					if (!text3.StartsWith("backup/"))
					{
						DateTime committerDate = referenceStorage.GetCommitterDate(i) ?? DateTime.Now;
						return GitCommandResult<Reference>.Success(new Reference(result, text2, text3, committerDate));
					}
				}
			}
			return GitCommandResult<Reference>.Success(new Reference(result, "", result.ToAbbreviatedString(), DateTime.Now));
		}

		private static GitCommandResult<Reference> ResolveRootRef(string rev, ReferenceStorage referenceStorage)
		{
			for (int i = 0; i < referenceStorage.Symrefs.Length; i++)
			{
				if (referenceStorage.Symrefs[i] == rev)
				{
					string text = referenceStorage.SymrefTargets[i];
					int num = Array.IndexOf(referenceStorage.Refs, text);
					if (num >= 0)
					{
						Sha sha = referenceStorage.Shas[num];
						string name = ReferenceShortName(text);
						DateTime committerDate = referenceStorage.GetCommitterDate(num) ?? DateTime.Now;
						return GitCommandResult<Reference>.Success(new Reference(sha, text, name, committerDate));
					}
				}
			}
			int num2 = Array.IndexOf(referenceStorage.Refs, rev);
			if (num2 >= 0)
			{
				return GitCommandResult<Reference>.Success(FindNamedReference(referenceStorage.Shas[num2], referenceStorage));
			}
			return GitCommandResult<Reference>.Failure(new GitCommandError.ParseError("'" + rev + "' not found in references"));
		}

		private static Reference FindNamedReference(Sha targetSha, ReferenceStorage referenceStorage)
		{
			for (int i = 0; i < referenceStorage.Shas.Length; i++)
			{
				if (referenceStorage.Shas[i] == targetSha)
				{
					string text = referenceStorage.Refs[i];
					if (text.StartsWith("refs/"))
					{
						string name = ReferenceShortName(text);
						DateTime committerDate = referenceStorage.GetCommitterDate(i) ?? DateTime.Now;
						return new Reference(targetSha, text, name, committerDate);
					}
				}
			}
			return new Reference(targetSha, "", targetSha.ToAbbreviatedString(), DateTime.Now);
		}

		private static string ReferenceShortName(string fullReference)
		{
			string[] array = new string[3] { "refs/heads/", "refs/remotes/", "refs/tags/" };
			foreach (string text in array)
			{
				if (fullReference.StartsWith(text))
				{
					return fullReference.Substring(text.Length);
				}
			}
			return fullReference;
		}

		private static string ReadStringFromFile(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					return null;
				}
				return File.ReadAllText(path);
			}
			catch (Exception arg)
			{
				Log.Warn($"Cannot read string from file: '{arg}'");
				return null;
			}
		}

		private static int ReadIntFromFile(string path)
		{
			string text = ReadStringFromFile(path);
			if (text == null)
			{
				return 0;
			}
			if (int.TryParse(text, out var result))
			{
				return result;
			}
			return 0;
		}

		private static Sha? ReadShaFromOptionalFile(string filePath)
		{
			string text = ReadStringFromFile(filePath)?.TrimEnd();
			if (text == null)
			{
				return null;
			}
			if (Sha.TryParse(text, out var result))
			{
				return result;
			}
			Log.Error("Cannot parse SHA in '" + text + "'");
			return null;
		}

		private static GitCommandResult<RepositoryState> GetMergeState(GitModule gitModule, ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			GitCommandResult<Reference> gitCommandResult = FindReferenceByShaFile(Path.Combine(gitModule.GitDir(), "MERGE_HEAD"), referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			Reference result = gitCommandResult.Result;
			GitCommandResult<Reference> gitCommandResult2 = ResolveRootRef("HEAD", referenceStorage);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult2.Error);
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.MergeInProgress(gitCommandResult2.Result, result, unmergedFiles.ToArray()));
		}

		private static GitCommandResult<RepositoryState> GetCherryPickState(ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			GitCommandResult<Reference> gitCommandResult = ResolveRootRef("HEAD", referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			GitCommandResult<Reference> gitCommandResult2 = ResolveRootRef("CHERRY_PICK_HEAD", referenceStorage);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult2.Error);
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.CherryPickInProgress(gitCommandResult.Result, gitCommandResult2.Result, unmergedFiles.ToArray()));
		}

		private static GitCommandResult<RepositoryState> GetRevertState(ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			GitCommandResult<Reference> gitCommandResult = ResolveRootRef("REVERT_HEAD", referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.RevertInProgress(gitCommandResult.Result.Sha, unmergedFiles.ToArray()));
		}

		private static GitCommandResult<RepositoryState> GetSquashState(ReferenceStorage referenceStorage, IReadOnlyList<ChangedFile> unmergedFiles)
		{
			GitCommandResult<Reference> gitCommandResult = ResolveRootRef("HEAD", referenceStorage);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryState>.Failure(gitCommandResult.Error);
			}
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.SquashInProgress(gitCommandResult.Result, unmergedFiles.ToArray()));
		}

		private static GitCommandResult<RepositoryState> GetBisectState(GitModule gitModule, ReferenceStorage referenceStorage)
		{
			string text;
			try
			{
				text = File.ReadAllText(Path.Combine(gitModule.GitDir(), "BISECT_START")).Trim();
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
				return GitCommandResult<RepositoryState>.Failure(new GitCommandError.UnknownException(ex));
			}
			Reference start;
			if (Sha.TryParse(text, out var result))
			{
				start = FindNamedReference(result, referenceStorage);
			}
			else
			{
				string text2 = "refs/heads/" + text;
				int num = Array.IndexOf(referenceStorage.Refs, text2);
				if (num < 0)
				{
					return GitCommandResult<RepositoryState>.Failure(new GitCommandError.ParseError("Cannot resolve reference '" + text + "'"));
				}
				DateTime committerDate = referenceStorage.GetCommitterDate(num) ?? DateTime.Now;
				start = new Reference(referenceStorage.Shas[num], text2, text, committerDate);
			}
			Sha? currentSha = ReadShaFromOptionalFile(Path.Combine(gitModule.GitDir(), "BISECT_EXPECTED_REV"));
			return GitCommandResult<RepositoryState>.Success(new RepositoryState.BisectInProgress(start, currentSha));
		}

		/// <summary>检测仓库是否为空（git init 完毕，没有任何 commit）。
		/// 与 RefreshRepositoryReferencesGitCommand.IsEmptyRepository 相同的实现：
		/// git rev-parse --verify HEAD 失败即说明空仓库。</summary>
		private static bool IsEmptyRepository(GitModule gitModule)
		{
			try
			{
				GitRequestResult result = new GitRequest(gitModule)
					.Command("rev-parse", "--verify", "HEAD")
					.Execute(silent: true);
				return !result.Success;
			}
			catch (Exception ex)
			{
				Log.Warn("IsEmptyRepository check failed: " + ex.Message);
				return false;
			}
		}
	}
}
