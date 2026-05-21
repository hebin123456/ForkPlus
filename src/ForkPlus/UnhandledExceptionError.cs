using System;
using ForkPlus.Git.Commands;

namespace ForkPlus
{
	public class UnhandledExceptionError : ISpawnError
	{
		public Exception Exception { get; }

		public UnhandledExceptionError(Exception ex)
		{
			Exception = ex;
		}

		public GitCommandError ToGitCommandError()
		{
			return new GitCommandError.UnknownException(Exception);
		}
	}
}
