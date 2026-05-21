namespace ForkPlus.Git.Commands
{
	public class ReferenceComparer
	{
		private static readonly NaturalStringComparer NaturalStringComparer = NaturalStringComparer.Instance;

		public int Compare(Reference lhs, Reference rhs)
		{
			int num = NaturalStringComparer.Compare(lhs.FullReference, rhs.FullReference);
			if (num != 0)
			{
				return num;
			}
			int num2 = lhs.Sha.CompareTo(rhs.Sha);
			if (num2 != 0)
			{
				return num2;
			}
			if (lhs is LocalBranch localBranch && rhs is LocalBranch localBranch2)
			{
				int num3 = localBranch.IsActive.CompareTo(localBranch2.IsActive);
				if (num3 != 0)
				{
					return num3;
				}
				return Compare(localBranch.UpstreamFullReference, localBranch2.UpstreamFullReference);
			}
			return 0;
		}

		private static int Compare([Null] string lhs, [Null] string rhs)
		{
			if (lhs != null)
			{
				if (rhs != null)
				{
					return lhs.CompareTo(rhs);
				}
				return 1;
			}
			if (rhs != null)
			{
				return -1;
			}
			return 0;
		}
	}
}
