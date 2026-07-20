using System.IO;

namespace ForkPlus.Git.Commands
{
	internal static class CommonDirReader
	{
		internal static string ReadCommonDir(string gitDirectory)
		{
			string path = PathHelper.Combine(gitDirectory, "commondir");
			if (!File.Exists(path))
			{
				return null;
			}
			string text;
			try
			{
				text = File.ReadAllText(path);
			}
			catch
			{
				return null;
			}
			string text2 = text.TrimEnd('\n', '\r');
			if (string.IsNullOrEmpty(text2))
			{
				return null;
			}
			if (Path.IsPathRooted(text2))
			{
				return PathHelper.Normalize(text2);
			}
			return PathHelper.Normalize(Path.GetFullPath(Path.Combine(gitDirectory, text2)));
		}
	}
}
