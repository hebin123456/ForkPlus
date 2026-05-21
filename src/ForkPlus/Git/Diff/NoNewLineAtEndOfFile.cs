using System;

namespace ForkPlus.Git.Diff
{
	[Flags]
	public enum NoNewLineAtEndOfFile
	{
		None = 0,
		Deleted = 1,
		Added = 2,
		Context = 4
	}
}
