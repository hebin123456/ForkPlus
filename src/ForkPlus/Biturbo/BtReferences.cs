using System;

namespace ForkPlus.Biturbo
{
	public struct BtReferences
	{
		public IntPtr names_data;

		public long names_data_len;

		public long names_data_cap;

		public IntPtr names_offsets;

		public long names_offsets_len;

		public long names_offsets_cap;

		public IntPtr oids;

		public long oids_len;

		public long oids_cap;

		public IntPtr symrefs_data;

		public long symrefs_data_len;

		public long symrefs_data_cap;

		public IntPtr symrefs_offsets;

		public long symrefs_offsets_len;

		public long symrefs_offsets_cap;

		public ulong hash;
	}
}
