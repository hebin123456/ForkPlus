using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public static class ShellRequestResultExtensions
	{
		public static GitCommandError ToGitCommandError(this ShellRequestResult self)
		{
			return new GitCommandError.GitError(self);
		}
	}
}
