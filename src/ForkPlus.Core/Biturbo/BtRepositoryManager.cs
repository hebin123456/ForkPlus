using System;

namespace ForkPlus.Biturbo
{
	public struct BtRepositoryManager
	{
		public IntPtr source_dirs;

		public long source_dirs_len;

		public long source_dirs_cap;

		public byte scan_depth;

		public IntPtr ignore;

		public long ignore_len;

		public long ignore_cap;

		public IntPtr repositories;

		public long repositories_len;

		public long repositories_cap;
	}
}
