using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ForkPlus.UI.Plugins
{
	/// <summary>v5.0.3：一次插件压缩包安装的结果汇总（供偏好设置 → 插件页反馈）。</summary>
	public sealed class PluginInstallResult
	{
		/// <summary>复制到 plugins/ 目录的文件数。</summary>
		public int CopiedFiles { get; set; }

		/// <summary>因属于宿主共享程序集而被跳过的文件数（不覆盖宿主自带版本）。</summary>
		public int SkippedSharedAssemblies { get; set; }

		/// <summary>因文件被占用而未能覆盖的文件名（重启后需重新安装或更新）。</summary>
		public List<string> LockedFiles { get; } = new List<string>();

		/// <summary>致命错误（压缩包非法 / 无插件 DLL 等）；为空表示安装成功。</summary>
		public string Error { get; set; }

		public bool Success => string.IsNullOrEmpty(Error);
	}

	/// <summary>
	/// v5.0.3：对比视图插件压缩包安装器（偏好设置 → 插件页的「安装插件」按钮）。
	///
	/// 输入为 ForkPlus-Plugins Releases 页面下载的 zip（内部结构为单一顶层目录
	/// <c>plugins/</c>，其中平铺插件 DLL 及其依赖；zip 条目在 Windows 包中使用
	/// 反斜杠分隔，需统一归一化为 '/'）。安装流程：
	///  1) 解压到进程临时目录（逐条目归一化路径并做 Zip Slip 防护）；
	///  2) 剥离单一顶层目录后，把全部文件<strong>平铺</strong>复制进可执行文件旁的
	///     <c>plugins/</c> 目录——加载器只扫描顶层 DLL、且按扁平文件名解析依赖，
	///     故不保留子目录层级；
	///  3) 宿主共享程序集（契约 / 共享视图组件 / Avalonia / SkiaSharp 等）跳过不覆盖，
	///     避免第三方包内旧版本覆盖宿主导致版本漂移崩溃；
	///  4) 调用方随后重新加载插件并刷新列表。
	/// 单个文件复制失败（Windows 上目标 DLL 已加载被占用）只记录、不中断其余文件。
	/// </summary>
	public static class PluginPackageInstaller
	{
		/// <summary>宿主共享程序集文件名（不含扩展名）前缀/全名——安装时跳过，不覆盖宿主自带版本。</summary>
		private static readonly string[] HostSharedAssemblyNames = new string[4] { "ForkPlus.Plugins.Abstractions", "ForkPlus.Plugins.Ui", "NLog", "ForkPlus" };

		private static readonly string[] HostSharedAssemblyPrefixes = new string[3] { "Avalonia", "SkiaSharp", "HarfBuzzSharp" };

		/// <summary>v5.0.3：把插件压缩包安装到 plugins/ 目录。</summary>
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
				string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
				int pluginAssemblies = 0;
				foreach (string file in files)
				{
					string fileName = Path.GetFileName(file);
					if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !IsHostSharedAssembly(fileName))
					{
						pluginAssemblies++;
					}
					if (IsHostSharedAssembly(fileName))
					{
						result.SkippedSharedAssemblies++;
						continue;
					}
					string destination = Path.Combine(targetDirectory, fileName);
					try
					{
						File.Copy(file, destination, overwrite: true);
						result.CopiedFiles++;
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to copy plugin file '" + fileName + "'", ex);
						result.LockedFiles.Add(fileName);
					}
				}
				if (pluginAssemblies == 0 && result.CopiedFiles == 0)
				{
					result.Error = "压缩包中未找到插件文件（需包含插件 DLL）。";
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
