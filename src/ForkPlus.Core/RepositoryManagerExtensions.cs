using System;

namespace ForkPlus
{
	public static class RepositoryManagerExtensions
	{
		public static string DefaultSourceDir(this RepositoryManager repositoryManager, [Null] string fallback = null)
		{
			return repositoryManager.SourceDirs.FirstItem() ?? fallback ?? Environment.ExpandEnvironmentVariables("%userprofile%");
		}

		[Null]
		public static string FindRepositoryName(this RepositoryManager repositoryManager, string path)
		{
			string normalizedPath = PathHelper.Normalize(path);
			int? num = repositoryManager.Repositories.IndexOfItem((RepositoryManager.Repository x) => x.Path == normalizedPath);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				return repositoryManager.Repositories[valueOrDefault].Name();
			}
			return null;
		}
	}
}
