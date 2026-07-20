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
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			try
			{
				return Path.GetDirectoryName(path);
			}
			catch (ArgumentException)
			{
				// 非法路径形式（如空字符串、非法字符）返回 null，与 null 输入一致
				return null;
			}
		}

		public static string GetReadableFileName(string filepath)
		{
			try
			{
				return Path.GetFileName(filepath);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get file name in path length " + (filepath?.Length ?? 0), ex);
				if (string.IsNullOrEmpty(filepath))
				{
					return filepath;
				}
				string normalized = Normalize(filepath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				int separatorIndex = normalized.LastIndexOf(Path.DirectorySeparatorChar);
				if (separatorIndex >= 0 && separatorIndex + 1 < normalized.Length)
				{
					return normalized.Substring(separatorIndex + 1);
				}
				return normalized.Length > 256 ? normalized.Substring(0, 256) : normalized;
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
			string[] array = GetComparablePathComponents(path1);
			string[] array2 = GetComparablePathComponents(path2);
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

		private static string[] GetComparablePathComponents(string path)
		{
			string normalizedPath = path ?? "";
			try
			{
				normalizedPath = Path.GetFullPath(normalizedPath);
			}
			catch (Exception ex) when (ex is PathTooLongException || ex is NotSupportedException || ex is ArgumentException)
			{
				Log.Warn("Failed to normalize path for comparison '" + path + "': " + ex.Message);
			}
			normalizedPath = Normalize(normalizedPath).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return normalizedPath.Split(new char[2] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
		}
	}
}
