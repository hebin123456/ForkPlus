using System;

namespace ForkPlus.Biturbo
{
	public struct BtGitConfigSection
	{
		public IntPtr name;

		public IntPtr sub_section;

		public IntPtr variables;

		public long variables_len;

		public long variables_cap;
	}
}
