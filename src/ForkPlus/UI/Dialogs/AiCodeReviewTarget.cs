using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public abstract class AiCodeReviewTarget
	{
		public class Branch : AiCodeReviewTarget
		{
			public Sha Src { get; }

			public Sha Dst { get; }

			public string Name { get; }

			public string FullReference { get; }

			public Branch(Sha mergeBase, ForkPlus.Git.Branch branch)
			{
				Src = mergeBase;
				Dst = branch.Sha;
				Name = branch.Name;
				FullReference = branch.FullReference;
			}
		}

		public class ShaRange : AiCodeReviewTarget
		{
			public Sha Src { get; }

			public Sha Dst { get; }

			public ShaRange(Sha src, Sha dst)
			{
				Src = src;
				Dst = dst;
			}
		}
	}
}
