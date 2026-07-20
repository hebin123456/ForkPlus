namespace ForkPlus
{
	public static class RepositoryManagerRepositoryExtensions
	{
		public static string Name(this RepositoryManager.Repository r)
		{
			return r.Alias ?? PathHelper.GetReadableFileName(r.Path);
		}

		public static string Folder(this RepositoryManager.Repository r, string[] sourceDirs)
		{
			return RelativePathFor(r.Path, sourceDirs);
		}

		[Null]
		private static string RelativePathFor(string path, string[] sourceDirs)
		{
			foreach (string text in sourceDirs)
			{
				if (path.StartsWith(text))
				{
					string text2 = path.Substring(text.Length).TrimStart('\\');
					int num = text2.LastIndexOf('\\');
					if (num != -1 && num < text2.Length)
					{
						return text2.Substring(0, num);
					}
				}
			}
			return null;
		}
	}
}
