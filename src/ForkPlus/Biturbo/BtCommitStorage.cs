using System;

namespace ForkPlus.Biturbo
{
	public struct BtCommitStorage
	{
		public IntPtr oids;

		public long oids_len;

		public long oids_cap;

		public IntPtr indexes;

		public long indexes_len;

		public long indexes_cap;

		public byte has_more;
	}
}
