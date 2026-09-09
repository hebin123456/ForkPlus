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
			try
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
					ForkPlusSettings.RepositoryManagerSettings repositoryManagerSettings = ForkPlusSettings.Default.RepositoryManager;
					string[] sourceDirs = repositoryManagerSettings?.SourceDirectories ?? new string[0];
					int scanDepth = repositoryManagerSettings?.ScanDepth ?? 5;
					ForkPlusSettings.RepositoryManagerSettings.Repository[] repositories = repositoryManagerSettings?.Repositories ?? new ForkPlusSettings.RepositoryManagerSettings.Repository[0];
					List<Repository> importedRepositories = new List<Repository>(repositories.Length);
					foreach (ForkPlusSettings.RepositoryManagerSettings.Repository repository in repositories)
					{
						try
						{
							if (repository != null)
							{
								importedRepositories.Add(Import(repository, sourceDirs));
							}
						}
						catch (Exception ex)
						{
							Log.Warn("Failed to import repository manager entry", ex);
						}
					}
					Repository[] repositories2 = importedRepositories.ToArray();
					RepositoryManager repositoryManager = new RepositoryManager(sourceDirs, (byte)scanDepth, new string[0], repositories2);
					try
					{
						repositoryManager.Save();
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to save fallback repository manager", ex);
					}
					return repositoryManager;
				}
				return gitCommandResult.Result ?? Empty();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to initialize repository manager", ex);
				return Empty();
			}
		}

		private static RepositoryManager Empty()
		{
			return new RepositoryManager(new string[0], 5, new string[0], new Repository[0]);
		}

		public RepositoryManager(string[] sourceDirs, byte scanDepth, string[] ignore, Repository[] repositories)
		{
			SourceDirs = NormalizeSourceDirs(sourceDirs);
			ScanDepth = scanDepth;
			Ignore = ignore ?? new string[0];
			Repositories = repositories ?? new Repository[0];
		}

		// v4.0.5：源目录统一以平台分隔符结尾。原实现无条件补 "\\"：
		//   - Windows 行为不变（目录前缀匹配需精确到目录边界，Fork 原始语义）；
		//   - Unix 上 "/home/user/projects\" 是字面上的另一个文件名——RescanUserRepositoriesCommand
		//     的 Directory.GetDirectories 直接 DirectoryNotFoundException（扫描静默找不到任何仓库），
		//     RelativePathFor 的 StartsWith 前缀匹配也全部失配（别名/分类推导失效）。
		//     同时兼容历史数据中已被写成 "\\" 结尾的条目（TrimEnd 后统一补 "/"）。
		private static string[] NormalizeSourceDirs(string[] sourceDirs)
		{
			if (OperatingSystem.IsWindows())
			{
				return (sourceDirs ?? new string[0]).CompactMap((string x) => string.IsNullOrWhiteSpace(x) ? null : ((!x.EndsWith("\\")) ? (x + "\\") : x));
			}
			return (sourceDirs ?? new string[0]).CompactMap(delegate (string x)
			{
				if (string.IsNullOrWhiteSpace(x))
				{
					return null;
				}
				x = x.TrimEnd('\\');
				return (!x.EndsWith("/")) ? (x + "/") : x;
			});
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
			// v4.0.5：Save 的 21 处调用点大多在 UI 事件处理器里，此前对原生库异常（如 ARM
			// 老系统 glibc 低于 libbiturbo.so 所需版本时 NativeLibrary.Load 抛 DllNotFoundException）
			// 零防护——异常直接炸 UI（"无响应崩溃"）且 repositories.toml 一个字节都写不出去，
			// 表现为"ARM 版本没有持久化配置文件"。此处 try-catch + 托管兜底：任何失败都把
			// 状态镜像进 settings.json 的 RepositoryManager 节，而 Load() 的既有回退路径正是
			// 从该节导入，两侧闭环：原生写失败 → settings.json 里有完整数据可恢复。
			try
			{
				BtResult btResult = Bt.bt_save_repository_manager(repositoriesFilePath, sourceDirs, sourceDirs.Length, scanDepth, ignore, ignore.Length, array, array.Length, array2, array2.Length, array3, array3.Length, array4, array4.Length);
				if (btResult != 0)
				{
					Log.Error($"Failed to save repository manager: {btResult}");
					SaveManagedFallback();
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Biturbo native save failed ({repositoriesFilePath}), falling back to managed settings persistence", ex);
				SaveManagedFallback();
			}
		}

		/// <summary>
		/// v4.0.5 托管持久化兜底：把仓库管理器状态镜像进 settings.json 的 RepositoryManager 节。
		/// 触发条件：bt_save_repository_manager 抛异常（典型为 ARM 老系统加载 libbiturbo.so 失败）
		/// 或返回非零错误码。Load() 在原生读取失败时会从同一节导入（旧版迁移逻辑），形成闭环。
		/// 编码遵循 Load.Import 的逆向契约：Name 存 alias（无 alias 时存 RelativePathFor 的
		/// 父目录相对路径，使导入侧 alias 判等回 null）；Opened 的时间戳按 UnixStartTime 精确往返。
		/// </summary>
		internal void SaveManagedFallback()
		{
			try
			{
				List<ForkPlusSettings.RepositoryManagerSettings.Repository> list = new List<ForkPlusSettings.RepositoryManagerSettings.Repository>(Repositories.Length);
				int num = 1;
				Repository[] repositories = Repositories;
				for (int i = 0; i < repositories.Length; i++)
				{
					Repository repository = repositories[i];
					string text = repository.Alias ?? RelativePathFor(repository.Path, SourceDirs).Item2 ?? PathHelper.GetReadableFileName(repository.Path);
					DateTime lastAccessTime = repository.Opened.HasValue ? DateTimeExtensions.UnixStartTime.AddSeconds((long)repository.Opened.GetValueOrDefault()) : DateTime.MinValue;
					list.Add(new ForkPlusSettings.RepositoryManagerSettings.Repository(num++, text, repository.Path, null, lastAccessTime, 0, repository.Color));
				}
				ForkPlusSettings.Default.RepositoryManager = new ForkPlusSettings.RepositoryManagerSettings(SourceDirs, new ForkPlusSettings.RepositoryManagerSettings.Category[0], list.ToArray(), ScanDepth);
				ForkPlusSettings.Default.Save();
				Log.Info($"Repository manager persisted to settings.json fallback ({Repositories.Length} repositories)");
			}
			catch (Exception ex)
			{
				Log.Error("Failed to persist repository manager fallback to settings.json", ex);
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
			// v4.0.5：与构造函数共用规范化——此前裸赋值，WelcomeWindow/偏好设置里
			// 传入的无后缀目录与构造路径产生的带后缀目录混存，扫描与前缀匹配行为不一致。
			SourceDirs = NormalizeSourceDirs(sourceDirs);
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
			// v4.0.5：Ignore 条目按平台分隔符拼接——原实现无条件补 "\\"，Unix 上
			// "srcDir/folder\" 与扫描路径（正斜杠）的 StartsWith 永远失配，被忽略目录
			// 会在下次扫描中原样回来。
			string text = OperatingSystem.IsWindows() ? "\\" : "/";
			string[] sourceDirs = SourceDirs;
			foreach (string sourceDir in sourceDirs)
			{
				foreach (string folder in foldersToDelete)
				{
					list.Add(sourceDir + folder + text);
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
			sourceDirs = sourceDirs ?? new string[0];
			foreach (string text in sourceDirs)
			{
				if (!string.IsNullOrEmpty(text) && path.StartsWith(text))
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
