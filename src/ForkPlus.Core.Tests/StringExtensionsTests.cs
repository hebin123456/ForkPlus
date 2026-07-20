using System;
using System.Text.RegularExpressions;
using Xunit;

namespace ForkPlus.Tests
{
	public class StringExtensionsTests
	{
		[Theory]
		[InlineData("abcdefg", "abcdefg")]      // exactly 7 chars -> returns full
		[InlineData("abcdefghij", "abcdefg")]   // longer than 7 -> first 7
		[InlineData("abc", "abc")]              // shorter than 7 -> returns full
		[InlineData("", "")]                    // empty -> empty
		public void Abbreviated_DefaultLength7_ReturnsFirst7Chars(string input, string expected)
		{
			Assert.Equal(expected, input.Abbreviated());
		}

		[Theory]
		[InlineData("abcdef", 3, "abc")]     // length shorter than input
		[InlineData("abcdef", 6, "abcdef")]  // length equal to input
		[InlineData("abcdef", 10, "abcdef")] // length longer than input
		[InlineData("abcdef", 0, "")]        // length 0
		public void Abbreviated_CustomLength_ReturnsFirstNChars(string input, int length, string expected)
		{
			Assert.Equal(expected, input.Abbreviated(length));
		}

		[Theory]
		[InlineData(null, "\"\"")]                      // null -> empty quoted (handled via ?? "")
		[InlineData("", "\"\"")]                        // empty -> empty quoted
		[InlineData("hello", "\"hello\"")]              // no inner quotes
		[InlineData("a\"b", "\"a\\\"b\"")]              // contains one inner quote
		[InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]// contains multiple inner quotes
		public void Quotify_WrapsInQuotesAndEscapesInnerQuotes(string input, string expected)
		{
			Assert.Equal(expected, input.Quotify());
		}

		[Theory]
		[InlineData("hello", "hello")]                  // no special chars
		[InlineData("", "")]                            // empty
		[InlineData("a b", "a\\ b")]                    // space
		[InlineData("a(b", "a\\(b")]                    // open paren
		[InlineData("a)b", "a\\)b")]                    // close paren
		[InlineData("a$b", "a\\$b")]                    // dollar
		[InlineData("a b(c)d$e", "a\\ b\\(c\\)d\\$e")]  // combination
		public void EscapeSpaces_EscapesSpacesParensAndDollar(string input, string expected)
		{
			Assert.Equal(expected, input.EscapeSpaces());
		}

		[Theory]
		[InlineData("hello", "hello")]                  // no quotes
		[InlineData("", "")]                            // empty
		[InlineData("a\"b", "a\\\"b")]                  // single inner quote
		[InlineData("\"a\"b\"", "\\\"a\\\"b\\\"")]      // multiple inner quotes
		public void EscapeQuotes_EscapesDoubleQuotes(string input, string expected)
		{
			Assert.Equal(expected, input.EscapeQuotes());
		}

		[Theory]
		[InlineData("hello", 1, 4, "ell")]   // middle slice
		[InlineData("hello", 0, 5, "hello")] // full string
		[InlineData("hello", 0, 0, "")]      // empty range at start
		[InlineData("hello", 2, 2, "")]      // empty range in middle
		public void Substring_WithRange_ReturnsSubstring(string input, int start, int end, string expected)
		{
			Assert.Equal(expected, input.Substring(new Range(start, end)));
		}

		[Theory]
		[InlineData("hello", 1, 4, "XY", "hXYo")]  // replace middle
		[InlineData("hello", 0, 5, "hi", "hi")]    // replace whole string
		[InlineData("hello", 0, 0, "X", "Xhello")] // insert at start (empty range)
		[InlineData("hello", 5, 5, "X", "helloX")] // insert at end (empty range)
		[InlineData("hello", 1, 4, "", "ho")]      // remove without insertion
		public void Replace_WithRange_ReplacesSubstring(string input, int start, int end, string value, string expected)
		{
			Assert.Equal(expected, input.Replace(new Range(start, end), value));
		}

		[Theory]
		[InlineData("hello", "he", "llo")]     // matches prefix
		[InlineData("hello", "xy", "hello")]   // no match
		[InlineData("", "a", "")]              // empty input
		[InlineData("hello", "", "hello")]     // empty prefix
		[InlineData("hello", "hello", "")]     // prefix equals input
		public void TrimStart_RemovesPrefixWhenPresent(string input, string prefix, string expected)
		{
			Assert.Equal(expected, input.TrimStart(prefix));
		}

		[Theory]
		[InlineData("hello", "lo", "hel")]     // matches suffix
		[InlineData("hello", "xy", "hello")]   // no match
		[InlineData("", "a", "")]              // empty input
		[InlineData("hello", "", "hello")]     // empty suffix
		[InlineData("hello", "hello", "")]     // suffix equals input
		public void TrimEnd_RemovesSuffixWhenPresent(string input, string suffix, string expected)
		{
			Assert.Equal(expected, input.TrimEnd(suffix));
		}

		[Theory]
		[InlineData("0123456789abcdef0123456789abcdef01234567", true)] // 40 hex chars (max)
		[InlineData("abcdef0", true)]   // 7 hex chars
		[InlineData("abcde", true)]     // 5 hex chars (min)
		[InlineData("AbCdEf", true)]    // mixed case, 6 chars
		[InlineData("12345", true)]     // digits only
		[InlineData("abcd", false)]     // 4 chars (too short)
		[InlineData("0123456789abcdef0123456789abcdef012345678", false)] // 41 chars (too long)
		[InlineData("abcdeg", false)]   // contains non-hex 'g'
		[InlineData("", false)]         // empty
		public void IsSha_DetectsValidAndInvalidShas(string line, bool expected)
		{
			Assert.Equal(expected, StringHelper.IsSha(line));
		}

		[Fact]
		public void IsSha_Null_ThrowsNullReferenceException()
		{
			// IsSha dereferences line.Length without a null guard, so null throws.
			Assert.Throws<NullReferenceException>(() => StringHelper.IsSha(null));
		}

		[Fact]
		public void FirstMatch_ReturnsFirstMatchWhenFound()
		{
			Regex regex = new Regex(@"\d+");
			Match match = regex.FirstMatch("abc123def456");
			Assert.NotNull(match);
			Assert.Equal("123", match.Value);
		}

		[Fact]
		public void FirstMatch_ReturnsNullWhenNoMatch()
		{
			Regex regex = new Regex(@"\d+");
			Assert.Null(regex.FirstMatch("abcdef"));
		}

		[Fact]
		public void FirstMatch_ReturnsNullOnEmptyInput()
		{
			Regex regex = new Regex(@"\d+");
			Assert.Null(regex.FirstMatch(""));
		}
	}
}
