using ForkPlus.Git.Diff.Parsing.Tokens;
using Xunit;

namespace ForkPlus.Tests
{
	public class DiffStringHelperTests
	{
		[Fact]
		public void LineRanges_SingleLineWithoutNewline_ReturnsSingleRange()
		{
			ForkPlus.Range[] ranges = "hello".LineRanges();

			Assert.Single(ranges);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(5, ranges[0].End);
		}

		[Fact]
		public void LineRanges_TwoLines_ReturnsTwoRangesWithNewlineInFirst()
		{
			ForkPlus.Range[] ranges = "a\nb".LineRanges();

			Assert.Equal(2, ranges.Length);
			// First range covers "a\n" (End includes the newline).
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(2, ranges[0].End);
			// Second range covers "b" (no trailing newline).
			Assert.Equal(2, ranges[1].Start);
			Assert.Equal(3, ranges[1].End);
		}

		[Fact]
		public void LineRanges_TrailingNewline_ExcludesEmptyLineByDefault()
		{
			ForkPlus.Range[] ranges = "a\n".LineRanges(includeEmptyLine: false);

			Assert.Single(ranges);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(2, ranges[0].End);
		}

		[Fact]
		public void LineRanges_TrailingNewline_IncludesEmptyLineWhenRequested()
		{
			ForkPlus.Range[] ranges = "a\n".LineRanges(includeEmptyLine: true);

			Assert.Equal(2, ranges.Length);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(2, ranges[0].End);
			// Trailing empty range collapses to a zero-length range at the end.
			Assert.Equal(2, ranges[1].Start);
			Assert.Equal(2, ranges[1].End);
			Assert.True(ranges[1].IsEmpty);
		}

		[Fact]
		public void LineRanges_EmptyString_ReturnsEmptyArray()
		{
			// The loop never executes and num == num2, so nothing is appended.
			ForkPlus.Range[] ranges = "".LineRanges();

			Assert.Empty(ranges);
		}

		[Fact]
		public void LineRanges_EmptyString_IncludeEmptyLine_ReturnsSingleEmptyRange()
		{
			ForkPlus.Range[] ranges = "".LineRanges(includeEmptyLine: true);

			Assert.Single(ranges);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(0, ranges[0].End);
		}

		[Fact]
		public void LineRanges_ConsecutiveNewlines_ReturnsOneRangePerNewline()
		{
			ForkPlus.Range[] ranges = "\n\n".LineRanges(includeEmptyLine: false);

			Assert.Equal(2, ranges.Length);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(1, ranges[0].End);
			Assert.Equal(1, ranges[1].Start);
			Assert.Equal(2, ranges[1].End);
		}

		[Fact]
		public void LineRanges_ConsecutiveNewlines_IncludesTrailingEmptyRangeWhenRequested()
		{
			ForkPlus.Range[] ranges = "\n\n".LineRanges(includeEmptyLine: true);

			Assert.Equal(3, ranges.Length);
			Assert.Equal(0, ranges[0].Start);
			Assert.Equal(1, ranges[0].End);
			Assert.Equal(1, ranges[1].Start);
			Assert.Equal(2, ranges[1].End);
			Assert.Equal(2, ranges[2].Start);
			Assert.Equal(2, ranges[2].End);
		}
	}
}
