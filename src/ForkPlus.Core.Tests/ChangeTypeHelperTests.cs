using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class ChangeTypeHelperTests
	{
		[Theory]
		[InlineData("A", ChangeType.Added)]
		[InlineData("D", ChangeType.Deleted)]
		[InlineData("R", ChangeType.Renamed)]
		[InlineData("C", ChangeType.Copied)]
		[InlineData("M", ChangeType.Modified)]
		[InlineData("U", ChangeType.Unmerged)]
		[InlineData("T", ChangeType.TypeChanged)]
		[InlineData("X", ChangeType.Unknown)]
		[InlineData("Z", ChangeType.Unknown)]
		[InlineData("", ChangeType.Unknown)]
		public void Parse_MapsSingleLetterPrefixToChangeType(string text, ChangeType expected)
		{
			Assert.Equal(expected, ChangeTypeHelper.Parse(text));
		}

		[Theory]
		[InlineData("Added", ChangeType.Added)]
		[InlineData("Deleted", ChangeType.Deleted)]
		[InlineData("Renamed", ChangeType.Renamed)]
		[InlineData("Copied", ChangeType.Copied)]
		[InlineData("Modified", ChangeType.Modified)]
		[InlineData("Unmerged", ChangeType.Unmerged)]
		[InlineData("TypeChanged", ChangeType.TypeChanged)]
		public void Parse_OnlyConsidersFirstLetter(string text, ChangeType expected)
		{
			Assert.Equal(expected, ChangeTypeHelper.Parse(text));
		}
	}
}
