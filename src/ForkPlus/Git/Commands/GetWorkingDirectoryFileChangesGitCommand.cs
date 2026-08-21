using System;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetWorkingDirectoryFileChangesGitCommand : GetFileChangesGitCommand
	{
		[Flags]
		private enum ChangeStatus
		{
			Unstaged = 1,
			Staged = 2,
			Committed = 4
		}

		public abstract class WorkingDirectoryRevisionDiffTarget
		{
			public class Revision : WorkingDirectoryRevisionDiffTarget
			{
				public Revision(string sha)
					: base(sha)
				{
				}
			}

			public class Amend : WorkingDirectoryRevisionDiffTarget
			{
				public Amend()
					: base("HEAD^")
				{
				}
			}

			public string Sha { get; }

			public WorkingDirectoryRevisionDiffTarget(string sha)
			{
				Sha = sha;
			}
		}

		// v3.10.2 修复：status 类命令（CreateReliableStatusCommand）显式带 core.checkStat=default（比较 ctime 等细粒度字段），
		// 而 diff 命令此前未带 → 用仓库默认（Git for Windows 默认 minimal，只比 mtime+size）。
		// 当文件内容已变但 mtime+size 恰好未变（同秒保存/时间戳精度/编辑器恢复 mtime）时：
		// status 报 Modified（列表显示修改），diff 却误判 clean（修改详情空白）——两者口径不一致。
		// 统一所有 diff 命令与 status 相同的 checkStat 口径，消除"看得见变化、看不见内容"的分裂。
		// 已用真实 git 实验验证：stat 匹配+内容已变时，minimal diff 输出空，default diff 输出正常。
		private static GitCommand CreateReliableDiffCommand(params string[] args)
		{
			GitCommand command = new GitCommand("-c", "core.checkStat=default");
			command.AddRange(args);
			return command;
		}

		public GitCommandResult<string> GetStagedPatch(GitModule gitModule, bool amend)
		{
			GitCommand gitCommand = CreateReliableDiffCommand("diff", "--find-renames", "--staged", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=50");
			if (amend)
			{
				gitCommand.Add("HEAD^");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
		// git diff 退出码 1 表示有差异，是正常的。
		if (gitRequestResult.ExitCode >= 2)
		{
			return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
		}
		return GitCommandResult<string>.Success(gitRequestResult.Stdout);
	}

	public GitCommandResult<string> GetChangesAsBinaryPatch(GitModule gitModule, ChangedFile changedFile, bool amend)
		{
			string srcRevision = (amend ? "HEAD^" : null);
			GitCommandResult<string> changesAsBinaryPatchInternal = GetChangesAsBinaryPatchInternal(gitModule, changedFile, amend, srcRevision);
			if (!changesAsBinaryPatchInternal.Succeeded && changedFile.Staged && amend && GetFileChangesGitCommand.IsRootReferenceError(changesAsBinaryPatchInternal.Error))
			{
				return GetChangesAsBinaryPatchInternal(gitModule, changedFile, amend, Sha.NullSha.ToString());
			}
			return changesAsBinaryPatchInternal;
		}

		private GitCommandResult<string> GetChangesAsBinaryPatchInternal(GitModule gitModule, ChangedFile changedFile, bool amend, [Null] string srcRevision)
		{
			GitCommand gitCommand = CreateReliableDiffCommand("-c", "core.quotepath=false", "--no-pager", "diff", "--find-renames", "--binary", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--submodule=short");
			if (changedFile.Staged)
			{
				gitCommand.Add("--staged");
			}
			if (changedFile.Staged && amend)
			{
				gitCommand = CreateReliableDiffCommand("-c", "core.quotepath=false", "--no-pager", "diff-index", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--patch", srcRevision, "--cached");
			}
			if (!changedFile.Tracked)
			{
				gitCommand.Add("--no-index");
			}
			gitCommand.Add("--");
			if (!changedFile.Tracked)
			{
				gitCommand.Add("/dev/null");
			}
			gitCommand.Add(changedFile.Path.Quotify());
			if (!string.IsNullOrEmpty(changedFile.OldPath))
			{
				gitCommand.Add(changedFile.OldPath.Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
			// git diff 退出码 1 表示有差异，是正常的。
			if (gitRequestResult.ExitCode >= 2)
			{
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string>.Success(gitRequestResult.Stdout);
		}

		public GitCommandResult<DiffContent> Execute(GitModule gitModule, ChangedFile changedFile, [Null] WorkingDirectoryRevisionDiffTarget revisionTarget, int contextSize, int tabWidth, bool ignoreWhitespaces, bool showEntireFile, bool loadLargeUntrackedFiles, bool resolvedConflict)
		{
			GitCommandResult<DiffContent> gitCommandResult = ExecuteInternal(gitModule, changedFile, revisionTarget, contextSize, tabWidth, ignoreWhitespaces, showEntireFile, loadLargeUntrackedFiles, resolvedConflict);
			if (!gitCommandResult.Succeeded && GetFileChangesGitCommand.IsRootReferenceError(gitCommandResult.Error) && revisionTarget != null)
			{
				WorkingDirectoryRevisionDiffTarget.Revision revisionTarget2 = new WorkingDirectoryRevisionDiffTarget.Revision(Sha.NullSha.ToString());
				return ExecuteInternal(gitModule, changedFile, revisionTarget2, contextSize, tabWidth, ignoreWhitespaces, showEntireFile, loadLargeUntrackedFiles, resolvedConflict);
			}
			return gitCommandResult;
		}

		private GitCommandResult<DiffContent> ExecuteInternal(GitModule gitModule, ChangedFile changedFile, [Null] WorkingDirectoryRevisionDiffTarget revisionTarget, int contextSize, int tabWidth, bool ignoreWhitespaces, bool showEntireFile, bool loadLargeUntrackedFiles, bool resolvedConflict)
		{
			if (!PathHelper.IsImagePath(changedFile.Path) && !changedFile.Tracked && !loadLargeUntrackedFiles)
			{
				long? fileSize = FileHelper.GetFileSize(gitModule.MakePath(changedFile.Path));
				if (fileSize.HasValue)
				{
					long valueOrDefault = fileSize.GetValueOrDefault();
					if (valueOrDefault > 5242880)
					{
						return GitCommandResult<DiffContent>.Failure(new GitCommandError.ChangesAreTooLarge(valueOrDefault));
					}
				}
			}
			GitCommand gitCommand = CreateReliableDiffCommand("-c", "core.quotepath=false", "--no-pager", "diff", "--find-renames", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--submodule=short");
			if (changedFile.Staged)
			{
				gitCommand.Add("--staged");
			}
			if (revisionTarget != null)
			{
				gitCommand = CreateReliableDiffCommand("-c", "core.quotepath=false", "--no-pager", "diff-index", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--patch", revisionTarget.Sha);
				if (revisionTarget is WorkingDirectoryRevisionDiffTarget.Amend)
				{
					gitCommand.Add("--cached");
				}
			}
			if (showEntireFile)
			{
				gitCommand.Add("--inter-hunk-context=1000000");
				gitCommand.Add("--unified=1000000");
			}
			else
			{
				gitCommand.Add($"--unified={contextSize}");
			}
			if (ignoreWhitespaces)
			{
				gitCommand.Add("--ignore-all-space");
			}
			if (!changedFile.Tracked)
			{
				gitCommand.Add("--no-index");
			}
			if (!resolvedConflict)
			{
				gitCommand.Add("--");
			}
			if (!changedFile.Tracked)
			{
				gitCommand.Add("/dev/null");
			}
			if (resolvedConflict)
			{
				gitCommand.Add(":2:" + changedFile.Path.Quotify());
				gitCommand.Add(changedFile.Path.Quotify());
			}
			else
			{
				gitCommand.Add(changedFile.Path.Quotify());
				if (!string.IsNullOrEmpty(changedFile.OldPath))
				{
					gitCommand.Add(changedFile.OldPath.Quotify());
				}
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
		// git diff 退出码：0=无差异，1=有差异（正常），2+=错误。
		// 之前用 !Success（即 ExitCode!=0）会误把有差异的 diff 当失败，导致新文件 diff 头部被当作错误文本显示。
		if (gitRequestResult.ExitCode >= 2)
		{
			Log.Error(gitRequestResult.Stderr);
			return GitCommandResult<DiffContent>.Failure(new GitCommandError.GitError(gitRequestResult));
		}
			bool flag = changedFile.ChangeType == ChangeType.Unmerged;
			if (flag && !resolvedConflict)
			{
				return GitCommandResult<DiffContent>.Success(new UnmergedDiffContent(fileType: (GetFileChangesGitCommand.ParseLfsDiff(gitRequestResult.Stdout, flag) != null) ? UnmergedDiffContent.ContentType.Lfs : ((changedFile is SubmoduleChangedFile) ? UnmergedDiffContent.ContentType.Submodule : ((!GetFileChangesGitCommand.IsBinaryContent(gitRequestResult.Stdout)) ? UnmergedDiffContent.ContentType.Text : UnmergedDiffContent.ContentType.Binary)), gitModule: gitModule, changedFile: changedFile, diffString: gitRequestResult.Stdout));
			}
			GitCommandResult<Patch> gitCommandResult = new PatchParser().Parse(gitRequestResult.Stdout);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<DiffContent>.Failure(new GitCommandError.ParseError("Failed to parse '" + changedFile.Path + "' diff: " + gitCommandResult.Error.FriendlyDescription));
			}
			Patch result = gitCommandResult.Result;
			return GitCommandResult<DiffContent>.Success(new ParsedDiffContent(gitModule, changedFile, result.Diffs.FirstItem(), tabWidth, showEntireFile));
		}
	}
}
