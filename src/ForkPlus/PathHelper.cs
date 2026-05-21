using System;
using System.IO;

namespace ForkPlus
{
	public static class PathHelper
	{
		public static string Normalize(string path)
		{
			return path.Replace("/", "\\");
		}

		public static string NormalizeUnix(string path)
		{
			return path.Replace("\\", "/");
		}

		public static string[] GetRelativePathComponents(string parentDirectory, string childPath)
		{
			if (!childPath.StartsWith(parentDirectory))
			{
				return new string[0];
			}
			return childPath.Substring(parentDirectory.Length).Split(new char[1] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
		}

		[Null]
		public static string GetParent([Null] string path)
		{
			return Path.GetDirectoryName(path);
		}

		public static string GetReadableFileName(string filepath)
		{
			try
			{
				return Path.GetFileName(filepath);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get file name in '" + filepath + "'", ex);
				return filepath;
			}
		}

		public static string Combine(string path1, string path2)
		{
			return Path.Combine(path1, path2);
		}

		public static string Combine(string path1, string path2, string path3)
		{
			return Path.Combine(path1, path2, path3);
		}

		public static string Combine(string path1, string path2, string path3, string path4)
		{
			return Path.Combine(path1, path2, path3, path4);
		}

		public static bool IsImagePath(string path)
		{
			if (path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return false;
		}

		public static string RelativePathOrFileName(string parent, string absolutePath)
		{
			if (absolutePath.StartsWith(parent) && absolutePath.Length > parent.Length)
			{
				return absolutePath.Substring(parent.Length + 1);
			}
			return Path.GetFileName(absolutePath);
		}

		[Null]
		public static (string, string) FindFirstDifferentComponent(string path1, string path2)
		{
			string[] array = Path.GetFullPath(path1).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
			string[] array2 = Path.GetFullPath(path2).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
			int num = array.Length - 1;
			int num2 = array2.Length - 1;
			while (num >= 0 && num2 >= 0 && string.Equals(array[num], array2[num2], StringComparison.OrdinalIgnoreCase))
			{
				num--;
				num2--;
			}
			string item = ((num >= 0) ? array[num] : null);
			string item2 = ((num2 >= 0) ? array2[num2] : null);
			return (item, item2);
		}
	}
}
