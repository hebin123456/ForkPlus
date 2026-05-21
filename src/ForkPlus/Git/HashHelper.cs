using System.Collections;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public static class HashHelper
	{
		public static int GetHashCode(ReferenceStorage.UpstreamTrackingReference[] data)
		{
			return ((IStructuralEquatable)data).GetHashCode((IEqualityComparer)EqualityComparer<object>.Default);
		}
	}
}
