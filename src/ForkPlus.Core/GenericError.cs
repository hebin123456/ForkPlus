using ForkPlus.Git.Commands;

namespace ForkPlus
{
	public class GenericError : ISpawnError
	{
		public string ErrorString { get; }

		public GenericError(string errorString)
		{
			ErrorString = errorString;
		}

		public GitCommandError ToGitCommandError()
		{
			return new GitCommandError.GenericError(ErrorString);
		}
	}
}
