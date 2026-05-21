using System.Diagnostics;
using System.IO;

namespace ForkPlus.Git
{
	[DebuggerDisplay("{RepositoryName}")]
	public class GitModule
	{
		private RepositorySettings _settings;

		public string Path { get; }

		public string CommonGitDir { get; }

		[Null]
		public string WorktreeGitDir { get; }

		[Null]
		public string ParentRepoPath { get; }

		public string RepositoryName { get; }

		[Null]
		public string ParentRepositoryName { get; }

		public ModuleType Type { get; }

		public RepositorySettings Settings
		{
			get
			{
				if (_settings == null)
				{
					_settings = RepositorySettings.Load(this);
				}
				return _settings;
			}
		}

		public string GitModulesFilePath => this.MakePath(".gitmodules");

		public GitModule(string path, string commonGitDir, [Null] string worktreeGitDir, [Null] string parentRepoPath)
		{
			Path = path;
			CommonGitDir = commonGitDir;
			WorktreeGitDir = worktreeGitDir;
			ParentRepoPath = parentRepoPath;
			RepositoryName = System.IO.Path.GetFileName(path);
			if (ParentRepoPath != null)
			{
				ParentRepositoryName = System.IO.Path.GetFileName(ParentRepoPath);
			}
			if (parentRepoPath != null)
			{
				Type = ModuleType.Submodule;
			}
			else if (worktreeGitDir != null)
			{
				Type = ModuleType.Worktree;
			}
			else
			{
				Type = ModuleType.Repository;
			}
		}

		public string GitDir()
		{
			return WorktreeGitDir ?? CommonGitDir;
		}

		public static GitModule Create(string path, string commonGitDir, [Null] string worktreeGitDir, [Null] string parentModulePath)
		{
			if (!Directory.Exists(path))
			{
				return null;
			}
			try
			{
				return new GitModule(path, commonGitDir, worktreeGitDir, parentModulePath);
			}
			catch
			{
				return null;
			}
		}
	}
}
