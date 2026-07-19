using System.Linq;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.2.0 AI Commit Composer 单个分组数据模型单元测试。覆盖 BuildFullMessage、HasUnmatchedFiles、UnmatchedFiles。
	/// </summary>
	public class WipCommitGroupTests
	{
		[Fact]
		public void BuildFullMessage_NullSubjectAndBody_ReturnsEmpty()
		{
			WipCommitGroup g = new WipCommitGroup();
			Assert.Equal("", g.BuildFullMessage());
		}

		[Fact]
		public void BuildFullMessage_OnlySubject_ReturnsSubject()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "fix: typo" };
			Assert.Equal("fix: typo", g.BuildFullMessage());
		}

		[Fact]
		public void BuildFullMessage_SubjectAndBody_ReturnsCombinedWithBlankLine()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "fix: bug", Body = "explanation" };
			Assert.Equal("fix: bug\n\nexplanation", g.BuildFullMessage());
		}

		[Fact]
		public void BuildFullMessage_EmptyBody_ReturnsOnlySubject()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "fix: bug", Body = "   " };
			Assert.Equal("fix: bug", g.BuildFullMessage());
		}

		[Fact]
		public void BuildFullMessage_TrimsWhitespace()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "  fix: bug  ", Body = "  details  " };
			Assert.Equal("fix: bug\n\ndetails", g.BuildFullMessage());
		}

		[Fact]
		public void HasUnmatchedFiles_NoFiles_False()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			Assert.False(g.HasUnmatchedFiles);
		}

		[Fact]
		public void HasUnmatchedFiles_AllMatched_False()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.Files.Add("a.cs");
			g.Files.Add("b.cs");
			g.MatchedFiles.Add(MakeChangedFile("a.cs"));
			g.MatchedFiles.Add(MakeChangedFile("b.cs"));
			Assert.False(g.HasUnmatchedFiles);
		}

		[Fact]
		public void HasUnmatchedFiles_SomeUnmatched_True()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.Files.Add("a.cs");
			g.Files.Add("b.cs");
			g.Files.Add("c.cs");
			g.MatchedFiles.Add(MakeChangedFile("a.cs"));
			// b.cs 和 c.cs 未匹配
			Assert.True(g.HasUnmatchedFiles);
		}

		[Fact]
		public void UnmatchedFiles_ReturnsOnlyUnmatchedPaths()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.Files.Add("a.cs");
			g.Files.Add("b.cs");
			g.Files.Add("c.cs");
			g.MatchedFiles.Add(MakeChangedFile("a.cs"));
			g.MatchedFiles.Add(MakeChangedFile("c.cs"));

			var unmatched = g.UnmatchedFiles.ToList();
			Assert.Equal(new[] { "b.cs" }, unmatched);
		}

		[Fact]
	public void UnmatchedFiles_DuplicateMatchedPath_NotReturnedAsUnmatched()
	{
		// 实现细节：UnmatchedFiles 用 HashSet<string> 检查 MatchedFiles.Path，
		// 重复的 Files 路径如果已在 matched 中，则不会出现在 unmatched 中
		WipCommitGroup g = new WipCommitGroup { Subject = "x" };
		g.Files.Add("a.cs");
		g.Files.Add("a.cs");
		g.MatchedFiles.Add(MakeChangedFile("a.cs"));

		var unmatched = g.UnmatchedFiles.ToList();
		Assert.Empty(unmatched);
	}

	[Fact]
	public void UnmatchedFiles_DuplicateUnmatchedPath_ReturnedForEachOccurrence()
	{
		// 同一未匹配路径在 Files 中出现两次，应都被 yield 出来（实现里没去重 Files）
		WipCommitGroup g = new WipCommitGroup { Subject = "x" };
		g.Files.Add("missing.cs");
		g.Files.Add("missing.cs");

		var unmatched = g.UnmatchedFiles.ToList();
		Assert.Equal(2, unmatched.Count);
		Assert.All(unmatched, p => Assert.Equal("missing.cs", p));
	}

		[Fact]
		public void MatchedFileCount_ReturnsMatchedFilesCount()
		{
			WipCommitGroup g = new WipCommitGroup { Subject = "x" };
			g.MatchedFiles.Add(MakeChangedFile("a.cs"));
			g.MatchedFiles.Add(MakeChangedFile("b.cs"));
			Assert.Equal(2, g.MatchedFileCount);
		}

		[Fact]
		public void Constructor_WithSubject_SetsProperties()
		{
			WipCommitGroup g = new WipCommitGroup("subj", "body", "reason");
			Assert.Equal("subj", g.Subject);
			Assert.Equal("body", g.Body);
			Assert.Equal("reason", g.Reason);
			Assert.Empty(g.Files);
			Assert.Empty(g.MatchedFiles);
		}

		internal static ChangedFile MakeChangedFile(string path, string oldPath = null)
		{
			return new ChangedFile(path, StatusType.Modified, StatusType.None,
				ChangeType.Modified, staged: true, isNew: false, tracked: true, oldPath: oldPath);
		}
	}
}
