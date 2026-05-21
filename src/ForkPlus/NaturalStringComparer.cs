using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	public sealed class NaturalStringComparer : IComparer<string>
	{
		public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern int StrCmpLogicalW(string psz1, string psz2);

		public int Compare(string x, string y)
		{
			return StrCmpLogicalW(x, y);
		}
	}
}
