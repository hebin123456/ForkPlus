using System;

namespace ForkPlus.Biturbo
{
	public struct BtRevisionHeader
	{
		public long author_index;

		public long author_time;

		public IntPtr subject;

		public byte has_body;
	}
}
