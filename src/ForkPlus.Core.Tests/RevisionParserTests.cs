using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class RevisionParserTests
	{
		private const string SampleSha40 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
		private const string ZeroSha40 = "0000000000000000000000000000000000000000";
		private const string HighSha40 = "ffffffffffffffffffffffffffffffffffffffff";

		[Fact]
		public void ParseRevisionParents_EmptyString_ReturnsEmptyArray()
		{
			Sha[] parents = RevisionParser.ParseRevisionParents("");

			Assert.Empty(parents);
		}

		[Fact]
		public void ParseRevisionParents_SingleSha40Chars_ReturnsOneParent()
		{
			Sha[] parents = RevisionParser.ParseRevisionParents(SampleSha40);

			Assert.Single(parents);
			Assert.Equal(SampleSha40, parents[0].ToString());
		}

		[Fact]
		public void ParseRevisionParents_TwoShas81Chars_ReturnsTwoParents()
		{
			string line = SampleSha40 + " " + ZeroSha40;

			Sha[] parents = RevisionParser.ParseRevisionParents(line);

			Assert.Equal(2, parents.Length);
			Assert.Equal(SampleSha40, parents[0].ToString());
			Assert.Equal(ZeroSha40, parents[1].ToString());
		}

		[Fact]
		public void ParseRevisionParents_MultipleShasSeparatedBySpace_ReturnsAllParents()
		{
			string line = SampleSha40 + " " + ZeroSha40 + " " + HighSha40;

			Sha[] parents = RevisionParser.ParseRevisionParents(line);

			Assert.Equal(3, parents.Length);
			Assert.Equal(SampleSha40, parents[0].ToString());
			Assert.Equal(ZeroSha40, parents[1].ToString());
			Assert.Equal(HighSha40, parents[2].ToString());
		}

		[Fact]
		public void ParseRevisionParents_KnownNullShaString_ParsesToNullSha()
		{
			Sha[] parents = RevisionParser.ParseRevisionParents(SampleSha40);

			Assert.Single(parents);
			Assert.Equal(Sha.NullSha, parents[0]);
		}

		[Fact]
		public void ParseRevision_ParsesAllFiveFieldsAndAdvancesIndex()
		{
			string[] lines =
			{
				SampleSha40,
				"alice",
				"alice@example.com",
				"1700000000",
				"commit message"
			};
			int i = 0;

			Revision revision = RevisionParser.ParseRevision(lines, ref i);

			Assert.NotNull(revision);
			Assert.Equal(SampleSha40, revision.Sha.ToString());
			Assert.Equal("alice", revision.Author.Name);
			Assert.Equal("alice@example.com", revision.Author.Email);
			Assert.Equal(
				DateTimeHelper.UnixStartTime.AddSeconds(1700000000).ToLocalTime(),
				revision.AuthorDate);
			revision.MessageParts(out string subject, out string description);
			Assert.Equal("commit message", subject);
			Assert.Equal(string.Empty, description);
			Assert.Equal(5, i);
		}

		[Fact]
		public void ParseRevision_BlankShaLine_KeepsNullShaAndParsesRemainingFields()
		{
			string[] lines =
			{
				"",
				"bob",
				"bob@example.com",
				"1700000000",
				"empty sha commit"
			};
			int i = 0;

			Revision revision = RevisionParser.ParseRevision(lines, ref i);

			Assert.NotNull(revision);
			Assert.Equal(Sha.NullSha, revision.Sha);
			Assert.Equal("bob", revision.Author.Name);
			Assert.Equal("bob@example.com", revision.Author.Email);
			Assert.Equal(5, i);
		}

		[Fact]
		public void ReadChangedFile_TwoFieldLine_ReturnsChangedFileWithPathAndStatus()
		{
			ChangedFile changedFile = RevisionParser.ReadChangedFile(
				"M\tpath/to/file.cs",
				new Submodule[0]);

			Assert.NotNull(changedFile);
			Assert.Equal("path/to/file.cs", changedFile.Path);
			Assert.Equal(StatusType.Modified, changedFile.Status);
		}
	}
}
