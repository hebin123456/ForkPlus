using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace ForkPlus.AutoUpdater
{
	/// <summary>
	/// 更新安装器：解压 → 等主程序退出 → 备份替换安装目录 → 重启主程序。
	/// 替换策略：把安装目录现有顶层条目整体移入 backup 目录（原子 move，
	/// Windows 上被占用文件会立刻失败进入重试而非半拷贝状态），再把解压出的
	/// 新文件移入安装目录；任一步失败时回滚（移回 backup），保证安装目录
	/// 不会停留在半新半旧的混合态。
	/// </summary>
	internal sealed class UpdateInstaller
	{
		/// <summary>等待主程序退出的超时（毫秒）。</summary>
		internal const int WaitExitTimeoutMs = 60000;

		/// <summary>文件被占用（杀毒/句柄延迟释放）时的移动重试次数。</summary>
		internal const int MoveRetryCount = 20;

		private readonly string _installDir;

		private readonly string _restartCommand;

		public UpdateInstaller(string installDir, string restartCommand)
		{
			_installDir = installDir;
			_restartCommand = restartCommand;
		}

		/// <summary>
		/// 解压更新包。zip 内为单根目录（ForkPlus-&lt;版本&gt;-&lt;平台&gt;/）时返回该根目录，
		/// 文件直接在 zip 根时返回解压根目录本身。返回的目录即"新文件源"。
		/// </summary>
		public string Extract(string zipPath, string extractDir)
		{
			if (Directory.Exists(extractDir))
			{
				Directory.Delete(extractDir, recursive: true);
			}
			Directory.CreateDirectory(extractDir);
			ZipFile.ExtractToDirectory(zipPath, extractDir);
			string[] topEntries = Directory.GetFileSystemEntries(extractDir);
			if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
			{
				string root = topEntries[0];
				// zip 由 GitHub Actions（ubuntu zip -qr）打出，external attributes 带
				// unix 权限位，.NET 解压会还原可执行位；对主程序 apphost 显式兜底
				// chmod（防止 zip 工具差异导致丢失执行位）。
				EnsureExecutable(Path.Combine(root, "ForkPlus"));
				EnsureExecutable(Path.Combine(root, "ForkPlus.AutoUpdater"));
				return root;
			}
			return extractDir;
		}

		/// <summary>等待主程序退出（轮询 pid，超时返回 false——调用方按失败处理）。</summary>
		public bool WaitForMainProcessExit(int mainProcessId, int timeoutMs)
		{
			DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
			while (DateTime.UtcNow < deadline)
			{
				if (!IsProcessAlive(mainProcessId))
				{
					return true;
				}
				Thread.Sleep(500);
			}
			return !IsProcessAlive(mainProcessId);
		}

		/// <summary>
		/// 替换安装目录：old → backup，new → install。失败时回滚并抛异常。
		/// </summary>
		public void Replace(string newFilesDir, string backupDir)
		{
			if (Directory.Exists(backupDir))
			{
				Directory.Delete(backupDir, recursive: true);
			}
			Directory.CreateDirectory(backupDir);
			Directory.CreateDirectory(_installDir);

			bool newMoved = false;
			try
			{
				// ① 现有文件整体移入 backup（安装目录清空）
				foreach (string entry in Directory.GetFileSystemEntries(_installDir))
				{
					MoveWithRetry(entry, Path.Combine(backupDir, Path.GetFileName(entry)));
				}
				newMoved = true;
				// ② 新文件移入安装目录
				foreach (string entry in Directory.GetFileSystemEntries(newFilesDir))
				{
					MoveWithRetry(entry, Path.Combine(_installDir, Path.GetFileName(entry)));
				}
				newMoved = false; // 全部就位，backup 只留待清理
			}
			catch (Exception)
			{
				// 回滚：移除半拷贝的新文件，把 backup 移回安装目录
				TryRollback(newFilesDir, backupDir, newMoved);
				throw;
			}
		}

		/// <summary>重启主程序（同路径的新版本）。失败抛异常（由 Program 统一上报）。</summary>
		public void RestartMainApp()
		{
			if (string.IsNullOrWhiteSpace(_restartCommand))
			{
				return;
			}
			ProcessStartInfo startInfo = new ProcessStartInfo(_restartCommand)
			{
				WorkingDirectory = _installDir
			};
			if (OperatingSystem.IsWindows())
			{
				// Windows：UseShellExecute 走 ShellExecute，子进程与 updater 完全解绑
				startInfo.UseShellExecute = true;
			}
			else
			{
				// Unix：直接 exec ELF（UseShellExecute=true 会经 xdg-open 语义解析，
				// 对二进制不可靠）；fork 出的子进程不受父进程退出影响
				startInfo.UseShellExecute = false;
			}
			using (Process process = Process.Start(startInfo))
			{
				// 不等待——updater 退出不影响已启动的新版本
			}
		}

		/// <summary>清理历史更新残留（上次更新被取消留下的 zip/解压目录），best effort。</summary>
		public static void CleanupStaleWorkDirs(string workRoot, TimeSpan olderThan)
		{
			try
			{
				if (!Directory.Exists(workRoot))
				{
					return;
				}
				DateTime cutoff = DateTime.UtcNow - olderThan;
				foreach (string dir in Directory.GetDirectories(workRoot))
				{
					try
					{
						if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
						{
							Directory.Delete(dir, recursive: true);
						}
					}
					catch (Exception)
					{
						// 单目录清理失败（文件占用）不影响主流程
					}
				}
			}
			catch (Exception)
			{
			}
		}

		/// <summary>
		/// 删除主程序设置文件（"重置此版本"流的设置重置步）。settingsFile 为空时按约定
		/// 位置推导（LocalApplicationData/ForkPlus/settings.json，与主程序
		/// App.ForkDirectoryPath 同构）。best effort：文件不存在为幂等成功，删除失败
		///（占用/权限）吞异常不阻断更新主流程。
		/// </summary>
		public static void DeleteSettingsFile(string settingsFile)
		{
			string path = string.IsNullOrWhiteSpace(settingsFile)
				? GetDefaultSettingsFilePath()
				: settingsFile;
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception)
			{
				// 主程序退出后文件被短暂占用的窗口极小；失败不阻断更新重启
			}
		}

		/// <summary>约定设置文件位置（主程序未显式传 --settings-file 时的兜底推导）。</summary>
		internal static string GetDefaultSettingsFilePath()
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"ForkPlus", "settings.json");
		}

		private void TryRollback(string newFilesDir, string backupDir, bool newMoved)
		{
			try
			{
				if (newMoved)
				{
					// 已移入安装目录的新文件（部分）退回 newFilesDir
					foreach (string entry in Directory.GetFileSystemEntries(_installDir))
					{
						string target = Path.Combine(newFilesDir, Path.GetFileName(entry));
						if (!Directory.Exists(target) && !File.Exists(target))
						{
							MoveWithRetry(entry, target);
						}
					}
				}
				// backup 移回安装目录（目标已存在 = 该旧文件从未被移走，跳过即可）
				foreach (string entry in Directory.GetFileSystemEntries(backupDir))
				{
					string destination = Path.Combine(_installDir, Path.GetFileName(entry));
					if (!Directory.Exists(destination) && !File.Exists(destination))
					{
						MoveWithRetry(entry, destination);
					}
				}
			}
			catch (Exception)
			{
				// 回滚本身失败：backup 目录保留在原地，用户可手动恢复——
				// 报错信息里带 backup 路径
				throw new InvalidOperationException("Rollback failed; previous version preserved at " + backupDir);
			}
		}

		/// <summary>move 带重试：Windows 上杀毒/索引服务短暂持有句柄时等一下再试。</summary>
		private static void MoveWithRetry(string source, string destination)
		{
			Exception lastError = null;
			for (int attempt = 0; attempt <= MoveRetryCount; attempt++)
			{
				try
				{
					if (Directory.Exists(source))
					{
						Directory.Move(source, destination);
					}
					else
					{
						File.Move(source, destination);
					}
					return;
				}
				catch (IOException ex)
				{
					lastError = ex;
				}
				catch (UnauthorizedAccessException ex)
				{
					lastError = ex;
				}
				Thread.Sleep(250);
			}
			throw new IOException("Failed to move '" + source + "' -> '" + destination + "': " + lastError?.Message, lastError);
		}

		private static bool IsProcessAlive(int processId)
		{
			if (processId <= 0)
			{
				return false;
			}
			try
			{
				using (Process process = Process.GetProcessById(processId))
				{
					return !process.HasExited;
				}
			}
			catch (ArgumentException)
			{
				// 进程已不存在（GetProcessById 找不到 pid 时抛 ArgumentException）
				return false;
			}
			catch (Exception)
			{
				// 权限等偶发问题：按存活处理（宁可多等超时也不误判退出后撞文件锁）
				return true;
			}
		}

		/// <summary>Unix：确保文件带执行位（zip 工具差异兜底）。文件不存在/平台不符则跳过。</summary>
		private static void EnsureExecutable(string path)
		{
			if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
			{
				try
				{
					if (File.Exists(path))
					{
						File.SetUnixFileMode(path, File.GetUnixFileMode(path)
							| System.IO.UnixFileMode.UserExecute
							| System.IO.UnixFileMode.GroupExecute
							| System.IO.UnixFileMode.OtherExecute);
					}
				}
				catch (Exception)
				{
					// 兜底失败（FS 不支持 / 权限）：保持现状
				}
			}
		}
	}
}
