using ForkPlus.Git.Interaction;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitCommandTests
	{
		[Fact]
		public void ArgumentsString_JoinsArgumentsInOrder()
		{
			var command = new GitCommand("status", "--porcelain");
			command.Add("-z");

			Assert.Equal("status --porcelain -z", command.ArgumentsString);
			Assert.Equal(new[] { "status", "--porcelain", "-z" }, command.ToArray());
		}

		[Fact]
		public void IsEmpty_IsFalseAfterAddingArgument()
		{
			var command = new GitCommand();

			Assert.True(command.IsEmpty);
			command.Add("status");

			Assert.False(command.IsEmpty);
		}

		[Fact]
		public void CheckLimit_RejectsArgumentThatExceedsLimit()
		{
			var command = new GitCommand(new string('x', Consts.Env.ArgumentLengthLimit));

			Assert.False(command.CheckLimit("y"));
		}
	}
}
