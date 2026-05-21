using System;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public sealed class SubmoduleComparer : IComparer<Submodule>
	{
		private readonly StringComparer _stringComparer;

		public SubmoduleComparer(StringComparer stringComparer)
		{
			_stringComparer = stringComparer;
		}

		public int Compare(Submodule x, Submodule y)
		{
			return _stringComparer.Compare(x.Path, y.Path);
		}
	}
}
