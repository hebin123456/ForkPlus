using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;

namespace ForkPlus
{
	public class RepositoryManager
	{
		[DebuggerDisplay("{Path}")]
		public struct Repository
		{
			public string Path { get; }

			[Null]
			public string Alias { get; }

			public int? Opened { get; }

			public RepositoryColor Color { get; }

			public Repository(string normalizedPath, [Null] string alias, int? opened, RepositoryColor color)
			{
				Alias = alias;
				Path = normalizedPath;
				Opened = opened;
				Color = color;
			}
		}

		public static readonly RepositoryManager Instance = Load();

		public string[] SourceDirs { get; private set; }

		public byte ScanDepth { get; private set; }

		public Repository[] Repositories { get; private set; }

		public string[] Ignore { get; private set; }

		public static RepositoryManager Load()
		{
			string repositoriesTomlPath = App.RepositoriesFilePath;
			GitCommandResult<RepositoryManager> gitCommandResult = BtRequest.Run(() => default(BtRepositoryManager), delegate(ref BtRepositoryManager x)
			{
				return Bt.bt_get_repository_manager(repositoriesTomlPath, ref x);
			}, delegate(ref BtRepositoryManager x)
			{
				return x.Into();
			}, delegate(ref BtRepositoryManager x)
			{
				Bt.bt_release_repository_manager(ref x);
			});
			if (!gitCommandResult.Succeeded)
			{
				Log.Warn("Failed to read '" + repositoriesTomlPath + "':\n" + gitCommandResult.Error.FriendlyDescription);
				string[] sourceDirs = ForkPlusSettings.Default.RepositoryManager.SourceDirectories;
				int scanDepth = ForkPlusSettings.Default.RepositoryManager.ScanDepth;
				ForkPlusSettings.RepositoryManagerSettings.Repository[] repositories = ForkPlusSettings.Default.RepositoryManager.Repositories;
				sourceDirs.Map((string x) => PathHelper.NormalizeUnix(x));
				Repository[] repositories2 = repositories.Map((ForkPlusSettings.RepositoryManagerSettings.Repository r) => Import(r, sourceDirs));
				RepositoryManager repositoryManager = new RepositoryManager(sourceDirs, (byte)scanDepth, new string[0], repositories2);
				repositoryManager.Save();
				return repositoryManager;
			}
			return gitCommandResult.Result;
		}

		public RepositoryManager(string[] sourceDirs, byte scanDepth, string[] ignore, Repository[] repositories)
		{
			SourceDirs = sourceDirs.Map((string x) => (!x.EndsWith("\\")) ? (x + "\\") : x);
			ScanDepth = scanDepth;
			Ignore = ignore;
			Repositories = repositories;
		}

		public void Save()
		{
			Directory.CreateDirectory(App.ForkDataDirectoryPath);
			string repositoriesFilePath = App.RepositoriesFilePath;
			string[] sourceDirs = SourceDirs;
			byte scanDepth = ScanDepth;
			string[] ignore = Ignore;
			string[] array = Repositories.Map((Repository x) => x.Path);
			string[] array2 = Repositories.Map((Repository x) => x.Alias ?? "");
			uint[] array3 = Repositories.Map((Repository x) => (uint)x.Opened.GetValueOrDefault());
			byte[] array4 = Repositories.Map((Repository x) => (byte)x.Color);
			BtResult btResult = Bt.bt_save_repository_manager(repositoriesFilePath, sourceDirs, sourceDirs.Length, scanDepth, ignore, ignore.Length, array, array.Length, array2, array2.Length, array3, array3.Length, array4, array4.Length);
			if (btResult != 0)
			{
				Log.Error($"Failed to save repository manager: {btResult}");
			}
		}

		public void AddRepositories(IReadOnlyList<string> paths)
		{
			List<Repository> list = new List<Repository>(Repositories);
			foreach (string path in paths)
			{
				string normalizedPath = PathHelper.Normalize(path);
				if (!list.ContainsItem((Repository x) => x.Path == normalizedPath))
				{
					list.Add(new Repository(normalizedPath, null, null, RepositoryColor.None));
				}
			}
			Repositories = list.ToArray();
		}

		public Repository AddOrUpdateLastOpened(GitModule gitModule)
		{
			return AddOrUpdateLastOpened(gitModule.Path);
		}

		public Repository AddOrUpdateLastOpened(string path)
		{
			string normalizedPath = PathHelper.Normalize(path);
			int? num = Repositories.IndexOfItem((Repository x) => x.Path == normalizedPath);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				Repository repository = Repositories[valueOrDefault];
				Repositories[valueOrDefault] = new Repository(repository.Path, repository.Alias, DateTime.Now.TimeIntervalSince1970(), repository.Color);
				return Repositories[valueOrDefault];
			}
			Repository repository2 = new Repository(normalizedPath, null, DateTime.Now.TimeIntervalSince1970(), RepositoryColor.None);
			Repository[] array = new Repository[Repositories.Length + 1];
			Array.Copy(Repositories, array, Repositories.Length);
			array[array.Length - 1] = repository2;
			Repositories = array;
			return repository2;
		}

		public void SetSourceDirs(string[] sourceDirs)
		{
			SourceDirs = sourceDirs;
		}

		public void RemoveAll()
		{
			Ignore = new string[0];
			Repositories = new Repository[0];
		}

		public void RenameRepository(string path, string newName)
		{
			string normalizedPath = PathHelper.Normalize(path);
			int? num = Repositories.IndexOfItem((Repository x) => x.Path == normalizedPath);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				string alias = ((RelativePathFor(normalizedPath, SourceDirs).Item2 == newName) ? null : newName);
				Repository repository = Repositories[valueOrDefault];
				Repositories[valueOrDefault] = new Repository(repository.Path, alias, repository.Opened, repository.Color);
			}
		}

		public void UpdateRepositoryColor(string path, RepositoryColor color)
		{
			string normalizedPath = PathHelper.Normalize(path);
			int? num = Repositories.IndexOfItem((Repository x) => x.Path == normalizedPath);
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				Repository repository = Repositories[valueOrDefault];
				Repositories[valueOrDefault] = new Repository(repository.Path, repository.Alias, repository.Opened, color);
			}
		}

		public void DeleteRepositories(string[] repositoriesToDelete, bool addToIgnore = true)
		{
			HashSet<string> pathsToRemove = new HashSet<string>(repositoriesToDelete);
			Repositories = Repositories.Filter((Repository r) => !pathsToRemove.Contains(r.Path)).ToArray();
			if (addToIgnore)
			{
				HashSet<string> hashSet = new HashSet<string>(Ignore);
				foreach (string item in repositoriesToDelete)
				{
					hashSet.Add(item);
				}
				Ignore = hashSet.ToArray();
			}
		}

		public void DeleteFolders(string[] foldersToDelete)
		{
			List<string> list = new List<string>();
			string[] sourceDirs = SourceDirs;
			foreach (string text in sourceDirs)
			{
				foreach (string text2 in foldersToDelete)
				{
					list.Add(text + text2 + "\\");
				}
			}
			HashSet<string> hashSet = new HashSet<string>(Ignore);
			List<string> repositoriesToDelete = new List<string>();
			Repository[] repositories = Repositories;
			for (int i = 0; i < repositories.Length; i++)
			{
				Repository repo = repositories[i];
				string text3 = IReadOnlyListExtensions.FirstItem(list, (string x) => repo.Folder(SourceDirs) != null && repo.Path.StartsWith(x));
				if (text3 != null)
				{
					hashSet.Add(text3);
					repositoriesToDelete.Add(repo.Path);
				}
			}
			Repositories = Repositories.Filter((Repository r) => !repositoriesToDelete.Contains(r.Path)).ToArray();
			Ignore = hashSet.ToArray();
		}

		private static Repository Import(ForkPlusSettings.RepositoryManagerSettings.Repository repository, string[] sourceDirs)
		{
			string name = repository.Name;
			string item = RelativePathFor(PathHelper.NormalizeUnix(repository.Path), sourceDirs).Item2;
			int value = ((!(repository.LastAccessTime == DateTime.MinValue)) ? repository.LastAccessTime.TimeIntervalSince1970() : 0);
			string alias = ((name == item) ? null : name);
			return new Repository(repository.Path, alias, value, repository.Color);
		}

		public static (string, string) RelativePathFor(string path, string[] sourceDirs)
		{
			foreach (string text in sourceDirs)
			{
				if (path.StartsWith(text))
				{
					string text2 = path.Substring(text.Length).TrimStart('/');
					int num = text2.LastIndexOf('/');
					if (num != -1 && num < text2.Length)
					{
						string item = text2.Substring(0, num);
						return (text2.Substring(num + 1), item);
					}
				}
			}
			return (null, PathHelper.GetReadableFileName(path));
		}
	}
}
