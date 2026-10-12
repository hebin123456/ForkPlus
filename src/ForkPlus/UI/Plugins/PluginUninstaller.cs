using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus.UI.Plugins
{
	/// <summary>v5.0.3：一次批量卸载的结果汇总（供偏好设置 → 插件页反馈）。</summary>
	public sealed class PluginUninstallResult
	{
		/// <summary>成功移除的插件 Id。</summary>
		public List<string> RemovedIds { get; } = new List<string>();

		/// <summary>在注册表中找不到来源目录的插件 Id。</summary>
		public List<string> NotFoundIds { get; } = new List<string>();

		/// <summary>因目录内文件被占用（Windows 下已加载的 DLL）而延迟到重启删除的目录路径。</summary>
		public List<string> PendingRestartFiles { get; } = new List<string>();

		public bool HasPendingRestart => PendingRestartFiles.Count > 0;
	}

	/// <summary>
	/// v5.1.0：对比视图插件卸载器。偏好设置 → 插件页勾选若干插件后批量卸载：
	/// 每个插件独占一个目录（<c>plugins/&lt;AssemblyName&gt;/</c>），卸载即**递归删除整个目录**——
	/// 私有依赖、原生库、清单一并清理干净，不留残留。内置插件自 v5.1.0 起同样可卸载
	/// （卸载后可从插件仓下载对应插件包重装）。
	///
	/// 文件占用（Windows 上已被默认 ALC 加载的 DLL 无法删除）：把**目录路径**写入
	/// plugins/.pending-uninstall，<see cref="DiffViewPluginLoader"/> 本次重载会跳过这些
	/// 目录（不再重新注册），下次启动 <see cref="ProcessPendingDeletions"/> 再尝试真正删除。
	/// </summary>
	public static class PluginUninstaller
	{
		private const string PendingDeletionFileName = ".pending-uninstall";

		private static readonly object SyncRoot = new object();

		private static HashSet<string> _pendingPaths;

		private static string PendingFilePath => Path.Combine(DiffViewPluginLoader.EnsurePluginDirectory(), PendingDeletionFileName);

		/// <summary>v5.1.0：指定插件目录是否处于「待删除」状态（卸载时被占用，重启后清理）。</summary>
		public static bool IsPendingDeletion(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}
			string fullPath = NormalizePath(path);
			lock (SyncRoot)
			{
				return PendingPaths().Contains(fullPath);
			}
		}

		/// <summary>
		/// v5.1.0：启动期清理上次卸载时被占用、延迟删除的插件目录。
		/// 必须早于 <see cref="DiffViewPluginLoader.LoadDefault"/> 调用。
		/// </summary>
		public static void ProcessPendingDeletions()
		{
			lock (SyncRoot)
			{
				HashSet<string> pending = PendingPaths();
				if (pending.Count == 0)
				{
					return;
				}
				List<string> remaining = new List<string>();
				foreach (string path in pending)
				{
					if (TryDeletePath(path))
					{
						continue;
					}
					remaining.Add(path);
				}
				pending.Clear();
				foreach (string path in remaining)
				{
					pending.Add(path);
				}
				SavePendingPaths(pending);
				if (remaining.Count == 0)
				{
					Log.Info("Pending plugin directory deletions processed (all removed)");
				}
				else
				{
					Log.Info($"Pending plugin directory deletions: {remaining.Count} still locked");
				}
			}
		}

		/// <summary>
		/// v5.1.0：批量卸载指定插件（递归删除其整个插件目录）。
		/// 调用方随后应执行 <see cref="DiffViewPluginLoader.Reload"/> 使注册表反映删除结果。
		/// </summary>
		public static PluginUninstallResult Uninstall(IEnumerable<string> pluginIds)
		{
			PluginUninstallResult result = new PluginUninstallResult();
			if (pluginIds == null)
			{
				return result;
			}
			HashSet<string> seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string pluginId in pluginIds)
			{
				if (string.IsNullOrWhiteSpace(pluginId))
				{
					continue;
				}
				string directory = DiffViewPluginLoader.GetPluginDirectory(pluginId);
				if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
				{
					result.NotFoundIds.Add(pluginId);
					continue;
				}
				if (TryDeleteDirectory(directory))
				{
					result.RemovedIds.Add(pluginId);
					continue;
				}
				// 目录内有文件被占用：登记整目录待删，本次重载会被跳过 → 视为已移除。
				if (seenDirectories.Add(directory))
				{
					AddPendingPath(directory);
					result.PendingRestartFiles.Add(directory);
				}
				result.RemovedIds.Add(pluginId);
			}
			return result;
		}

		/// <summary>递归删除插件目录（清除只读属性后整目录删除）。</summary>
		private static bool TryDeleteDirectory(string directory)
		{
			try
			{
				if (!Directory.Exists(directory))
				{
					return true;
				}
				foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
				{
					File.SetAttributes(file, FileAttributes.Normal);
				}
				Directory.Delete(directory, recursive: true);
				return true;
			}
			catch (Exception ex)
			{
				Log.Info("Plugin directory is locked, deferring deletion to next start: " + directory + " (" + ex.Message + ")");
				return false;
			}
		}

		private static void AddPendingPath(string path)
		{
			string fullPath = NormalizePath(path);
			lock (SyncRoot)
			{
				HashSet<string> pending = PendingPaths();
				if (pending.Add(fullPath))
				{
					SavePendingPaths(pending);
				}
			}
		}

		/// <summary>删除待删路径：目录递归删、单文件直接删（兼容旧版遗留的按文件待删记录）。</summary>
		private static bool TryDeletePath(string path)
		{
			if (Directory.Exists(path))
			{
				return TryDeleteDirectory(path);
			}
			if (File.Exists(path))
			{
				return TryDeleteFile(path);
			}
			return true;
		}

		private static HashSet<string> PendingPaths()
		{
			if (_pendingPaths == null)
			{
				_pendingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				try
				{
					string file = PendingFilePath;
					if (File.Exists(file))
					{
						foreach (string line in File.ReadAllLines(file))
						{
							if (!string.IsNullOrWhiteSpace(line))
							{
								_pendingPaths.Add(line.Trim());
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to read pending plugin deletions", ex);
				}
			}
			return _pendingPaths;
		}

		private static void SavePendingPaths(HashSet<string> pending)
		{
			try
			{
				string file = PendingFilePath;
				if (pending.Count == 0)
				{
					if (File.Exists(file))
					{
						File.Delete(file);
					}
					return;
				}
				File.WriteAllLines(file, pending);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to persist pending plugin deletions", ex);
			}
		}

		private static bool TryDeleteFile(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					return true;
				}
				File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
				return true;
			}
			catch (Exception ex)
			{
				Log.Info("Plugin file is locked, deferring deletion to next start: " + path + " (" + ex.Message + ")");
				return false;
			}
		}

		private static string NormalizePath(string path)
		{
			try
			{
				return Path.GetFullPath(path);
			}
			catch
			{
				return path;
			}
		}
	}
}