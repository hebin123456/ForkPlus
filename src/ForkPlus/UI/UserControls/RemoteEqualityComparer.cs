using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class RemoteEqualityComparer : IEqualityComparer<Remote>
	{
		public bool Equals(Remote x, Remote y)
		{
			if (x.Name == y.Name && x.Url == y.Url)
			{
				return x.DisableImplicitFetch == y.DisableImplicitFetch;
			}
			return false;
		}

		public int GetHashCode(Remote obj)
		{
			return 17 + obj.Name.GetHashCode() * 31 + obj.Url.GetHashCode() * 31 + obj.DisableImplicitFetch.GetHashCode() * 31;
		}
	}
}
