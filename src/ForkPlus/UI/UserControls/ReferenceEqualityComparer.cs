using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class ReferenceEqualityComparer : IEqualityComparer<Reference>
	{
		public bool Equals(Reference x, Reference y)
		{
			if (x.FullReference == y.FullReference && x.Sha == y.Sha && IsActive(x) == IsActive(y))
			{
				return Upstream(x) == Upstream(y);
			}
			return false;
		}

		public int GetHashCode(Reference obj)
		{
			return 17 + obj.FullReference.GetHashCode() * 31 + obj.Sha.GetHashCode() * 31 + IsActive(obj).GetHashCode() * 31 + Upstream(obj).GetHashCode() * 31;
		}

		private static bool IsActive(Reference reference)
		{
			return (reference as LocalBranch)?.IsActive ?? false;
		}

		private static string Upstream(Reference reference)
		{
			return (reference as LocalBranch)?.UpstreamFullReference ?? string.Empty;
		}
	}
}
