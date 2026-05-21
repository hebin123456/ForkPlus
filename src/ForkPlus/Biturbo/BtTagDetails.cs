using System;

namespace ForkPlus.Biturbo
{
	public struct BtTagDetails
	{
		public BtOid tag_object_oid;

		public IntPtr tagger_name;

		public IntPtr tagger_email;

		public long tagger_time;

		public IntPtr name;

		public IntPtr message;
	}
}
