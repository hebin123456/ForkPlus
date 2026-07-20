using System;

namespace ForkPlus.Git
{
	public class RepositorySubmodules
	{
		public Submodule[] Items { get; }

		[Null]
		public DateTime? UpdateTime { get; }

		public RepositorySubmodules(Submodule[] submodules, DateTime? updateTime)
		{
			Items = submodules;
			UpdateTime = updateTime;
		}
	}
}
