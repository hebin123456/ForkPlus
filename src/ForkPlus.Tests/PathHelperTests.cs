using System.IO;
using Xunit;

namespace ForkPlus.Tests
{
	public class PathHelperTests
	{
		[Theory]
		[InlineData("a/b/c", "a\\b\\c")]
		[InlineData("a\\b/c", "a\\b\\c")]
		public void Normalize_ConvertsSlashesToWindowsSeparators(string input, string expected)
		{
			Assert.Equal(expected, PathHelper.Normalize(input));
		}

		[Theory]
		[InlineData("a\\b\\c", "a/b/c")]
		[InlineData("a/b\\c", "a/b/c")]
		public void NormalizeUnix_ConvertsBackslashesToForwardSlashes(string input, string expected)
		{
			Assert.Equal(expected, PathHelper.NormalizeUnix(input));
		}

		[Fact]
		public void GetRelativePathComponents_ReturnsChildComponents()
		{
			string parent = Path.Combine("C:\\work", "repo");
			string child = Path.Combine(parent, "src", "file.txt");

			Assert.Equal(new[] { "src", "file.txt" }, PathHelper.GetRelativePathComponents(parent, child));
		}

		[Fact]
		public void RelativePathOrFileName_ReturnsRelativePathUnderParent()
		{
			string parent = Path.Combine("C:\\work", "repo");
			string child = Path.Combine(parent, "src", "file.txt");

			Assert.Equal(Path.Combine("src", "file.txt"), PathHelper.RelativePathOrFileName(parent, child));
		}
	}
}
