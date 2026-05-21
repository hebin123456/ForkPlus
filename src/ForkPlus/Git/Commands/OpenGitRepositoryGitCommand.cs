using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class OpenGitRepositoryGitCommand
	{
		public GitCommandResult<GitModule> Execute(GitModule parentModule, Submodule submodule)
		{
			string text = PathHelper.Normalize(parentModule.MakePath(submodule.Path));
			GitCommandResult<(string, string)> gitCommandResult = new ReadGitDirectoryGitCommand().Execute(text);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<GitModule>.Failure(gitCommandResult.Error);
			}
			var (commonGitDir, worktreeGitDir) = gitCommandResult.Result;
			return GitCommandResult<GitModule>.Success(new GitModule(text, commonGitDir, worktreeGitDir, parentModule.Path));
		}

		public GitCommandResult<GitModule> Execute(string path)
		{
			path = PathHelper.Normalize(path);
			if (!Directory.Exists(path))
			{
				return GitCommandResult<GitModule>.Failure(new GitCommandError.NotFound());
			}
			GitCommandResult<(string, string)> gitCommandResult = new ReadGitDirectoryGitCommand().Execute(path);
			if (gitCommandResult.Succeeded)
			{
				var (text, worktreeGitDir) = gitCommandResult.Result;
				if (text.Contains("\\.git\\modules\\"))
				{
					string parentModulePath = GetParentModulePath(path);
					return GitCommandResult<GitModule>.Success(new GitModule(path, text, worktreeGitDir, parentModulePath));
				}
				return GitCommandResult<GitModule>.Success(GitModule.Create(path, text, worktreeGitDir, null));
			}
			return OpenRepositoryFallback(path);
		}

		private static GitCommandResult<GitModule> OpenRepositoryFallback(string path)
		{
			GitRequestResult gitRequestResult = default(GitRequest).CurrentDir(path).Command("rev-parse", "--show-toplevel", "--absolute-git-dir").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				GitCommandError.UnsafeRepository unsafeRepository = GitCommandError.UnsafeRepository.Test(gitRequestResult, path);
				if (unsafeRepository != null)
				{
					return GitCommandResult<GitModule>.Failure(unsafeRepository);
				}
				return GitCommandResult<GitModule>.Failure(new GitCommandError.NotFound());
			}
			if (!ParseRepositoryDirectories(gitRequestResult.Stdout, out var root, out var gitDirectory))
			{
				Log.Error("Cannot parse repository directories in '" + gitRequestResult.Stdout + "'");
				return GitCommandResult<GitModule>.Failure(new GitCommandError.NotFound());
			}
			string parentModulePath = GetParentModulePath(root);
			string text = CommonDirReader.ReadCommonDir(gitDirectory);
			string commonGitDir;
			string worktreeGitDir;
			if (text != null)
			{
				commonGitDir = text;
				worktreeGitDir = gitDirectory;
			}
			else
			{
				commonGitDir = gitDirectory;
				worktreeGitDir = null;
			}
			return GitCommandResult<GitModule>.Success(new GitModule(root, commonGitDir, worktreeGitDir, parentModulePath));
		}

		[Null]
		private static string GetParentModulePath(string repositoryRoot)
		{
			string directoryName = Path.GetDirectoryName(repositoryRoot);
			if (directoryName == null)
			{
				return null;
			}
			GitRequestResult gitRequestResult = default(GitRequest).CurrentDir(directoryName).Command("rev-parse", "--show-toplevel", "--absolute-git-dir").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return null;
			}
			if (!ParseRepositoryDirectories(gitRequestResult.Stdout, out var root, out var _))
			{
				return null;
			}
			string gitModulesFile = Path.Combine(root, ".gitmodules");
			GitCommandResult<Submodule[]> gitCommandResult = new GetSubmodulesGitCommand().Execute(gitModulesFile);
			if (!gitCommandResult.Succeeded)
			{
				return null;
			}
			string text = PathHelper.NormalizeUnix(repositoryRoot);
			Submodule[] result = gitCommandResult.Result;
			foreach (Submodule submodule in result)
			{
				if (text.EndsWith(submodule.Path))
				{
					return root;
				}
			}
			return null;
		}

		private static bool ParseRepositoryDirectories(string revParseOutput, out string root, out string gitDirectory)
		{
			string[] array = revParseOutput.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 2)
			{
				root = null;
				gitDirectory = null;
				return false;
			}
			root = PathHelper.Normalize(array[0]);
			gitDirectory = PathHelper.Normalize(array[1]);
			return true;
		}
	}
}
