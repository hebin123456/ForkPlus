using System;

namespace ForkPlus.Biturbo
{
	public struct BtRevisionHeaders
	{
		public IntPtr revisions;

		public long revisions_len;

		public long revisions_cap;

		public IntPtr identities;

		public long identities_len;

		public long identities_cap;
	}
}
