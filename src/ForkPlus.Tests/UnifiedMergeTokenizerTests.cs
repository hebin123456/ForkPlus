using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Parsing.Tokenizer;
using Xunit;

namespace ForkPlus.Tests
{
	public class UnifiedMergeTokenizerTests
	{
		[Fact]
		public void GetTokens_SingleConflictBlock_ReturnsStartSeparatorEndTokens()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			string input = "++<<<<<<< local\n++=======\n++>>>>>>> remote";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			Assert.NotNull(tokens);
			Assert.Equal(3, tokens.Length);
			Assert.IsType<ConflictStartToken>(tokens[0]);
			Assert.IsType<ConflictSeparatorToken>(tokens[1]);
			Assert.IsType<ConflictEndToken>(tokens[2]);
		}

		[Fact]
		public void GetTokens_ConflictStartOnNonFinalLine_LocalNameRetainsTrailingNewline()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// "++<<<<<<< local\n" has length 16; ParseConflictTitle does
			// Substring(10) without trimming, so the trailing '\n' is retained.
			string input = "++<<<<<<< local\n++=======\n++>>>>>>> remote";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			ConflictStartToken startToken = Assert.IsType<ConflictStartToken>(tokens[0]);
			Assert.Equal("local\n", startToken.LocalName);
		}

		[Fact]
		public void GetTokens_ConflictStartOnFinalLine_LocalNameIsExact()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// A single conflict-start line with no trailing newline yields a
			// LocalName without the trailing newline.
			string input = "++<<<<<<< local";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			ConflictStartToken startToken = Assert.IsType<ConflictStartToken>(tokens[0]);
			Assert.Equal("local", startToken.LocalName);
		}

		[Fact]
		public void GetTokens_ConflictEndOnFinalLine_RemoteNameHasNoTrailingNewline()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			string input = "++<<<<<<< local\n++=======\n++>>>>>>> remote";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			ConflictEndToken endToken = Assert.IsType<ConflictEndToken>(tokens[2]);
			Assert.Equal("remote", endToken.RemoteName);
		}

		[Fact]
		public void GetTokens_Diff3Format_IncludesBaseStartToken()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			string input = "++<<<<<<< local\n++||||||| abc123\n++=======\n++>>>>>>> remote";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			Assert.NotNull(tokens);
			Assert.Equal(4, tokens.Length);
			Assert.IsType<ConflictStartToken>(tokens[0]);
			// "++||||||| abc123\n" -> ParseBaseSha does Substring(10), keeps '\n'.
			BaseStartToken baseStart = Assert.IsType<BaseStartToken>(tokens[1]);
			Assert.Equal("abc123\n", baseStart.Sha);
			Assert.IsType<ConflictSeparatorToken>(tokens[2]);
			Assert.IsType<ConflictEndToken>(tokens[3]);
		}

		[Fact]
		public void GetTokens_ContextLineAfterChunkHeader_ReturnsContextToken()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// A ContextToken is only emitted after a "@@@" line has set the flag.
			string input = "@@@ -1,1 +1,1\n  common line";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			Assert.NotNull(tokens);
			Assert.Equal(2, tokens.Length);
			Assert.IsType<UnknownToken>(tokens[0]);
			ContextToken context = Assert.IsType<ContextToken>(tokens[1]);
			Assert.Equal(ContextType.None, context.RemoteType);
			Assert.Equal(ContextType.None, context.LocalType);
			Assert.Equal("common line", context.ContextString);
		}

		[Fact]
		public void GetTokens_ChangeContextLine_ParsesRemoteAndLocalChangeTypes()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// Two-char prefix: first char encodes RemoteType, second char LocalType.
			// "+-" -> RemoteType.Add, LocalType.Remove.
			string input = "@@@ -1,1 +1,1\n+-changed";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			ContextToken context = Assert.IsType<ContextToken>(tokens[1]);
			Assert.Equal(ContextType.Add, context.RemoteType);
			Assert.Equal(ContextType.Remove, context.LocalType);
			Assert.Equal("changed", context.ContextString);
		}

		[Fact]
		public void GetTokens_ContextShapedLineBeforeChunkHeader_ReturnsUnknownToken()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// Without a preceding "@@@" line the context flag is still false, so a
			// context-shaped line falls through to UnknownToken.
			string input = "  common line";

			MergeToken[] tokens = tokenizer.GetTokens(input);

			Assert.NotNull(tokens);
			Assert.Single(tokens);
			Assert.IsType<UnknownToken>(tokens[0]);
		}

		[Fact]
		public void GetTokens_EmptyString_ReturnsEmptyArrayNotNull()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// The main loop never executes for empty input, so an empty array
			// (rather than null) is returned.
			MergeToken[] tokens = tokenizer.GetTokens("");

			Assert.NotNull(tokens);
			Assert.Empty(tokens);
		}

		[Fact]
		public void GetTokens_ConflictStartTooShort_ReturnsNull()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			// "++<<<<<<<" has length 9 (< 10), so ParseConflictTitle fails and the
			// tokenizer signals failure by returning null.
			MergeToken[] tokens = tokenizer.GetTokens("++<<<<<<<");

			Assert.Null(tokens);
		}

		[Fact]
		public void GetTokens_ConflictEndTooShort_ReturnsNull()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			MergeToken[] tokens = tokenizer.GetTokens("++>>>>>>>");

			Assert.Null(tokens);
		}

		[Fact]
		public void GetTokens_BaseStartTooShort_ReturnsNull()
		{
			UnifiedMergeTokenizer tokenizer = new UnifiedMergeTokenizer();
			MergeToken[] tokens = tokenizer.GetTokens("++|||||||");

			Assert.Null(tokens);
		}
	}
}
