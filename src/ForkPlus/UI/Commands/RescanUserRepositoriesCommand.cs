using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class RescanUserRepositoriesCommand
	{
		public void Execute(RepositoryManagerUserControl repositoryManagerUserControl)
		{
			byte scanDepth = ForkPlus.RepositoryManager.Instance.ScanDepth;
			string[] sourceDirs = ForkPlus.RepositoryManager.Instance.SourceDirs;
			string[] ignore = ForkPlus.RepositoryManager.Instance.Ignore.Map((string x) => x.TrimEnd("/"));
			List<string> result = new List<string>(128);
			string[] array = sourceDirs;
			foreach (string path in array)
			{
				FindGitRepositoriesRecursive(path, ignore, result, scanDepth);
			}
			Application.Current.Dispatcher.Async(delegate
			{
				ForkPlus.RepositoryManager.Instance.AddRepositories(result);
				ForkPlus.RepositoryManager.Instance.Save();
				repositoryManagerUserControl.Refresh();
			});
		}

		public void Execute(bool reset)
		{
			byte scanDepth = ForkPlus.RepositoryManager.Instance.ScanDepth;
			string[] sourceDirs = ForkPlus.RepositoryManager.Instance.SourceDirs;
			string[] ignore = (reset ? new string[0] : ForkPlus.RepositoryManager.Instance.Ignore);
			List<string> result = new List<string>(128);
			string[] array = sourceDirs;
			foreach (string path in array)
			{
				FindGitRepositoriesRecursive(path, ignore, result, scanDepth);
			}
			Application.Current.Dispatcher.Async(delegate
			{
				if (reset)
				{
					ForkPlus.RepositoryManager.Instance.RemoveAll();
				}
				ForkPlus.RepositoryManager.Instance.AddRepositories(result);
				ForkPlus.RepositoryManager.Instance.Save();
			});
		}

		private void FindGitRepositoriesRecursive(string path, string[] ignore, List<string> result, int maxLevel)
		{
			if (maxLevel <= 0 || ignore.ContainsItem((string x) => path.StartsWith(x)))
			{
				return;
			}
			string[] directories;
			try
			{
				directories = Directory.GetDirectories(path);
				Directory.GetFiles(path);
			}
			catch (Exception ex)
			{
				Log.Error("Cannot read directories in: '" + path + "'", ex);
				return;
			}
			List<string> list = new List<string>(directories.Length);
			if (GitMmUserControl.IsGitMmWorkspace(path))
			{
				result.Add(path);
				return;
			}
			string[] array = directories;
			foreach (string text in array)
			{
				string fileName = Path.GetFileName(text);
				if (!fileName.StartsWith("$"))
				{
					if (fileName == ".git" && new ValidateRepositoryPathGitCommand().Execute(path) == RepositoryValidState.ValidRepository)
					{
						result.Add(path);
						return;
					}
					list.Add(text);
				}
			}
			foreach (string item in list)
			{
				FindGitRepositoriesRecursive(item, ignore, result, maxLevel - 1);
			}
		}
	}
}
