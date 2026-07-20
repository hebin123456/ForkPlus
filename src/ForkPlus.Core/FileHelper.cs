using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

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

		public static void OpenInWindowsExplorer(string absolutePath)
		{
			try
			{
				if (File.Exists(absolutePath))
				{
					string arguments = "/select, \"" + absolutePath + "\"";
					Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
				}
				else if (Directory.Exists(absolutePath))
				{
					Process.Start(new ProcessStartInfo("explorer.exe", absolutePath) { UseShellExecute = true });
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
				MoveFileEx(tempFileName, filePath, MoveFileFlags.ReplaceExisting | MoveFileFlags.CopyAllowed | MoveFileFlags.WriteThrough);
			}
			catch (Exception)
			{
				File.Delete(tempFileName);
				throw;
			}
		}

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool MoveFileEx([In] string lpExistingFileName, [In] string lpNewFileName, [In] MoveFileFlags dwFlags);
	}
}
