using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus
{
	/// <summary>
	/// 文件大小格式化器。
	/// Windows 上优先使用 shlwapi!StrFormatByteSize（与 Windows 资源管理器一致）。
	/// 非 Windows 平台回退到托管实现（KB/MB/GB，按当前区域格式化数字）。
	/// </summary>
	public class FileSizeFormatter
	{
		private static readonly bool s_isWindows =
			System.OperatingSystem.IsWindows();

		public static string Format(long fileSize)
		{
			if (s_isWindows)
			{
				try
				{
					StringBuilder stringBuilder = new StringBuilder(11);
					StrFormatByteSize(fileSize, stringBuilder, stringBuilder.Capacity);
					return stringBuilder.ToString();
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

			return FormatManaged(fileSize);
		}

		/// <summary>
		/// 托管文件大小格式化：按 1024 进制计算 KB/MB/GB/TB，使用当前区域数字格式。
		/// 与 StrFormatByteSize 不同（后者使用 1000 进制），但更符合开发者直觉，
		/// 且在跨平台场景下行为一致。
		/// </summary>
		private static string FormatManaged(long fileSize)
		{
			// 与 Windows 资源管理器一致：使用 1000 进制（KB = 1000 bytes）
			const long KB = 1000L;
			const long MB = 1000L * KB;
			const long GB = 1000L * MB;
			const long TB = 1000L * GB;

			// 使用当前线程文化的数字格式（与 StrFormatByteSize 的本地化行为一致）
			var culture = System.Globalization.CultureInfo.CurrentCulture;

			if (fileSize >= TB)
				return ((double)fileSize / TB).ToString("0.##", culture) + " TB";
			if (fileSize >= GB)
				return ((double)fileSize / GB).ToString("0.##", culture) + " GB";
			if (fileSize >= MB)
				return ((double)fileSize / MB).ToString("0.##", culture) + " MB";
			if (fileSize >= KB)
				return ((double)fileSize / KB).ToString("0.##", culture) + " KB";

			return fileSize.ToString(culture) + " bytes";
		}

		[DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
		private static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);
	}
}
