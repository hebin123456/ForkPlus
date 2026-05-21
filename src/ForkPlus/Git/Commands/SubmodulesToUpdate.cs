using System;

namespace ForkPlus.Git.Commands
{
	public struct SubmodulesToUpdate
	{
		public int Length => Submodules.Length;

		public Tuple<Submodule, bool>[] Submodules { get; }

		public SubmodulesToUpdate(Tuple<Submodule, bool>[] submodules)
		{
			Submodules = submodules;
		}
	}
}
