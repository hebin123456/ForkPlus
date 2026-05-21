using System;

namespace ForkPlus.Biturbo
{
	public struct BtTreeItem
	{
		public ushort kind;

		public IntPtr filename;

		public BtOid treeish;
	}
}
