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
			// Migration note：StrCmpLogicalW（shlwapi.dll）是 Windows 专属"逻辑排序"
			// （数字段按数值比较："2" < "10"）。Linux/macOS 无该库，P/Invoke 直接抛
			// DllNotFoundException（主窗口 RepositoryManager 排序崩溃实证）。
			// Unix 用纯托管等价实现，Windows 保持原生调用（行为一致）。
			// 返回值契约：恒 -1/0/1（StrCmpLogicalW 实际语义）。
			// BinarySearchBy 等调用方按三分支消费比较结果，托管实现曾返回
			// char.CompareTo/string.Compare 的任意差值（如 -3/+49），破坏契约。
			int result;
			if (!OperatingSystem.IsWindows())
			{
				result = CompareLogicalOrdinalIgnoreCase(x, y);
			}
			else
			{
				result = StrCmpLogicalW(x, y);
			}
			return (result < 0) ? (-1) : ((result > 0) ? 1 : 0);
		}

		/// <summary>
		/// Migration note：StrCmpLogicalW 的托管等价实现：大小写不敏感 + 数字段按数值比较。
		/// 语义对齐 Windows 行为：逐段比较，数字段按 ulong 数值（前导零不参与大小，
		/// 数值相等时短段在前），文本段 OrdinalIgnoreCase。
		/// NaturalStringComparer（引用排序等场景）在 Unix 上复用本实现。
		/// </summary>
		internal static int CompareLogicalOrdinalIgnoreCase(string x, string y)
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x == null) return -1;
			if (y == null) return 1;
			int ix = 0, iy = 0;
			while (ix < x.Length && iy < y.Length)
			{
				char cx = x[ix];
				char cy = y[iy];
				bool dx = char.IsDigit(cx);
				bool dy = char.IsDigit(cy);
				if (dx && dy)
				{
					// 提取数字段（跳过前导零）
					int sx = ix, sy = iy;
					while (ix < x.Length && x[ix] == '0') ix++;
					while (iy < y.Length && y[iy] == '0') iy++;
					int ex = ix, ey = iy;
					while (ex < x.Length && char.IsDigit(x[ex])) ex++;
					while (ey < y.Length && char.IsDigit(y[ey])) ey++;
					int lenX = ex - ix;
					int lenY = ey - iy;
					if (lenX != lenY) return lenX < lenY ? -1 : 1;
				int cmp = string.Compare(x, ix, y, iy, lenX, StringComparison.OrdinalIgnoreCase);
				if (cmp != 0) return cmp < 0 ? -1 : 1;
					// 数值相等：前导零多者（更长原始段）在后，与 Explorer 行为一致
					int rawX = ex - sx, rawY = ey - sy;
					if (rawX != rawY) return rawX < rawY ? -1 : 1;
					ix = ex;
					iy = ey;
				}
				else
			{
				int c = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
				if (c != 0) return c < 0 ? -1 : 1;
				ix++;
				iy++;
			}
			}
			if (ix < x.Length) return 1;
			if (iy < y.Length) return -1;
			return 0;
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
