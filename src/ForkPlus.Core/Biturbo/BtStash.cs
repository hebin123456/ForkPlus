using System;

namespace ForkPlus.Biturbo
{
	public struct BtStash
	{
		public int reflog_id;

		public BtOid oid;

		public BtOid first_parent;

		public long author_index;

		public long author_time;

		public IntPtr subject;
	}
}
