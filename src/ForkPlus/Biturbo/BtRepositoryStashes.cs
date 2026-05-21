using System;

namespace ForkPlus.Biturbo
{
	public struct BtRepositoryStashes
	{
		public IntPtr stashes;

		public long stashes_len;

		public long stashes_cap;

		public IntPtr identities;

		public long identities_len;

		public long identities_cap;
	}
}
