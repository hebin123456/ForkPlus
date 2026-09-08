using System;
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
			// Migration note：StrCmpLogicalW（shlwapi.dll）是 Windows 专属"逻辑排序"（数字段按数值
			// 比较："2" < "10"）。Linux/macOS 无该库，P/Invoke 直接抛 DllNotFoundException——
			// 实证：RepositoryReferences.New 排序引用时崩溃，RefreshRepositoryData 整体失败，
			// 主界面永久停在"加载中"（2026-08-30 fork.log 实证）。Unix 走纯托管等价实现，
			// Windows 保持原生调用（与 NumericIgnoreCaseStringComparer 同一处理模式）。
			if (!OperatingSystem.IsWindows())
			{
				return NumericIgnoreCaseStringComparer.CompareLogicalOrdinalIgnoreCase(x, y);
			}
			return StrCmpLogicalW(x, y);
		}
	}
}
