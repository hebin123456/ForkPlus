using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetWorkingDirectoryChangedFilesGitCommand
	{
		public GitCommandResult<ChangedFile[]> ExecuteForAmend(GitModule gitModule, [Null] Submodule[] submodules)
		{
			GitCommandResult<ChangedFile[]> gitCommandResult = Execute(gitModule, "HEAD^", staged: true, submodules ?? new Submodule[0]);
			if (!gitCommandResult.Succeeded && IsRootReferenceError(gitCommandResult.Error))
			{
				return Execute(gitModule, Sha.NullSha.ToString(), staged: true, submodules ?? new Submodule[0]);
			}
			return gitCommandResult;
		}

		public GitCommandResult<ChangedFile[]> ExecuteForRevision(GitModule gitModule, string sha, [Null] Submodule[] submodules)
		{
			return Execute(gitModule, sha, staged: false, submodules ?? new Submodule[0]);
		}

		private static GitCommandResult<ChangedFile[]> Execute(GitModule gitModule, string sha, bool staged, Submodule[] submodules)
		{
			GitCommand gitCommand = new GitCommand("diff-index", "-z", "-M");
			if (staged)
			{
				gitCommand.Add("--cached");
			}
			gitCommand.Add(sha);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<ChangedFile[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<ChangedFile[]>.Success(ParseChangedFiles(gitRequestResult.Stdout, submodules));
		}

		private static ChangedFile[] ParseChangedFiles(string input, Submodule[] submodules)
		{
			string[] array = input.Split(Consts.Chars.Nul, StringSplitOptions.None);
			List<ChangedFile> list = new List<ChangedFile>();
			for (int i = 0; i < array.Length - 1; i += 2)
			{
				string text = array[i];
				string text2 = array[i + 1];
				string[] array2 = text.Split(Consts.Chars.Space);
				if (array2.Length != 5)
				{
					Log.Error("Cannot parse changed file in '" + text + "'");
					return list.ToArray();
				}
				string fileMode = array2[1];
				string treeIsh = array2[2];
				string text3 = array2[4];
				string text4 = text2;
				if (text3.Length <= 0)
				{
					Log.Error("Cannot parse change type in '" + text + "'");
					return list.ToArray();
				}
				StatusType status = StatusTypeHelper.Parse(text3[0]);
				ChangeType changeType = ChangeTypeHelper.Parse(text3);
				Submodule submodule = submodules.FirstItem(text4, (Submodule x, string p) => x.Path == p);
				if (submodule != null)
				{
					list.Add(new SubmoduleChangedFile(submodule, text4, status, StatusType.None, null, treeIsh, fileMode));
					continue;
				}
				string oldPath = null;
				if (changeType == ChangeType.Renamed || changeType == ChangeType.Copied)
				{
					oldPath = text4;
					text4 = array[i + 2];
					i++;
				}
				list.Add(new ChangedFile(text4, status, StatusType.None, oldPath, treeIsh, fileMode));
			}
			list.Sort(ChangedFile.Comparer.Compare);
			return list.ToArray();
		}

		private static bool IsRootReferenceError(GitCommandError error)
		{
			if (error is GitCommandError.GitError gitError && gitError.Stderr.Contains("unknown revision or path not in the working tree."))
			{
				return true;
			}
			return false;
		}
	}
}
