using System;
using System.IO;

namespace ForkPlus
{
	internal static class Fs
	{
		public static void Write(string path, string content, bool ensureDirectories = false)
		{
			if (ensureDirectories)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
			}
			File.WriteAllText(path, content);
		}

		public static bool EnsureParentDirectory(string path)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				return true;
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to create parent directory for '" + path + "'", ex);
				return false;
			}
		}

		private static bool IsLongPath(string path)
		{
			return path.Length > 255;
		}

		public static void DeleteFile(string path)
		{
			if (IsLongPath(path))
			{
				File.Delete("\\\\?\\" + path);
			}
			else
			{
				File.Delete(path);
			}
		}

		public static void SetAttributes(string path, FileAttributes attributes)
		{
			if (IsLongPath(path))
			{
				File.SetAttributes("\\\\?\\" + path, attributes);
			}
			else
			{
				File.SetAttributes(path, attributes);
			}
		}

		public static bool IsReadOnly(string path)
		{
			try
			{
				return new FileInfo(path).IsReadOnly;
			}
			catch (Exception ex)
			{
				Log.Warn("IsReadOnly", ex);
				return false;
			}
		}
	}
}
