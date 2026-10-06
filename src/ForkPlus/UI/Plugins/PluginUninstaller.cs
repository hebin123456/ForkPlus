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

		/// <summary>因属于内置插件而被拒绝卸载的插件 Id。</summary>
		public List<string> SkippedBuiltInIds { get; } = new List<string>();

		/// <summary>在注册表中找不到来源文件的插件 Id。</summary>
		public List<string> NotFoundIds { get; } = new List<string>();

		/// <summary>因文件被占用（Windows 下已加载的 DLL）而延迟到重启删除的文件路径。</summary>
		public List<string> PendingRestartFiles { get; } = new List<string>();

		public bool HasPendingRestart => PendingRestartFiles.Count > 0;
	}

	/// <summary>
	/// v5.0.3：对比视图插件卸载器。偏好设置 → 插件页勾选若干插件后批量卸载：
	/// 删除插件所在 DLL 及其同名附属文件（.pdb / .xml / .THIRD-PARTY-NOTICES.txt）；
	/// 内置插件（forkplus.image / forkplus.hex）受保护，拒绝卸载。
	///
	/// 文件占用（Windows 上已被默认 ALC 加载的 DLL 无法删除）：把文件路径写入
	/// plugins/.pending-uninstall，<see cref="DiffViewPluginLoader"/> 本次重载会跳过这些
	/// 文件（不再重新注册），下次启动 <see cref="ProcessPendingDeletions"/> 再尝试真正删除。
	/// 共享依赖 DLL（如 pdfium、Docnet.Core 等）不属于任何单个插件，不在此清理，避免误删。
	/// </summary>
	public static class PluginUninstaller
	{
		private const string PendingDeletionFileName = ".pending-uninstall";

		private static readonly object SyncRoot = new object();

		private static HashSet<string> _pendingFiles;

		private static string PendingFilePath => Path.Combine(DiffViewPluginLoader.EnsurePluginDirectory(), PendingDeletionFileName);

		/// <summary>v5.0.3：指定文件是否处于「待删除」状态（卸载时被占用，重启后清理）。</summary>
		public static bool IsPendingDeletion(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}
			string fullPath = NormalizePath(path);
			lock (SyncRoot)
			{
				return PendingFiles().Contains(fullPath);
			}
		}

		/// <summary>
		/// v5.0.3：启动期清理上次卸载时被占用、延迟删除的插件文件。
		/// 必须早于 <see cref="DiffViewPluginLoader.LoadDefault"/> 调用。
		/// </summary>
		public static void ProcessPendingDeletions()
		{
			lock (SyncRoot)
			{
				HashSet<string> pending = PendingFiles();
				if (pending.Count == 0)
				{
					return;
				}
				List<string> remaining = new List<string>();
				foreach (string path in pending)
				{
					if (File.Exists(path) && !TryDeleteFile(path))
					{
						remaining.Add(path);
					}
				}
				pending.Clear();
				foreach (string path in remaining)
				{
					pending.Add(path);
				}
				SavePendingFiles(pending);
				if (remaining.Count == 0)
				{
					Log.Info("Pending plugin deletions processed (all files removed)");
				}
				else
				{
					Log.Info($"Pending plugin deletions: {remaining.Count} file(s) still locked");
				}
			}
		}

		/// <summary>
		/// v5.0.3：批量卸载指定插件（删除 DLL 与同名附属文件）。内置插件自动跳过。
		/// 调用方随后应执行 <see cref="DiffViewPluginLoader.Reload"/> 使注册表反映删除结果。
		/// </summary>
		public static PluginUninstallResult Uninstall(IEnumerable<string> pluginIds)
		{
			PluginUninstallResult result = new PluginUninstallResult();
			if (pluginIds == null)
			{
				return result;
			}
			Dictionary<string, string> sources = DiffViewPluginLoader.GetPluginSourceFiles();
			List<KeyValuePair<string, string>> targets = new List<KeyValuePair<string, string>>();
			HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string pluginId in pluginIds)
			{
				if (string.IsNullOrWhiteSpace(pluginId))
				{
					continue;
				}
				if (DiffViewPluginLoader.IsBuiltInPlugin(pluginId))
				{
					result.SkippedBuiltInIds.Add(pluginId);
					continue;
				}
				if (!sources.TryGetValue(pluginId, out string dllPath) || string.IsNullOrWhiteSpace(dllPath))
				{
					result.NotFoundIds.Add(pluginId);
					continue;
				}
				if (seenFiles.Add(dllPath))
				{
					targets.Add(new KeyValuePair<string, string>(pluginId, dllPath));
				}
			}
			foreach (KeyValuePair<string, string> target in targets)
			{
				bool removed = true;
				foreach (string file in PluginFiles(target.Value))
				{
					if (!File.Exists(file))
					{
						continue;
					}
					if (TryDeleteFile(file))
					{
						continue;
					}
					removed = false;
					AddPendingFile(file);
					result.PendingRestartFiles.Add(file);
				}
				if (removed)
				{
					result.RemovedIds.Add(target.Key);
				}
				else
				{
					// 文件虽未删除，但已在待删列表中，重载时会被跳过——视为已移除。
					result.RemovedIds.Add(target.Key);
				}
			}
			return result;
		}

		/// <summary>插件所在 DLL 及其同名附属文件（pdb / xml / 第三方声明）。</summary>
		private static IEnumerable<string> PluginFiles(string dllPath)
		{
			yield return dllPath;
			string basePath = Path.ChangeExtension(dllPath, null);
			yield return basePath + ".pdb";
			yield return basePath + ".xml";
			yield return basePath + ".THIRD-PARTY-NOTICES.txt";
		}

		private static void AddPendingFile(string path)
		{
			string fullPath = NormalizePath(path);
			lock (SyncRoot)
			{
				HashSet<string> pending = PendingFiles();
				if (pending.Add(fullPath))
				{
					SavePendingFiles(pending);
				}
			}
		}

		private static HashSet<string> PendingFiles()
		{
			if (_pendingFiles == null)
			{
				_pendingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				try
				{
					string file = PendingFilePath;
					if (File.Exists(file))
					{
						foreach (string line in File.ReadAllLines(file))
						{
							if (!string.IsNullOrWhiteSpace(line))
							{
								_pendingFiles.Add(line.Trim());
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to read pending plugin deletions", ex);
				}
			}
			return _pendingFiles;
		}

		private static void SavePendingFiles(HashSet<string> pending)
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
