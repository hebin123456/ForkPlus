using System;
using ForkPlus.Git;

namespace ForkPlus
{
	public class RepositoryDataUpdatedEventArgs : EventArgs
	{
		public RepositoryUserControl RepositoryUserControl { get; }

		public RepositoryData Old { get; }

		public RepositoryData New { get; }

		public RepositoryDataUpdatedEventArgs(RepositoryUserControl repositoryUserControl, RepositoryData old, RepositoryData @new)
		{
			RepositoryUserControl = repositoryUserControl;
			Old = old;
			New = @new;
		}
	}
}
