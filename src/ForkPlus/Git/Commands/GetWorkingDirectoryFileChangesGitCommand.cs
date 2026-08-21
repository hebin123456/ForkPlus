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

		// v3.10.2 修复（根因）：diff 命令必须与 status 命令完全对齐 fsmonitor/untrackedCache/checkStat/--no-optional-locks 四件套。
		//
		// 此前 diff 只带了 core.checkStat=default，缺少 core.fsmonitor=false。
		// 启用了 core.fsmonitor=true 的仓库（部分 git mm 子仓会开），git diff 内部会先问 fsmonitor daemon
		// "这个文件脏不脏"——daemon 的脏文件列表可能过期漏报（daemon 未收到 inotify/被挂起/时序竞争），
		// git diff 信了就跳过该文件直接判 clean → 输出空 diff。这就是"列表里显示 Modified，点开修改详情却是空白"
		// 的真正根因：status 绕过了 daemon（带 fsmonitor=false），diff 没有，两者口径分裂。
		// 用 TortoiseGit 打开 commit 界面后 ForkPlus 恢复正常，是因为 TortoiseGit 触碰了工作区/刷新了 daemon，
		// 让 daemon 的脏文件列表追上了真实状态——但这是巧合，不是机制保证。
		//
		// 统一四件套：
		//   - core.fsmonitor=false：绕过 fsmonitor daemon，强制 git diff 逐文件做真正的 stat+content 比较
		//   - core.untrackedCache=false：绕过 untracked cache，避免缓存过期导致的漏报
		//   - core.checkStat=default：比较 ctime 等细粒度字段（而非默认 minimal 只比 mtime+size）
		//   - --no-optional-locks：不抢 optional lock，避免与并发的 status/fetch 等操作互相阻塞
		private static GitCommand CreateReliableDiffCommand(params string[] args)
		{
			GitCommand command = new GitCommand(
				"-c", "core.fsmonitor=false",
				"-c", "core.untrackedCache=false",
				"-c", "core.checkStat=default",
				"--no-optional-locks");
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
