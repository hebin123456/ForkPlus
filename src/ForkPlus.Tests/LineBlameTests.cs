using System;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls.Editor.Diff.LineBlame;
using Xunit;

namespace ForkPlus.Tests
{
	public class LineBlameTests
	{
		[Fact]
		public void WorkingTreeInfo_CarriesStagedAndUntrackedFlags()
		{
			LineBlameInfo staged = LineBlameInfo.WorkingTree(isStaged: true, isUntracked: false, oldRevisionSpec: null);
			Assert.Equal(LineBlameMode.WorkingTree, staged.Mode);
			Assert.True(staged.IsStaged);
			Assert.False(staged.IsUntracked);
			Assert.Null(staged.NewRevisionSpec);

			LineBlameInfo untracked = LineBlameInfo.WorkingTree(isStaged: false, isUntracked: true, oldRevisionSpec: "HEAD~1");
			Assert.True(untracked.IsUntracked);
			Assert.Equal("HEAD~1", untracked.OldRevisionSpec);

			LineBlameInfo revision = LineBlameInfo.Revision("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
			Assert.Equal(LineBlameMode.Revision, revision.Mode);
			Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", revision.NewRevisionSpec);
			Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", revision.OldRevisionSpec);
		}

		[Fact]
		public void OldRevisionSpec_FallsBackToParentOrHead()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				LineBlameInfo revision = LineBlameInfo.Revision("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", null);
				LineBlameContext context = new LineBlameContext(module, null, "a.txt", null, revision);
				Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa~1", context.GetOldRevisionSpec());

				LineBlameInfo worktree = LineBlameInfo.WorkingTree(isStaged: false, isUntracked: false, oldRevisionSpec: null);
				LineBlameContext worktreeContext = new LineBlameContext(module, null, "a.txt", null, worktree);
				Assert.Equal("HEAD", worktreeContext.GetOldRevisionSpec());
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void TryFindChunk_LocatesContainingLineWithBinarySearch()
		{
			GetBlameGitCommand.BlameChunk chunk1 = CreateChunk(1, 3, Sha.Zero);
			GetBlameGitCommand.BlameChunk chunk2 = CreateChunk(4, 2, ParseSha("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
			GetBlameGitCommand.BlameChunk[] chunks = new[] { chunk1, chunk2 };

			Assert.True(LineBlameContext.TryFindChunk(chunks, 2, out GetBlameGitCommand.BlameChunk found));
			Assert.Equal(1, found.LineNumber);
			Assert.True(LineBlameContext.TryFindChunk(chunks, 4, out found));
			Assert.Equal(4, found.LineNumber);
			Assert.False(LineBlameContext.TryFindChunk(chunks, 6, out _));
			Assert.False(LineBlameContext.TryFindChunk(null, 1, out _));
			Assert.False(LineBlameContext.TryFindChunk(new GetBlameGitCommand.BlameChunk[0], 1, out _));
		}

		[Fact]
		public void Blame_WorktreeShowsUncommittedLines_HeadDoesNot()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);

				GitCommandResult<GetBlameGitCommand.BlameChunk[]> headResult =
					new GetBlameGitCommand().Execute(module, "a.txt", "HEAD");
				Assert.True(headResult.Succeeded, headResult.Error?.ToString());
				Assert.True(LineBlameContext.TryFindChunk(headResult.Result, 1, out GetBlameGitCommand.BlameChunk headLine));
				Assert.Equal("Test", headLine.Revision.Author.Name);
				Assert.Equal(3, headLine.LineCount);

				GitCommandResult<GetBlameGitCommand.BlameChunk[]> worktreeResult =
					new GetBlameGitCommand().Execute(module, "a.txt", null);
				Assert.True(worktreeResult.Succeeded, worktreeResult.Error?.ToString());
				Assert.True(LineBlameContext.TryFindChunk(worktreeResult.Result, 5, out GetBlameGitCommand.BlameChunk uncommittedLine), "工作区追加行应能找到 blame 块");
				Assert.Equal(Sha.Zero, uncommittedLine.Revision.Sha);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void LineHistory_ReturnsCommitForLine()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				GitCommandResult<(RevisionWithFiles[], string[])> result =
					new GetFileHistoryGitCommand().Execute(module, "a.txt", new Range(1, 1), null);
				Assert.True(result.Succeeded, result.Error?.ToString());
				Assert.NotEmpty(result.Result.Item1);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		private static GetBlameGitCommand.BlameChunk CreateChunk(int lineNumber, int lineCount, Sha sha)
		{
			Revision revision = new Revision(sha, new RevisionHeader(new UserIdentity("Test", "test@example.com"), DateTime.Now, "subject", hasBody: false));
			return new GetBlameGitCommand.BlameChunk(lineNumber, lineCount, revision);
		}

		private static Sha ParseSha(string value)
		{
			Assert.True(Sha.TryParse(value, out Sha sha));
			return sha;
		}
	}
}
