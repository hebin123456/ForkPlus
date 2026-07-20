using System.IO;

namespace ForkPlus.Git
{
	public static class GitModuleExtensionsMethods
	{
		public static string FolderName(this GitModule gitModule)
		{
			return PathHelper.GetReadableFileName(gitModule.Path);
		}

		public static string MakePath(this GitModule gitModule, string relativePath)
		{
			return Path.Combine(gitModule.Path, relativePath);
		}

		public static string CommitMessagePath(this GitModule gitModule)
		{
			return Path.Combine(gitModule.GitDir(), "COMMITMESSAGE");
		}

		public static string HooksDirectoryPath(this GitModule gitModule)
		{
			return Path.Combine(gitModule.CommonGitDir, "hooks");
		}

		public static string HookPath(this GitModule gitModule, string hookName)
		{
			return Path.Combine(gitModule.HooksDirectoryPath(), hookName);
		}

		public static string StashFilePath(this GitModule gitModule)
		{
			return PathHelper.Combine(gitModule.CommonGitDir, "logs", "refs", "stash");
		}

		public static string HeadFilePath(this GitModule gitModule)
		{
			return PathHelper.Combine(gitModule.GitDir(), "logs", "HEAD");
		}

		public static string ConfigFilePath(this GitModule gitModule)
		{
			return PathHelper.Combine(gitModule.CommonGitDir, "config");
		}

		public static string ForkPlusSettingsFile(this GitModule gitModule)
		{
			return PathHelper.Combine(gitModule.GitDir(), Consts.ForkPlus.RepositorySettingsFilename);
		}

		public static string WorktreesDirectoryPath(this GitModule gitModule)
		{
			return Path.Combine(gitModule.CommonGitDir, "worktrees");
		}

		public static string SyncPath(this GitModule gitModule)
		{
			return Path.Combine(gitModule.GitDir(), "fork", "sync");
		}
	}
}
