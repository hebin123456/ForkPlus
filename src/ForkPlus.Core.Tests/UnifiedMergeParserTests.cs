using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Parsing;
using Xunit;

namespace ForkPlus.Tests
{
	public class UnifiedMergeParserTests
	{
		// Minimal valid merge-conflict input. No context lines are required and
		// no "@@@" chunk header is needed: ConflictStart + ConflictSeparator +
		// ConflictEnd parses into a single ConflictChunk, and ParseConflictChunk
		// back-fills the empty local/remote line lists with EmptyLine placeholders.
		private const string MinimalConflict =
			"++<<<<<<< local\n++=======\n++>>>>>>> remote";

		[Fact]
		public void TryParse_MinimalConflictBlock_ReturnsTrueAndResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();

			bool ok = parser.TryParse("file.txt", MinimalConflict, noNewLineAtEndOfFile: false, out MergeConflict result);

			Assert.True(ok);
			Assert.NotNull(result);
			Assert.Equal("file.txt", result.FilePath);
			Assert.False(result.NoNewLineAtEndOfFile);
			Assert.Single(result.Chunks);
			Assert.IsType<MergeConflict.ConflictChunk>(result.Chunks[0]);
		}

		[Fact]
		public void TryParse_MinimalConflictBlock_SetsTrimmedLocalAndRemoteNames()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();

			parser.TryParse("file.txt", MinimalConflict, false, out MergeConflict result);

			MergeConflict.ConflictChunk chunk = (MergeConflict.ConflictChunk)result.Chunks[0];
			// ParseConflictChunk trims the trailing newline from the token names.
			Assert.Equal("local", chunk.LocalName);
			Assert.Equal("remote", chunk.RemoteName);
		}

		[Fact]
		public void TryParse_NoNewLineAtEndOfFileFlag_PreservedOnResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();

			parser.TryParse("file.txt", MinimalConflict, noNewLineAtEndOfFile: true, out MergeConflict result);

			Assert.True(result.NoNewLineAtEndOfFile);
		}

		[Fact]
		public void TryParse_FilePath_PreservedOnResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();

			parser.TryParse("src/path/file.cs", MinimalConflict, false, out MergeConflict result);

			Assert.Equal("src/path/file.cs", result.FilePath);
		}

		[Fact]
		public void TryParse_TokenizerFailure_ReturnsFalseAndNullResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();
			// "++<<<<<<<" is too short for ParseConflictTitle, so the tokenizer
			// returns null and TryParse surfaces a failure.
			bool ok = parser.TryParse("file.txt", "++<<<<<<<", false, out MergeConflict result);

			Assert.False(ok);
			Assert.Null(result);
		}

		[Fact]
		public void TryParse_BareConflictSeparator_ReturnsFalseAndNullResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();
			// A ConflictSeparator with no preceding ConflictStart is an unexpected
			// token for TryParseChunk, so parsing fails.
			bool ok = parser.TryParse("file.txt", "++=======\n", false, out MergeConflict result);

			Assert.False(ok);
			Assert.Null(result);
		}

		[Fact]
		public void TryParse_BareConflictEnd_ReturnsFalseAndNullResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();
			// A ConflictEnd with no preceding ConflictStart is an unexpected token
			// for TryParseChunk, so parsing fails.
			bool ok = parser.TryParse("file.txt", "++>>>>>>> remote\n", false, out MergeConflict result);

			Assert.False(ok);
			Assert.Null(result);
		}

		[Fact]
		public void TryParse_PlainText_ReturnsFalseAndNullResult()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();
			// "hello\n" becomes a single UnknownToken; with no chunk-starting token
			// TryParseChunk exhausts the tokens and returns false.
			bool ok = parser.TryParse("file.txt", "hello\n", false, out MergeConflict result);

			Assert.False(ok);
			Assert.Null(result);
		}

		[Fact]
		public void TryParse_EmptyString_ReturnsTrueWithZeroChunks()
		{
			UnifiedMergeParser parser = new UnifiedMergeParser();
			// The tokenizer returns an empty (not null) array for empty input, so
			// TryParse produces a MergeConflict with zero chunks rather than failing.
			bool ok = parser.TryParse("file.txt", "", false, out MergeConflict result);

			Assert.True(ok);
			Assert.NotNull(result);
			Assert.Empty(result.Chunks);
		}
	}
}
