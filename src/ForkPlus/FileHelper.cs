using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ForkPlus
{
	internal static class FileHelper
	{
		[Flags]
		private enum MoveFileFlags
		{
			None = 0,
			ReplaceExisting = 1,
			CopyAllowed = 2,
			DelayUntilReboot = 4,
			WriteThrough = 8,
			CreateHardlink = 0x10,
			FailIfNotTrackable = 0x20
		}

		public static long? GetFileSize(string filePath)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(filePath);
				if (fileInfo.Exists)
				{
					return fileInfo.Length;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
			return null;
		}

		public static string GetReadableFileSize(long fileSize, bool addSizeInBytes = true)
		{
			string text = FileSizeFormatter.Format(fileSize);
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
				text2 = text + " (" + GetReadableFileSizeInBytes(fileSize) + ")";
			}
			return text2;
		}

		public static string GetReadableFileSizeInBytes(long fileSize)
		{
			NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
			numberFormatInfo.NumberGroupSizes = new int[1] { 3 };
			numberFormatInfo.NumberGroupSeparator = ",";
			NumberFormatInfo numberFormatInfo2 = numberFormatInfo;
			return fileSize.ToString("N0", numberFormatInfo2) + " B";
		}

		public static bool AtomicWrite(string filepath, string content)
		{
			for (int i = 0; i < 3; i++)
			{
				try
				{
					WriteFile(filepath, content);
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to write to '{filepath}' {i}", ex);
					continue;
				}
				return true;
			}
			return false;
		}

		// 阶段 5：跨平台"在文件管理器中显示"。
		// Windows: explorer.exe /select,<path>
		// Linux:   xdg-open <containing_dir>（xdg-open 不支持 /select 选中，仅打开所在目录）
		// macOS:   open -R <path>（-R 表示在 Finder 中显示并选中）
		public static void OpenInWindowsExplorer(string absolutePath)
		{
			try
			{
				if (OperatingSystem.IsWindows())
				{
					if (File.Exists(absolutePath))
					{
						// explorer /select 语法要求逗号后紧跟路径，中间不能有空格，否则新版 Windows
						// 会忽略 /select 直接打开"文档"库而非选中目标文件。
						string arguments = "/select,\"" + absolutePath + "\"";
						Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
					}
					else if (Directory.Exists(absolutePath))
					{
						Process.Start(new ProcessStartInfo("explorer.exe", absolutePath) { UseShellExecute = true });
					}
				}
				else if (OperatingSystem.IsMacOS())
				{
					// macOS: open -R 在 Finder 中选中文件/目录
					Process.Start(new ProcessStartInfo("open", "-R \"" + absolutePath + "\"") { UseShellExecute = false });
				}
				else if (OperatingSystem.IsLinux())
				{
					// Linux: xdg-open 仅能打开所在目录，无法选中目标文件
					string dir = File.Exists(absolutePath) ? Path.GetDirectoryName(absolutePath)
						: (Directory.Exists(absolutePath) ? absolutePath : null);
					if (dir != null)
					{
						Process.Start(new ProcessStartInfo("xdg-open", "\"" + dir + "\"") { UseShellExecute = false });
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show file in Explorer", ex);
			}
		}

		private static void WriteFile(string filePath, string content)
		{
			string tempFileName = Path.GetTempFileName();
			using (StreamWriter streamWriter = new StreamWriter(tempFileName))
			{
				streamWriter.Write(content);
			}
			try
			{
				// 阶段 5：原子写跨平台化。
				// Windows: Kernel32!MoveFileEx(MOVEFILE_REPLACE_EXISTING) 支持跨卷原子替换
				// Unix:    File.Move 在同卷内原子，跨卷回退到 File.Replace（.NET 7+ 已跨平台支持）
				if (OperatingSystem.IsWindows())
				{
					MoveFileEx(tempFileName, filePath, MoveFileFlags.ReplaceExisting | MoveFileFlags.CopyAllowed | MoveFileFlags.WriteThrough);
				}
				else
				{
					// Unix 下 File.Move 在同文件系统内原子；目标已存在时需先 File.Replace
					if (File.Exists(filePath))
					{
						File.Replace(tempFileName, filePath, destinationBackupFileName: null);
					}
					else
					{
						File.Move(tempFileName, filePath);
					}
				}
			}
			catch (Exception)
			{
				File.Delete(tempFileName);
				throw;
			}
		}

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool MoveFileEx([In] string lpExistingFileName, [In] string lpNewFileName, [In] MoveFileFlags dwFlags);
	}
}
