using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	/// <summary>
	/// Natural-order string comparer (e.g., "file2" &lt; "file10").
	/// On Windows, prefers shlwapi!StrCmpLogicalW for parity with Windows Explorer.
	/// On non-Windows, falls back to a managed natural-sort implementation.
	/// </summary>
	public sealed class NaturalStringComparer : IComparer<string>
	{
		public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

		private static readonly bool s_isWindows =
			System.OperatingSystem.IsWindows();

		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		private static extern int StrCmpLogicalW(string psz1, string psz2);

		public int Compare(string x, string y)
		{
			if (x is null && y is null) return 0;
			if (x is null) return -1;
			if (y is null) return 1;

			if (s_isWindows)
			{
				try
				{
					return StrCmpLogicalW(x, y);
				}
				catch (DllNotFoundException)
				{
					// Fall through to managed implementation if shlwapi is unavailable.
				}
				catch (EntryPointNotFoundException)
				{
					// Fall through to managed implementation if shlwapi is unavailable.
				}
			}

			return NaturalCompareManaged(x, y);
		}

		/// <summary>
		/// 托管自然排序比较：将每个字符串拆分为非数字与数字交替的 token，逐 token 比较。
		/// 数字 token 按其整数值比较（无前导零权重），值相等时前导零更多者排在前。
		/// </summary>
		private static int NaturalCompareManaged(string x, string y)
		{
			int i = 0, j = 0;
			while (i < x.Length && j < y.Length)
			{
				bool xIsDigit = char.IsDigit(x[i]);
				bool yIsDigit = char.IsDigit(y[j]);

				if (xIsDigit && yIsDigit)
				{
					int xStart = i, yStart = j;
					while (i < x.Length && char.IsDigit(x[i])) i++;
					while (j < y.Length && char.IsDigit(y[j])) j++;

					ReadOnlySpan<char> xNum = x.AsSpan(xStart, i - xStart);
					ReadOnlySpan<char> yNum = y.AsSpan(yStart, j - yStart);

					// 去除前导零用于值比较
					int xValStart = 0;
					while (xValStart < xNum.Length - 1 && xNum[xValStart] == '0') xValStart++;
					int yValStart = 0;
					while (yValStart < yNum.Length - 1 && yNum[yValStart] == '0') yValStart++;

					int xValLen = xNum.Length - xValStart;
					int yValLen = yNum.Length - yValStart;

					if (xValLen != yValLen)
						return xValLen < yValLen ? -1 : 1;

					int cmp = xNum.Slice(xValStart, xValLen).SequenceCompareTo(yNum.Slice(yValStart, yValLen));
					if (cmp != 0) return cmp;

					// 值相等：文本更短者（前导零更多）排在前，与 StrCmpLogicalW "01" &lt; "1" 一致
					if (xNum.Length != yNum.Length)
						return xNum.Length < yNum.Length ? -1 : 1;
				}
				else
				{
					int cmp = char.ToLowerInvariant(x[i]).CompareTo(char.ToLowerInvariant(y[j]));
					if (cmp != 0) return cmp;
					i++;
					j++;
				}
			}

			int xRemaining = x.Length - i;
			int yRemaining = y.Length - j;
			return xRemaining.CompareTo(yRemaining);
		}
	}
}
