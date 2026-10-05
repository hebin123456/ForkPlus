using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// 插件侧的文件尺寸格式化（v5.0.0 插件化架构）。
	/// 与主工程 FileSizeFormatter/FileHelper 的尺寸方法保持同实现（托管跨平台实现，无平台差异），
	/// 独立成份以保证插件工程拆仓后自包含。输出必须与主工程一致（同一 UI 展示口径）。
	/// v5.0.0 拆分后 public：契约（Abstractions）→ 共享组件（Ui）→ 各插件跨工程使用。
	/// </summary>
	public static class PluginSizeFormat
	{
		private static readonly string[] Units = new string[6] { "bytes", "KB", "MB", "GB", "TB", "PB" };

		/// <summary>等价主工程 FileSizeFormatter.Format（跨平台一律走托管实现，插件不做 Windows 专属 P/Invoke）。</summary>
		public static string Format(long fileSize)
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

		/// <summary>等价主工程 FileHelper.GetReadableFileSize。</summary>
		public static string ReadableFileSize(long fileSize, bool addSizeInBytes = true)
		{
			string text = Format(fileSize);
			string text2;
			if (!addSizeInBytes)
			{
				text2 = text;
				if (text2 == null)
				{
					return "";
				}
			}
			else
			{
				text2 = text + " (" + ReadableFileSizeInBytes(fileSize) + ")";
			}
			return text2;
		}

		/// <summary>等价主工程 FileHelper.GetReadableFileSizeInBytes。</summary>
		public static string ReadableFileSizeInBytes(long fileSize)
		{
			NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
			numberFormatInfo.NumberGroupSizes = new int[1] { 3 };
			numberFormatInfo.NumberGroupSeparator = ",";
			NumberFormatInfo numberFormatInfo2 = numberFormatInfo;
			return fileSize.ToString("N0", numberFormatInfo2) + " B";
		}
	}
}
