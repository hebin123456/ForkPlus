using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	public class GetWorktreesGitCommand
	{
		public GitCommandResult<RepositoryWorktrees> Execute(GitModule gitModule)
		{
			using (new Benchmarker("GetWorktreesGitCommand"))
			{
				return ExecuteInternal(gitModule);
			}
		}

		private GitCommandResult<RepositoryWorktrees> ExecuteInternal(GitModule gitModule)
		{
			string text = gitModule.WorktreesDirectoryPath();
			if (!SafeDirectoryExists(text))
			{
				return GitCommandResult<RepositoryWorktrees>.Success(RepositoryWorktrees.Empty);
			}
			string commonGitDir = gitModule.CommonGitDir;
			if (!SafeDirectoryExists(commonGitDir))
			{
				return GitCommandResult<RepositoryWorktrees>.Success(RepositoryWorktrees.Empty);
			}
			string[] array = new string[0];
			try
			{
				array = Directory.GetDirectories(text);
			}
			catch (Exception ex)
			{
				Log.Warn($"Can't open worktrees directory at '{text}': '{ex}'");
				return GitCommandResult<RepositoryWorktrees>.Success(RepositoryWorktrees.Empty);
			}
			if (array.Length == 0)
			{
				return GitCommandResult<RepositoryWorktrees>.Success(RepositoryWorktrees.Empty);
			}
			Worktree? mainWorktree = null;
			if (!IsBareRepository(PathHelper.Combine(commonGitDir, "config")))
			{
				string text2 = ((Path.GetFileName(commonGitDir) == ".git") ? Path.GetDirectoryName(commonGitDir) : commonGitDir);
				string head = GetHead(commonGitDir);
				if (head != null && SafeDirectoryExists(text2))
				{
					mainWorktree = new Worktree(text2, head, isMain: true, gitModule.Path == text2);
				}
			}
			List<Worktree> list = new List<Worktree>(array.Length);
			string[] array2 = array;
			foreach (string text3 in array2)
			{
				string head2 = GetHead(text3);
				if (head2 == null)
				{
					continue;
				}
				string text4 = ReadWorktreeGitdirFile(text3);
				if (text4 != null)
				{
					string directoryName = Path.GetDirectoryName(text4);
					if (SafeDirectoryExists(directoryName))
					{
						list.Add(new Worktree(directoryName, head2, isMain: false, gitModule.Path == directoryName));
					}
				}
			}
			list.Sort((Worktree x, Worktree y) => x.FriendlyName.CompareTo(y.FriendlyName));
			return GitCommandResult<RepositoryWorktrees>.Success(new RepositoryWorktrees(mainWorktree, list.ToArray()));
		}

		[Null]
		private static string ReadWorktreeGitdirFile(string worktreeGitDirectory)
		{
			string text = PathHelper.Combine(worktreeGitDirectory, "gitdir");
			if (!SafeFileExists(text))
			{
				Log.Warn("Can't find gitdir in '" + worktreeGitDirectory + "'");
				return null;
			}
			try
			{
				string text2 = File.ReadAllText(text).TrimEnd();
				if (Path.IsPathRooted(text2))
				{
					return PathHelper.Normalize(text2);
				}
				return PathHelper.Normalize(Path.GetFullPath(PathHelper.Combine(worktreeGitDirectory, text2)));
			}
			catch (Exception arg)
			{
				Log.Warn($"Can't read gitdir in '{text}': '{arg}'");
				return null;
			}
		}

		[Null]
		private static string GetHead(string gitDirectory)
		{
			if (SafeDirectoryExists(PathHelper.Combine(gitDirectory, "reftable")))
			{
				return ReadHeadStringFromReferences(gitDirectory);
			}
			return ReadHeadString(gitDirectory);
		}

		[Null]
		private static string ReadHeadString(string gitDirectory)
		{
			string path = PathHelper.Combine(gitDirectory, "HEAD");
			try
			{
				string text = File.ReadAllText(path).TrimEnd();
				if (text.IndexOf("ref: ") != -1)
				{
					return text.Substring(5);
				}
				return text;
			}
			catch
			{
				return null;
			}
		}

		private static bool IsBareRepository(string configPath)
		{
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(configPath);
			if (gitCommandResult.Succeeded)
			{
				return gitCommandResult.Result.GetString("core", null, "bare") == "true";
			}
			return false;
		}

		private static bool SafeDirectoryExists(string path)
		{
			try
			{
				return !string.IsNullOrEmpty(path) && Directory.Exists(path);
			}
			catch (PathTooLongException ex)
			{
				Log.Warn($"Path is too long: '{path}': '{ex.Message}'");
				return false;
			}
		}

		private static bool SafeFileExists(string path)
		{
			try
			{
				return !string.IsNullOrEmpty(path) && File.Exists(path);
			}
			catch (PathTooLongException ex)
			{
				Log.Warn($"Path is too long: '{path}': '{ex.Message}'");
				return false;
			}
		}

		[Null]
		private static string ReadHeadStringFromReferences(string gitDirectory)
		{
			GitCommandResult<string> gitCommandResult = BtRequest.Run(() => default(BtReferences), delegate(ref BtReferences x)
			{
				return Bt.bt_get_references(gitDirectory, skip_tags: false, ref x);
			}, delegate(ref BtReferences x)
			{
				GitCommandResult<(string[], string[])> symrefs = x.GetSymrefs();
				if (symrefs.Succeeded)
				{
					(string[], string[]) result = symrefs.Result;
					string[] item = result.Item1;
					string[] item2 = result.Item2;
					int? num = item.IndexOfItem((string s) => s == "HEAD");
					if (num.HasValue)
					{
						int valueOrDefault = num.GetValueOrDefault();
						return GitCommandResult<string>.Success(item2[valueOrDefault]);
					}
				}
				GitCommandResult<(string[], Sha[])> refs = x.GetRefs();
				if (refs.Succeeded)
				{
					(string[], Sha[]) result2 = refs.Result;
					string[] item3 = result2.Item1;
					Sha[] item4 = result2.Item2;
					int? num = item3.IndexOfItem((string n) => n == "HEAD");
					if (num.HasValue)
					{
						int valueOrDefault2 = num.GetValueOrDefault();
						return GitCommandResult<string>.Success(item4[valueOrDefault2].ToString());
					}
				}
				return GitCommandResult<string>.Success(null);
			}, delegate(ref BtReferences x)
			{
				Bt.bt_release_references(ref x);
			});
			if (gitCommandResult.Succeeded)
			{
				return gitCommandResult.Result;
			}
			return null;
		}
	}
}
