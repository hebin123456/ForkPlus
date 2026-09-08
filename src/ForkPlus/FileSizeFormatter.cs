using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus
{
	public class FileSizeFormatter
	{
		private static readonly string[] Units = new string[6] { "bytes", "KB", "MB", "GB", "TB", "PB" };

		public static string Format(long fileSize)
		{
			// Migration note：StrFormatByteSize（shlwapi.dll）是 Windows 专属。Linux/macOS 无该库，
			// P/Invoke 抛 DllNotFoundException——BinaryContentUserControl（二进制差异视图）、
			// FileHelper、GitLfsProgressHandler（LFS 进度）三条路径都会崩。
			// Unix 用托管等价实现：1024 进制、3 位有效数字（<10 保留 2 位小数、<100 保留 1 位、
			// 其余取整），输出风格与 Windows 一致（"18 bytes" / "1.85 KB" / "1.18 MB"）。
			if (!OperatingSystem.IsWindows())
			{
				return FormatManaged(fileSize);
			}
			StringBuilder stringBuilder = new StringBuilder(11);
			StrFormatByteSize(fileSize, stringBuilder, stringBuilder.Capacity);
			return stringBuilder.ToString();
		}

		internal static string FormatManaged(long fileSize)
		{
			if (fileSize < 1024L)
			{
				return fileSize + " " + Units[0];
			}
			double num = Math.Abs((double)fileSize);
			int num2 = 0;
			while (num >= 1024.0 && num2 < Units.Length - 1)
			{
				num /= 1024.0;
				num2++;
			}
			string format = (num < 10.0) ? "F2" : ((num < 100.0) ? "F1" : "F0");
			return num.ToString(format) + " " + Units[num2];
		}

		[DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
		private static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);
	}
}
