using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitStatusParsingTests
	{
		[Theory]
		[InlineData('A', StatusType.Added)]
		[InlineData('M', StatusType.Modified)]
		[InlineData('D', StatusType.Deleted)]
		[InlineData('R', StatusType.Renamed)]
		[InlineData('T', StatusType.TypeChanged)]
		[InlineData('U', StatusType.Unmerged)]
		[InlineData('?', StatusType.Untracked)]
		[InlineData('!', StatusType.Ignored)]
		[InlineData(' ', StatusType.None)]
		public void StatusTypeHelper_ParsesPorcelainStatusCharacters(char input, StatusType expected)
		{
			Assert.Equal(expected, StatusTypeHelper.Parse(input));
		}

		[Theory]
		[InlineData("A", ChangeType.Added)]
		[InlineData("D", ChangeType.Deleted)]
		[InlineData("R", ChangeType.Renamed)]
		[InlineData("C", ChangeType.Copied)]
		[InlineData("M", ChangeType.Modified)]
		[InlineData("U", ChangeType.Unmerged)]
		[InlineData("T", ChangeType.TypeChanged)]
		[InlineData(" ", ChangeType.Unknown)]
		public void ChangeTypeHelper_ParsesChangeTypes(string input, ChangeType expected)
		{
			Assert.Equal(expected, ChangeTypeHelper.Parse(input));
		}
	}
}
