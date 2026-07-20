using System.Diagnostics;

namespace ForkPlus.Git.Commands
{
	[DebuggerDisplay("{SubDomain}")]
	public class SubdomainsToReload
	{
		private SubDomain _subDomain;

		public SubDomain SubDomain
		{
			get
			{
				lock (this)
				{
					return _subDomain;
				}
			}
			set
			{
				lock (this)
				{
					_subDomain = value;
				}
			}
		}

		public SubdomainsToReload(SubDomain subDomain)
		{
			SubDomain = subDomain;
		}

		public bool Contains(SubDomain subdomain)
		{
			return (SubDomain & subdomain) == subdomain;
		}
	}
}
