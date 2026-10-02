using System;
using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：一个插件目录的发现结果。失败的插件不抛异常——一个坏清单不该让整批插件都挂掉，
	/// 调用方按 <see cref="Error"/> 记日志、跳过即可。
	/// </summary>
	public sealed class PluginDiscoveryResult
	{
		public string ManifestPath { get; }

		/// <summary>解析成功的清单元数据；失败为 null。</summary>
		public PluginManifest Manifest { get; }

		/// <summary>失败原因；成功为 null。</summary>
		public string Error { get; }

		public PluginDiscoveryResult(string manifestPath, PluginManifest manifest, string error)
		{
			ManifestPath = manifestPath;
			Manifest = manifest;
			Error = error;
		}

		public bool IsUsable => Manifest != null;
	}

	/// <summary>
	/// v4.5.0：插件目录扫描。约定 pluginsRoot 下「一个插件一个子目录、目录里有 plugin.json」，
	/// 与 VS Code / LSP 生态一致；插件仓（可含 GPL 代码）只需产出这种目录结构即可接入。
	/// </summary>
	public static class PluginDiscovery
	{
		public const string ManifestFileName = "plugin.json";

		/// <summary>
		/// 扫描插件根目录下的直接子目录。根目录不存在返回空列表（首次使用尚未安装任何插件是正常状态）。
		/// </summary>
		public static List<PluginDiscoveryResult> Discover(string pluginsRoot)
		{
			List<PluginDiscoveryResult> results = new List<PluginDiscoveryResult>();
			if (string.IsNullOrWhiteSpace(pluginsRoot) || !Directory.Exists(pluginsRoot))
			{
				return results;
			}
			string[] directories;
			try
			{
				directories = Directory.GetDirectories(pluginsRoot);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				results.Add(new PluginDiscoveryResult(pluginsRoot, null, "Failed to enumerate plugin directory: " + ex.Message));
				return results;
			}
			Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
			foreach (string directory in directories)
			{
				results.Add(LoadOne(directory));
			}
			return results;
		}

		/// <summary>读取单个插件目录的清单。</summary>
		public static PluginDiscoveryResult LoadOne(string pluginDirectory)
		{
			string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
			if (!File.Exists(manifestPath))
			{
				return new PluginDiscoveryResult(manifestPath, null, "Missing " + ManifestFileName + ".");
			}
			string json;
			try
			{
				json = File.ReadAllText(manifestPath);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
			{
				return new PluginDiscoveryResult(manifestPath, null, "Failed to read " + ManifestFileName + ": " + ex.Message);
			}
			PluginManifest manifest = PluginManifest.Parse(json, pluginDirectory, out string error);
			return new PluginDiscoveryResult(manifestPath, manifest, error);
		}
	}
}