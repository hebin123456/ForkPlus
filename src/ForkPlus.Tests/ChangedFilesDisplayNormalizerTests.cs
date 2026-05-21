using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	public class ChangedFilesDisplayNormalizerTests
	{
		[Theory]
		[InlineData("../b/xxx")]
		[InlineData("..\\b\\xxx")]
		[InlineData("C:\\work\\repo")]
		public void LooksLikeLinkTarget_ReturnsTrueForPathLikeTargets(string value)
		{
			Assert.True(ChangedFilesDisplayNormalizer.LooksLikeLinkTarget(value));
		}

		[Theory]
		[InlineData("")]
		[InlineData("plain text")]
		[InlineData("line1\nline2")]
		public void LooksLikeLinkTarget_ReturnsFalseForNonPathContent(string value)
		{
			Assert.False(ChangedFilesDisplayNormalizer.LooksLikeLinkTarget(value));
		}
	}
}
