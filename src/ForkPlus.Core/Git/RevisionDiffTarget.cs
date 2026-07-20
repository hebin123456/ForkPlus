using System.Collections.Generic;

namespace ForkPlus.Git
{
	public abstract class RevisionDiffTarget
	{
		public class Revision : RevisionDiffTarget
		{
			public Revision(Sha sha)
				: base(sha)
			{
			}
		}

		public class Range : RevisionDiffTarget
		{
			public Sha OtherSha { get; }

			public Range(Sha sha, Sha otherSha)
				: base(sha)
			{
				OtherSha = otherSha;
			}

			public Range Swap()
			{
				return new Range(OtherSha, base.Sha);
			}
		}

		public class MultipleRevisions : RevisionDiffTarget
		{
			public IReadOnlyList<Sha> AllShas { get; }

			public MultipleRevisions(IReadOnlyList<Sha> shas)
				: base(shas[0])
			{
				AllShas = shas;
			}
		}

		public class WorkingDirectory : RevisionDiffTarget
		{
			public WorkingDirectory(Sha sha)
				: base(sha)
			{
			}
		}

		public Sha Sha { get; }

		public RevisionDiffTarget(Sha sha)
		{
			Sha = sha;
		}
	}
}
