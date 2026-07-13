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

		[Fact]
		public void GetRelativePathComponents_ReturnsComponentsUnderRepoParent()
		{
			string parent = "C:\\repo";
			string child = Path.Combine(parent, "src", "file.cs");

			Assert.Equal(new[] { "src", "file.cs" }, PathHelper.GetRelativePathComponents(parent, child));
		}

		[Theory]
		[InlineData("C:\\images\\photo.png", true)]
		[InlineData("assets/photo.jpg", true)]
		[InlineData("C:\\assets\\sprite.BMP", true)]
		[InlineData("C:\\code\\Program.cs", false)]
		public void IsImagePath_DetectsImageExtensions(string path, bool expected)
		{
			Assert.Equal(expected, PathHelper.IsImagePath(path));
		}

		[Fact]
		public void FindFirstDifferentComponent_ReturnsFirstDifferingComponent()
		{
			var result = PathHelper.FindFirstDifferentComponent("C:\\repo\\src\\file.cs", "C:\\repo\\src\\other.cs");

			Assert.Equal("file.cs", result.Item1);
			Assert.Equal("other.cs", result.Item2);
		}

		[Fact]
		public void RelativePathOrFileName_ReturnsRelativePathForRepoChild()
		{
			string parent = "C:\\repo";
			string child = Path.Combine(parent, "src", "file.cs");

			Assert.Equal(Path.Combine("src", "file.cs"), PathHelper.RelativePathOrFileName(parent, child));
		}

		[Fact]
		public void RelativePathOrFileName_ReturnsFileNameWhenNotUnderParent()
		{
			string parent = "C:\\repo";
			string absolute = Path.Combine("D:\\other", "file.cs");

			Assert.Equal("file.cs", PathHelper.RelativePathOrFileName(parent, absolute));
		}

		[Fact]
		public void GetParent_ReturnsParentDirectory()
		{
			string path = Path.Combine("C:\\repo", "src", "file.cs");

			Assert.Equal(Path.Combine("C:\\repo", "src"), PathHelper.GetParent(path));
		}

		[Fact]
		public void GetParent_ReturnsNullForNullOrEmptyPath()
		{
			Assert.Null(PathHelper.GetParent(null));
			Assert.Null(PathHelper.GetParent(""));
		}

		[Fact]
		public void Combine_TwoSegments_ReturnsCombinedPath()
		{
			Assert.Equal(Path.Combine("C:\\repo", "src"), PathHelper.Combine("C:\\repo", "src"));
		}

		[Fact]
		public void Combine_ThreeSegments_ReturnsCombinedPath()
		{
			Assert.Equal(Path.Combine("C:\\repo", "src", "file.cs"), PathHelper.Combine("C:\\repo", "src", "file.cs"));
		}
	}
}
