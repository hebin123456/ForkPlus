using System;

namespace ForkPlus.Biturbo
{
	public struct BtSpawnWithOutputResult
	{
		public int status;

		public IntPtr stdout;

		public long stdout_len;

		public long stdout_cap;

		public IntPtr stderr;

		public long stderr_len;

		public long stderr_cap;
	}
}
