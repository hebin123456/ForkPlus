using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class RevisionSelector
	{
		public class Head : RevisionSelector
		{
		}

		public class Sha : RevisionSelector
		{
			public IReadOnlyList<ForkPlus.Git.Sha> Shas { get; }

			public Sha(IReadOnlyList<ForkPlus.Git.Sha> shas)
			{
				Shas = shas;
			}

			public Sha(ForkPlus.Git.Sha sha)
				: this(new ForkPlus.Git.Sha[1] { sha })
			{
			}
		}
	}
}
