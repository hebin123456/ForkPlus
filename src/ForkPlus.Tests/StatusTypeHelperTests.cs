using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class StatusTypeHelperTests
	{
		[Theory]
		[InlineData('A', StatusType.Added)]
		[InlineData('B', StatusType.Broken)]
		[InlineData('C', StatusType.Copied)]
		[InlineData('D', StatusType.Deleted)]
		[InlineData('!', StatusType.Ignored)]
		[InlineData('M', StatusType.Modified)]
		[InlineData('R', StatusType.Renamed)]
		[InlineData('T', StatusType.TypeChanged)]
		[InlineData('X', StatusType.Unknown)]
		[InlineData('U', StatusType.Unmerged)]
		[InlineData('?', StatusType.Untracked)]
		[InlineData(' ', StatusType.None)]
		public void Parse_MapsKnownCharToStatusType(char statusTypeChar, StatusType expected)
		{
			Assert.Equal(expected, StatusTypeHelper.Parse(statusTypeChar));
		}

		[Theory]
		[InlineData('Z')]
		[InlineData('Y')]
		public void Parse_UnknownCharReturnsNone(char statusTypeChar)
		{
			Assert.Equal(StatusType.None, StatusTypeHelper.Parse(statusTypeChar));
		}
	}
}
