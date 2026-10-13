using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.1.0：插件目录清单（<c>plugin.json</c>）的宿主侧只读模型。
	///
	/// 新插件架构下每个插件独占一个目录（<c>plugins/&lt;AssemblyName&gt;/</c>），目录根放一份
	/// <c>plugin.json</c>。宿主在**安装前**（读 zip 内的清单）与**加载前**（读磁盘上的清单）
	/// 都以它为准判定：缺少清单、缺少 <see cref="MinHostVersion"/>、或宿主版本低于该值，
	/// 一律不安装 / 不加载。清单同时携带插件自身版本与主程序集名，供偏好设置页展示。
	///
	/// 清单格式：
	/// <code>
	/// {
	///   "schemaVersion": 1,
	///   "id": "forkplus.pdf",
	///   "version": "1.0.0",
	///   "minHostVersion": "5.1.0",
	///   "assembly": "ForkPlus.Plugins.Pdf"
	/// }
	/// </code>
	/// </summary>
	public sealed class PluginManifest
	{
		/// <summary>清单文件名（插件目录根）。</summary>
		public const string FileName = "plugin.json";

		/// <summary>当前宿主声明的宿主版本（与 AssemblyInfo 的 AssemblyVersion 对齐）。</summary>
		public const string HostVersion = "5.1.2";

		public int SchemaVersion { get; set; }

		/// <summary>插件唯一 Id（如 "forkplus.pdf"）。</summary>
		public string Id { get; set; }

		/// <summary>插件自身版本（如 "1.0.0"）。</summary>
		public string Version { get; set; }

		/// <summary>兼容的最低宿主版本（如 "5.1.0"）；宿主版本 ≥ 该值才兼容。</summary>
		public string MinHostVersion { get; set; }

		/// <summary>插件主程序集名（不含 .dll），如 "ForkPlus.Plugins.Pdf"。目录名与之一致。</summary>
		public string Assembly { get; set; }

		/// <summary>清单是否完整（Id / MinHostVersion / Assembly 均非空）。</summary>
		public bool IsComplete =>
			!string.IsNullOrWhiteSpace(Id) &&
			!string.IsNullOrWhiteSpace(MinHostVersion) &&
			!string.IsNullOrWhiteSpace(Assembly);

		/// <summary>
		/// 从插件目录读取清单；目录下无 plugin.json、JSON 非法、或字段不完整时返回 null。
		/// </summary>
		[Null]
		public static PluginManifest ReadFromDirectory(string pluginDirectory)
		{
			if (string.IsNullOrWhiteSpace(pluginDirectory))
			{
				return null;
			}
			return ReadFromFile(Path.Combine(pluginDirectory, FileName));
		}

		/// <summary>从指定清单文件读取；文件不存在/解析失败/字段不完整时返回 null。</summary>
		[Null]
		public static PluginManifest ReadFromFile(string manifestPath)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
				{
					return null;
				}
				JObject json = JObject.Parse(File.ReadAllText(manifestPath));
				PluginManifest manifest = new PluginManifest
				{
					SchemaVersion = json.Value<int?>("schemaVersion") ?? 1,
					Id = json.Value<string>("id"),
					Version = json.Value<string>("version"),
					MinHostVersion = json.Value<string>("minHostVersion"),
					Assembly = json.Value<string>("assembly")
				};
				return manifest.IsComplete ? manifest : null;
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to read plugin manifest '" + manifestPath + "': " + ex.Message);
				return null;
			}
		}

		/// <summary>
		/// v5.1.0：宿主当前版本是否满足清单要求的 <paramref name="minHostVersion"/>。
		/// 解析失败（非法版本串）按不兼容处理——宁可拒绝，也不放过可能不兼容的插件。
		/// </summary>
		public static bool IsHostCompatible(string minHostVersion)
		{
			if (string.IsNullOrWhiteSpace(minHostVersion))
			{
				return false;
			}
			return TryParseVersion(minHostVersion, out Version required)
				&& TryParseVersion(HostVersion, out Version host)
				&& host >= required;
		}

		/// <summary>宽容解析 "x" / "x.y" / "x.y.z" 形式版本（缺位补 0）。</summary>
		private static bool TryParseVersion(string text, [Null] out Version version)
		{
			version = null;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string[] parts = text.Trim().Split('.');
			if (parts.Length == 0 || parts.Length > 4)
			{
				return false;
			}
			int[] numbers = new int[4];
			for (int i = 0; i < parts.Length; i++)
			{
				if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
				{
					return false;
				}
			}
			version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
			return true;
		}
	}
}