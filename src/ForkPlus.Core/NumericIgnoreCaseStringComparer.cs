using System;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	public sealed class NumericIgnoreCaseStringComparer : StringComparer
	{
		public static readonly NumericIgnoreCaseStringComparer Comparer = new NumericIgnoreCaseStringComparer();

		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern int StrCmpLogicalW(string psz1, string psz2);

		public override int Compare(string x, string y)
		{
			return StrCmpLogicalW(x, y);
		}

		public override bool Equals(string x, string y)
		{
			return StringComparer.OrdinalIgnoreCase.Equals(x, y);
		}

		public override int GetHashCode(string obj)
		{
			return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
		}
	}
}
