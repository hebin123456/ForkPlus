using System;
using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class FuzzySearchTests
	{
		[Theory]
		[InlineData("hello", "hello", true)]
		[InlineData("axbxc", "abc", true)]
		[InlineData("Hello World", "hlo", true)]
		[InlineData("HELLO", "hlo", true)]
		[InlineData("hello", "xyz", false)]
		[InlineData("hello", "", true)]
		[InlineData("", "", true)]
		[InlineData("", "a", false)]
		public void HasFuzzyMatch_VariousCases(string target, string substring, bool expected)
		{
			Assert.Equal(expected, target.HasFuzzyMatch(substring));
		}

		[Fact]
		public void HasFuzzyMatch_SubsequenceAcrossString()
		{
			Assert.True("axbxc".HasFuzzyMatch("abc"));
			Assert.False("axbxc".HasFuzzyMatch("abd"));
		}

		[Fact]
		public void HasFuzzyMatch_CaseInsensitive()
		{
			Assert.True("HelloWorld".HasFuzzyMatch("hlo"));
			Assert.True("HELLOWORLD".HasFuzzyMatch("hlo"));
		}

		[Fact]
		public void Match_EqualLengthReturnsScoreMax()
		{
			Assert.Equal(FuzzySearch.SCORE_MAX, "abc".Match("abc"));
		}

		[Fact]
		public void Match_SubsequenceWithGapsIsPositiveButLowerThanMax()
		{
			double score = "axbxc".Match("abc");
			Assert.True(score > 0.0);
			Assert.True(score < FuzzySearch.SCORE_MAX);
		}

		[Fact]
		public void Match_LargeGapScoresLowerThanSmallGap()
		{
			double smallGap = "abcde".Match("abc");
			double largeGap = "axbxc".Match("abc");
			Assert.True(largeGap < smallGap);
		}

		[Fact]
		public void Match_EmptyNeedleReturnsScoreMin()
		{
			Assert.Equal(FuzzySearch.SCORE_MIN, "abc".Match(""));
		}

		[Fact]
		public void Match_NullNeedleReturnsScoreMin()
		{
			Assert.Equal(FuzzySearch.SCORE_MIN, "abc".Match(null));
		}

		[Fact]
		public void Match_NoMatchReturnsScoreMin()
		{
			Assert.Equal(FuzzySearch.SCORE_MIN, "abcdef".Match("axd"));
		}

		[Fact]
		public void FuzzyFilter_FiltersAndSortsByScoreDescending()
		{
			var entries = new List<string> { "README.md", "readme.txt", "config.json" };
			var result = entries.FuzzyFilter("read", s => s);
			Assert.Equal(2, result.Count);
			Assert.Equal("README.md", result[0]);
			Assert.Equal("readme.txt", result[1]);
		}

		[Fact]
		public void FuzzyFilter_EmptyFilterReturnsAllEntries()
		{
			var entries = new List<string> { "a", "b", "c" };
			var result = entries.FuzzyFilter("", s => s);
			Assert.Equal(3, result.Count);
		}

		[Fact]
		public void FuzzyFilter_NullFilterReturnsAllEntries()
		{
			var entries = new List<string> { "a", "b", "c" };
			var result = entries.FuzzyFilter(null, s => s);
			Assert.Equal(3, result.Count);
		}

		[Fact]
		public void FuzzyFilter_NoMatchReturnsEmpty()
		{
			var entries = new List<string> { "abc", "def" };
			var result = entries.FuzzyFilter("xyz", s => s);
			Assert.Empty(result);
		}

		[Fact]
		public void FuzzyFilter_NullSecondarySelectorWorks()
		{
			var entries = new List<string> { "README.md", "config.json" };
			var result = entries.FuzzyFilter("read", s => s, null);
			Assert.Single(result);
			Assert.Equal("README.md", result[0]);
		}

		[Fact]
		public void FuzzyFilter_UsesSecondarySelectorWhenPrimaryDoesNotMatch()
		{
			var entries = new List<string> { "file1.txt", "file2.txt" };
			var result = entries.FuzzyFilter("read", s => s, s => "readme-" + s);
			Assert.Equal(2, result.Count);
		}
	}
}
