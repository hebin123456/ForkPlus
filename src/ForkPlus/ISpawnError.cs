using ForkPlus.Git.Commands;

namespace ForkPlus
{
	public interface ISpawnError
	{
		GitCommandError ToGitCommandError();
	}
}
