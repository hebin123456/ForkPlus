using ForkPlus.Biturbo;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetSubmoduleDiffContentGitCommand
	{
		public GitCommandResult<SubmoduleDiffContent> Execute(GitModule submoduleGitModule, GitModule parentGitModule, Sha srcSha, Sha dstSha, bool isSubmoduleWorkingDirectoryDirty, SubmoduleChangedFile changedFile, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			RevisionStorage revisionStorage;
			if (srcSha != Sha.Zero && dstSha != Sha.Zero)
			{
				using CommitGraphCache commitGraphCache = new CommitGraphCache(submoduleGitModule);
				GitCommandResult<RevisionStorage> gitCommandResult = new GetRevisionStorageGitCommand().Execute(submoduleGitModule, commitGraphCache, srcSha, dstSha);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult.Error);
				}
				revisionStorage = gitCommandResult.Result;
			}
			else
			{
				revisionStorage = RevisionStorage.Empty;
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			GitCommandResult<GitConfig> gitCommandResult2 = new GetGitConfigGitCommand().Execute(submoduleGitModule);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult2.Error);
			}
			GitConfig result = gitCommandResult2.Result;
			GitCommandResult<ReferenceStorage> gitCommandResult3 = new GetReferencesGitCommand().Execute(submoduleGitModule, result);
			if (!gitCommandResult3.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult3.Error);
			}
			_ = gitCommandResult3.Result;
			GitCommandResult<RepositoryRemotes> gitCommandResult4 = new GetRemotesGitCommand().Execute(result);
			if (!gitCommandResult4.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult4.Error);
			}
			RepositoryRemotes result2 = gitCommandResult4.Result;
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			GitCommandResult<Revision[]> revisions = GetRevisions(submoduleGitModule, srcSha, dstSha);
			if (!revisions.Succeeded)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(revisions.Error);
			}
			Revision srcRevision = revisions.Result[0];
			Revision dstRevision = revisions.Result[1];
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			RepositoryReferences references = RepositoryReferences.New(gitCommandResult3.Result.WithHead(dstSha), new string[0], new string[0], new string[0], hideTags: false);
			string[] changedFilePaths = new string[0];
			if (isSubmoduleWorkingDirectoryDirty)
			{
				GitCommandResult<ChangedFilesCollection> gitCommandResult5 = new GetChangedFilesGitCommand().Execute(submoduleGitModule, new Submodule[0], excludeUntrackedFiles: true);
				if (!gitCommandResult5.Succeeded)
				{
					return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult5.Error);
				}
				changedFilePaths = gitCommandResult5.Result.ChangedFiles.Map((ChangedFile x) => x.Path);
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			BugtrackerLinkDefinition[] bugtrackers = new GetBugtrackerRulesGitCommand().Execute(submoduleGitModule);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			BehindAheadCount behindAheadCount;
			if (srcSha != Sha.Zero && dstSha != Sha.Zero)
			{
				using CommitGraphCache commitGraphCache2 = new CommitGraphCache(submoduleGitModule);
				GitCommandResult<BehindAheadCount> gitCommandResult6 = new GetBehindAheadCountGitCommand().Execute(submoduleGitModule, srcSha, dstSha, commitGraphCache2);
				if (!gitCommandResult6.Succeeded)
				{
					return GitCommandResult<SubmoduleDiffContent>.Failure(gitCommandResult6.Error);
				}
				behindAheadCount = gitCommandResult6.Result;
			}
			else
			{
				behindAheadCount = new BehindAheadCount(0, 0);
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<SubmoduleDiffContent>.Failure(new GitCommandError.Cancelled());
			}
			return GitCommandResult<SubmoduleDiffContent>.Success(new SubmoduleDiffContent(changedFile, changedFile.Submodule, submoduleGitModule, result, parentGitModule, srcRevision, dstRevision, srcSha, dstSha, references, result2, revisionStorage, behindAheadCount, changedFilePaths, bugtrackers));
		}

		private GitCommandResult<Revision[]> GetRevisions(GitModule gitModule, Sha src, Sha dst)
		{
			if (src != Sha.Zero)
			{
				if (dst != Sha.Zero)
				{
					GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, new Sha[2] { src, dst });
					if (!gitCommandResult.Succeeded)
					{
						return GitCommandResult<Revision[]>.Failure(gitCommandResult.Error);
					}
					if (gitCommandResult.Result.Length != 2)
					{
						return GitCommandResult<Revision[]>.Failure(new GitCommandError.Bug("Failed to find src or dst)"));
					}
					return GitCommandResult<Revision[]>.Success(new Revision[2]
					{
						gitCommandResult.Result[0],
						gitCommandResult.Result[1]
					});
				}
				GitCommandResult<Revision[]> gitCommandResult2 = new GetRevisionsGitCommand().Execute(gitModule, new Sha[1] { src });
				if (!gitCommandResult2.Succeeded)
				{
					return GitCommandResult<Revision[]>.Failure(gitCommandResult2.Error);
				}
				Revision revision = gitCommandResult2.Result.SingleItem();
				if (revision == null)
				{
					return GitCommandResult<Revision[]>.Failure(new GitCommandError.Bug("Failed to find src)"));
				}
				return GitCommandResult<Revision[]>.Success(new Revision[2] { revision, null });
			}
			if (dst != Sha.Zero)
			{
				GitCommandResult<Revision[]> gitCommandResult3 = new GetRevisionsGitCommand().Execute(gitModule, new Sha[1] { dst });
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<Revision[]>.Failure(gitCommandResult3.Error);
				}
				Revision revision2 = gitCommandResult3.Result.SingleItem();
				if (revision2 == null)
				{
					return GitCommandResult<Revision[]>.Failure(new GitCommandError.Bug("Failed to find dst)"));
				}
				return GitCommandResult<Revision[]>.Success(new Revision[2] { null, revision2 });
			}
			return GitCommandResult<Revision[]>.Success(new Revision[2]);
		}
	}
}
