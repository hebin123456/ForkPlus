using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetRevisionChangedFilesGitCommand
	{
		public GitCommandResult<ChangedFile[]> Execute(GitModule gitModule, RevisionDiffTarget target, [Null] Submodule[] submodules)
		{
			submodules = submodules ?? new Submodule[0];
			if (target is RevisionDiffTarget.WorkingDirectory workingDirectory)
			{
				return new GetWorkingDirectoryChangedFilesGitCommand().ExecuteForRevision(gitModule, workingDirectory.Sha.ToString(), submodules);
			}
			if (target is RevisionDiffTarget.Range range)
			{
				return ExecuteInt(gitModule, range.Sha.ToString(), range.OtherSha.ToString(), submodules);
			}
			GitCommandResult<ChangedFile[]> gitCommandResult = ExecuteInt(gitModule, target.Sha.ToString(), $"{target.Sha}~1", submodules);
			if (!gitCommandResult.Succeeded && IsRootReferenceError(gitCommandResult.Error))
			{
				return ExecuteInt(gitModule, target.Sha.ToString(), "4b825dc642cb6eb9a060e54bf8d69288fbee4904", submodules);
			}
			return gitCommandResult;
		}

		private static bool IsRootReferenceError(GitCommandError error)
		{
			if (error is GitCommandError.GitError gitError && gitError.Stderr.Contains("unknown revision or path not in the working tree."))
			{
				return true;
			}
			return false;
		}

		private static GitCommandResult<ChangedFile[]> ExecuteInt(GitModule gitModule, string sha, [Null] string otherSha, Submodule[] submodules)
		{
			string text = (string.IsNullOrEmpty(otherSha) ? (sha + "~1") : otherSha);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("--no-pager", "diff", "--name-status", "--find-renames", "-z", text, sha).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<ChangedFile[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return ParseChangedFiles(gitRequestResult.Stdout, submodules);
		}

		public static GitCommandResult<ChangedFile[]> ParseChangedFiles(string input, Submodule[] submodules)
		{
			string[] array = input.Split(Consts.Chars.Nul);
			List<ChangedFile> list = new List<ChangedFile>(128);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				if (text.Length <= 0)
				{
					continue;
				}
				StatusType statusType = StatusTypeHelper.Parse(text[0]);
				string text2 = array[i + 1];
				switch (statusType)
				{
				case StatusType.Added:
				case StatusType.Deleted:
				case StatusType.Modified:
				case StatusType.TypeChanged:
				{
					Submodule submodule = submodules.FirstItem(text2, (Submodule x, string p) => x.Path == p);
					if (submodule != null)
					{
						list.Add(new SubmoduleChangedFile(submodule, text2, statusType));
					}
					else
					{
						list.Add(new ChangedFile(text2, statusType));
					}
					i++;
					break;
				}
				case StatusType.Copied:
				case StatusType.Renamed:
				{
					string path = array[i + 2];
					list.Add(new ChangedFile(path, statusType, StatusType.None, text2));
					i += 2;
					break;
				}
				default:
					Log.Warn("Unknown changeMark type '" + text + "'");
					i++;
					break;
				}
			}
			return GitCommandResult<ChangedFile[]>.Success(list.ToArray());
		}
	}
}
