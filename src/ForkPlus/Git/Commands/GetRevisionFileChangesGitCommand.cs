using System.IO;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetRevisionFileChangesGitCommand : GetFileChangesGitCommand
	{
		public GitCommandResult<DiffContent> Execute(GitModule gitModule, RevisionDiffTarget target, ChangedFile changedFile, int contextSize, int tabWidth, bool ignoreWhitespaces, bool showEntireFile)
		{
			if (target is RevisionDiffTarget.WorkingDirectory workingDirectory)
			{
				GitCommandResult<DiffContent> gitCommandResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(gitModule, changedFile, new GetWorkingDirectoryFileChangesGitCommand.WorkingDirectoryRevisionDiffTarget.Revision(workingDirectory.Sha.ToString()), contextSize, tabWidth, ignoreWhitespaces, showEntireFile, loadLargeUntrackedFiles: false, resolvedConflict: false);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<DiffContent>.Failure(gitCommandResult.Error);
				}
				if (gitCommandResult.Result is ParsedDiffContent result)
				{
					return GitCommandResult<DiffContent>.Success(result);
				}
				if (gitCommandResult.Result is TextDiffContent textDiffContent)
				{
					GitCommandResult<Patch> gitCommandResult2 = new PatchParser().Parse(textDiffContent.Text);
					if (!gitCommandResult2.Succeeded)
					{
						return GitCommandResult<DiffContent>.Failure(gitCommandResult2.Error);
					}
					Patch result2 = gitCommandResult2.Result;
					return GitCommandResult<DiffContent>.Success(new ParsedDiffContent(gitModule, changedFile, result2.Diffs.FirstItem(), tabWidth, showEntireFile));
				}
				return GitCommandResult<DiffContent>.Failure(new GitCommandError.Bug("Invalid diff content type for working directory diff with " + workingDirectory.Sha));
			}
			if (target is RevisionDiffTarget.Range range)
			{
				return ExecuteInternal(gitModule, range.Sha.ToString(), range.OtherSha.ToString(), changedFile, contextSize, tabWidth, ignoreWhitespaces, showEntireFile);
			}
			GitCommandResult<DiffContent> gitCommandResult3 = ExecuteInternal(gitModule, target.Sha.ToString(), $"{target.Sha}~1", changedFile, contextSize, tabWidth, ignoreWhitespaces, showEntireFile);
			if (!gitCommandResult3.Succeeded && GetFileChangesGitCommand.IsRootReferenceError(gitCommandResult3.Error))
			{
				return ExecuteInternal(gitModule, target.Sha.ToString(), "4b825dc642cb6eb9a060e54bf8d69288fbee4904", changedFile, contextSize, tabWidth, ignoreWhitespaces, showEntireFile);
			}
			return gitCommandResult3;
		}

		public GitCommandResult<DiffContent> GetBinaryContent(GitModule gitModule, ChangedFile changedFile, string sha, [Null] string otherSha)
		{
			otherSha = otherSha ?? (sha + "~1");
			string text = changedFile.OldPath ?? changedFile.Path;
			string path = changedFile.Path;
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("show", (otherSha + ":" + text).Quotify()).Execute();
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("show", (sha + ":" + path).Quotify()).Execute();
			if (GetFileChangesGitCommand.IsLfsContent(gitRequestResult.Stdout) || GetFileChangesGitCommand.IsLfsContent(gitRequestResult2.Stdout))
			{
				LfsPointer src = LfsPointer.Parse(gitRequestResult?.Stdout);
				LfsPointer dst = LfsPointer.Parse(gitRequestResult2?.Stdout);
				return GitCommandResult<DiffContent>.Success(new LfsDiffContent(gitModule, changedFile, BinaryFileType.LfsBinaryFile, src, dst));
			}
			MemoryStream srcData = CatFile(gitModule, otherSha, text);
			MemoryStream dstData = CatFile(gitModule, sha, path);
			return GitCommandResult<DiffContent>.Success(new BinaryDiffContent(changedFile, srcData, dstData));
		}

		[Null]
		private static MemoryStream CatFile(GitModule gitModule, string sha, string path)
		{
			ShellRequestBinaryResult shellRequestBinaryResult = new GitRequest(gitModule).Command("cat-file", "--filters", (sha + ":" + path).Quotify()).ExecuteBinary(null, silent: true);
			if (shellRequestBinaryResult.Success)
			{
				return shellRequestBinaryResult.Stdout;
			}
			return null;
		}

		private GitCommandResult<DiffContent> ExecuteInternal(GitModule gitModule, string sha, string otherSha, ChangedFile changedFile, int contextSize, int tabWidth, bool ignoreWhitespaces, bool showEntireFile)
		{
			string text = (string.IsNullOrEmpty(otherSha) ? (sha + "~1") : otherSha);
			GitCommand gitCommand = new GitCommand("-c", "core.quotepath=false", "--no-pager", "diff", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--submodule=short", text, sha);
			if (changedFile.OldPath != null)
			{
				gitCommand.Add("--find-renames");
			}
			if (showEntireFile)
			{
				gitCommand.Add("--inter-hunk-context=100000");
				gitCommand.Add("--unified=100000");
			}
			else
			{
				gitCommand.Add($"--unified={contextSize}");
			}
			if (ignoreWhitespaces)
			{
				gitCommand.Add("--ignore-all-space");
			}
			gitCommand.Add("--");
			gitCommand.Add(changedFile.Path.Quotify());
			if (changedFile.OldPath != null)
			{
				gitCommand.Add(changedFile.OldPath.Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<DiffContent>.Failure(gitRequestResult.ToGitCommandError());
			}
			GitCommandResult<Patch> gitCommandResult = new PatchParser().Parse(gitRequestResult.Stdout);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<DiffContent>.Failure(gitCommandResult.Error);
			}
			Patch result = gitCommandResult.Result;
			return GitCommandResult<DiffContent>.Success(new ParsedDiffContent(gitModule, changedFile, result.Diffs.FirstItem(), tabWidth, showEntireFile));
		}
	}
}
