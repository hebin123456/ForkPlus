using System;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	/// <summary>
	/// Case-insensitive natural-order string comparer (e.g., "file2" &lt; "file10").
	/// On Windows, prefers shlwapi!StrCmpLogicalW for parity with Windows Explorer.
	/// On non-Windows, falls back to <see cref="NaturalStringComparer"/>'s managed
	/// implementation. Equality and hashcode use ordinal-ignore-case semantics,
	/// matching the WPF-era behaviour.
	/// </summary>
	public sealed class NumericIgnoreCaseStringComparer : StringComparer
	{
		public static readonly NumericIgnoreCaseStringComparer Comparer = new NumericIgnoreCaseStringComparer();

		private static readonly NaturalStringComparer s_natural = NaturalStringComparer.Instance;

		// Kept for binary/source compatibility with code that may still reference the import.
		// The Windows path is dispatched through NaturalStringComparer, which performs the
		// same P/Invoke (StrCmpLogicalW) on Windows and falls back to managed natural sort
		// on non-Windows.
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern int StrCmpLogicalW(string psz1, string psz2);

		public override int Compare(string x, string y)
		{
			return s_natural.Compare(x, y);
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
