using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ForkPlus.UI.Plugins
{
	/// <summary>v5.0.3：一次插件压缩包安装的结果汇总（供偏好设置 → 插件页反馈）。</summary>
	public sealed class PluginInstallResult
	{
		/// <summary>成功安装的插件 Id（v5.1.0：按插件清单识别）。</summary>
		public List<string> InstalledIds { get; } = new List<string>();

		/// <summary>复制到 plugins/&lt;AssemblyName&gt;/ 的文件数。</summary>
		public int CopiedFiles { get; set; }

		/// <summary>因属于宿主共享程序集而被跳过的文件数（不覆盖宿主自带版本）。</summary>
		public int SkippedSharedAssemblies { get; set; }

		/// <summary>v5.1.0：被拒绝安装的插件及原因（缺少清单 / 缺少 minHostVersion / 与宿主不兼容）。</summary>
		public List<string> RejectedReasons { get; } = new List<string>();

		/// <summary>因文件被占用而未能覆盖的文件名（重启后需重新安装或更新）。</summary>
		public List<string> LockedFiles { get; } = new List<string>();

		/// <summary>致命错误（压缩包非法 / 无插件目录 / 无插件可安装等）；为空表示安装成功。</summary>
		public string Error { get; set; }

		public bool Success => string.IsNullOrEmpty(Error) && InstalledIds.Count > 0;
	}

	/// <summary>
	/// v5.1.0：对比视图插件压缩包安装器（偏好设置 → 插件页的「安装插件」按钮）。
	///
	/// 输入为 ForkPlus-Plugins Releases 页面下载的 zip——**合集包**（内含多个插件目录）与
	/// **单插件包**（内含一个插件目录）都支持。包内结构统一为单一顶层 <c>plugins/</c>，
	/// 其下每个插件独占目录 <c>&lt;AssemblyName&gt;/</c>，目录根带 <c>plugin.json</c> 清单。
	/// 安装流程：
	///  1) 解压到进程临时目录（逐条目归一化路径并做 Zip Slip 防护）；
	///  2) 剥离单一顶层目录（如 plugins/），扫描各插件目录（以含 plugin.json 为准）；
	///  3) 逐个读清单做兼容校验：缺少清单 / 缺少 minHostVersion / 宿主版本低于清单要求 →
	///     **拒绝安装**并记录原因；否则把整个插件目录复制到可执行文件旁的
	///     <c>plugins/&lt;AssemblyName&gt;/</c>（保留原目录层级）——每插件独立目录，
	///     私有依赖互不打架，卸载时整目录删除即可清理干净；
	///  4) 宿主共享程序集（契约 / 共享视图组件 / Avalonia / SkiaSharp 等）跳过不覆盖；
	///  5) 调用方随后重新加载插件并刷新列表。
	/// 单个文件复制失败（Windows 上目标 DLL 已加载被占用）只记录、不中断其余文件。
	/// </summary>
	public static class PluginPackageInstaller
	{
		/// <summary>宿主共享程序集文件名（不含扩展名）全名——安装时跳过，不覆盖宿主自带版本。</summary>
		private static readonly string[] HostSharedAssemblyNames = new string[4] { "ForkPlus.Plugins.Abstractions", "ForkPlus.Plugins.Ui", "NLog", "ForkPlus" };

		private static readonly string[] HostSharedAssemblyPrefixes = new string[3] { "Avalonia", "SkiaSharp", "HarfBuzzSharp" };

		/// <summary>把插件压缩包安装到 plugins/ 目录。</summary>
		public static PluginInstallResult InstallFromZip(string zipPath)
		{
			PluginInstallResult result = new PluginInstallResult();
			if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
			{
				result.Error = "安装包文件不存在。";
				return result;
			}
			string targetDirectory = DiffViewPluginLoader.EnsurePluginDirectory();
			string tempDirectory = Path.Combine(Path.GetTempPath(), "forkplus-plugin-" + Guid.NewGuid().ToString("N"));
			try
			{
				Directory.CreateDirectory(tempDirectory);
				ExtractArchive(zipPath, tempDirectory);
				string sourceRoot = ResolveSourceRoot(tempDirectory);
				List<string> pluginDirectories = FindPluginDirectories(sourceRoot);
				if (pluginDirectories.Count == 0)
				{
					result.Error = "压缩包中未找到插件（需包含带 plugin.json 清单的插件目录）。";
					return result;
				}
				foreach (string pluginDirectory in pluginDirectories)
				{
					InstallPluginDirectory(pluginDirectory, targetDirectory, result);
				}
				if (result.InstalledIds.Count == 0)
				{
					result.Error = "压缩包中的插件均无法安装：" + string.Join("；", result.RejectedReasons);
				}
			}
			catch (InvalidDataException)
			{
				result.Error = "压缩包已损坏或不是有效的 zip 文件。";
			}
			catch (Exception ex)
			{
				Log.Error("Failed to install plugin package '" + zipPath + "'", ex);
				result.Error = ex.Message;
			}
			finally
			{
				TryDeleteDirectory(tempDirectory);
			}
			return result;
		}

		/// <summary>校验单个插件目录的清单并整目录复制到 plugins/&lt;AssemblyName&gt;/。</summary>
		private static void InstallPluginDirectory(string pluginDirectory, string targetDirectory, PluginInstallResult result)
		{
			PluginManifest manifest = PluginManifest.ReadFromDirectory(pluginDirectory);
			if (manifest == null)
			{
				result.RejectedReasons.Add(Path.GetFileName(pluginDirectory) + "：缺少或非法的 plugin.json 清单（需含 id / version / minHostVersion / assembly）");
				return;
			}
			if (!PluginManifest.IsHostCompatible(manifest.MinHostVersion))
			{
				result.RejectedReasons.Add(manifest.Id + "：需要主程序版本 ≥ " + manifest.MinHostVersion + "，当前为 " + PluginManifest.HostVersion);
				return;
			}
			string destinationDirectory = Path.Combine(targetDirectory, manifest.Assembly);
			string[] files = Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories);
			string sourceRoot = Path.GetFullPath(pluginDirectory);
			foreach (string file in files)
			{
				string fileName = Path.GetFileName(file);
				if (IsHostSharedAssembly(fileName))
				{
					result.SkippedSharedAssemblies++;
					continue;
				}
				string relativePath = file.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string destination = Path.Combine(destinationDirectory, relativePath);
				try
				{
					string parent = Path.GetDirectoryName(destination);
					if (!string.IsNullOrEmpty(parent))
					{
						Directory.CreateDirectory(parent);
					}
					File.Copy(file, destination, overwrite: true);
					result.CopiedFiles++;
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to copy plugin file '" + fileName + "'", ex);
					result.LockedFiles.Add(fileName);
				}
			}
			result.InstalledIds.Add(manifest.Id);
		}

		/// <summary>逐条目解压：路径分隔符归一化为 '/'，并做 Zip Slip 防护（拒绝越出目标目录的条目）。</summary>
		private static void ExtractArchive(string zipPath, string targetDirectory)
		{
			string targetRoot = Path.GetFullPath(targetDirectory);
			using ZipArchive archive = ZipFile.OpenRead(zipPath);
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (string.IsNullOrEmpty(entry.Name))
				{
					continue;
				}
				string relativePath = entry.FullName.Replace('\\', '/');
				string destination = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
				if (!destination.StartsWith(targetRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
				{
					Log.Warn("Skipped unsafe zip entry: " + entry.FullName);
					continue;
				}
				string parent = Path.GetDirectoryName(destination);
				if (!string.IsNullOrEmpty(parent))
				{
					Directory.CreateDirectory(parent);
				}
				entry.ExtractToFile(destination, overwrite: true);
			}
		}

		/// <summary>
		/// 解析插件文件根目录：压缩包内若只有单一顶层目录（如 plugins/）且根下无文件，
		/// 取其作为根（剥离该层）；否则以解压根为根。保证不同打包层级都能正确安装。
		/// </summary>
		private static string ResolveSourceRoot(string extractedDirectory)
		{
			string[] topDirectories = Directory.GetDirectories(extractedDirectory);
			string[] topFiles = Directory.GetFiles(extractedDirectory);
			if (topDirectories.Length == 1 && topFiles.Length == 0)
			{
				return topDirectories[0];
			}
			return extractedDirectory;
		}

		/// <summary>v5.1.0：找出根下所有"插件目录"（自身或子目录根含 plugin.json）。</summary>
		private static List<string> FindPluginDirectories(string sourceRoot)
		{
			List<string> result = new List<string>();
			if (File.Exists(Path.Combine(sourceRoot, PluginManifest.FileName)))
			{
				// 单插件包且已剥离到插件目录根。
				result.Add(sourceRoot);
				return result;
			}
			foreach (string subDirectory in Directory.GetDirectories(sourceRoot))
			{
				if (File.Exists(Path.Combine(subDirectory, PluginManifest.FileName)))
				{
					result.Add(subDirectory);
				}
			}
			return result;
		}

		/// <summary>宿主共享程序集：安装时跳过，不覆盖宿主自带版本。</summary>
		private static bool IsHostSharedAssembly(string fileName)
		{
			string name = Path.GetFileNameWithoutExtension(fileName);
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			foreach (string shared in HostSharedAssemblyNames)
			{
				if (name.Equals(shared, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			foreach (string prefix in HostSharedAssemblyPrefixes)
			{
				if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static void TryDeleteDirectory(string directory)
		{
			try
			{
				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					Directory.Delete(directory, recursive: true);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to clean plugin temp directory '" + directory + "'", ex);
			}
		}
	}
}