using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：插件清单（plugin.json）——插件仓与主程序之间的唯一静态契约。
	/// 主程序只按本清单定位并启动插件自带的宿主进程，不加载任何插件程序集，
	/// 因此插件内使用 GPL 等传染性协议时与主程序（进程外、arm's-length 通信）隔离。
	/// </summary>
	public sealed class PluginManifest
	{
		/// <summary>全局唯一插件 id（建议反向域名 + 名称，如 com.example.font-tools）。</summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>插件自身版本（自由格式，仅用于展示）。</summary>
		[JsonProperty("version")]
		public string Version { get; set; }

		/// <summary>插件实现的宿主协议版本；与 <see cref="PluginProtocol.Version"/> 不符则拒载。</summary>
		[JsonProperty("apiVersion")]
		public int ApiVersion { get; set; }

		/// <summary>插件声明的许可证（如 GPL-3.0-or-later）。仅作展示，主程序不做法律判断。</summary>
		[JsonProperty("license")]
		public string License { get; set; }

		[JsonProperty("homepage")]
		public string Homepage { get; set; }

		/// <summary>宿主进程启动方式（相对插件目录，或 PATH 上的命令名）。</summary>
		[JsonProperty("host")]
		public PluginHostEntry Host { get; set; }

		/// <summary>该插件提供的对比视图（每个视图对应主程序里的一个查看器注册项）。</summary>
		[JsonProperty("viewers")]
		public List<PluginViewerDescriptor> Viewers { get; set; }

		/// <summary>清单所在目录（由 <see cref="PluginDiscovery"/> 填入，用于解析宿主可执行文件相对路径）。</summary>
		[JsonIgnore]
		public string BaseDirectory { get; set; }

		public List<PluginViewerDescriptor> ViewersOrEmpty => Viewers ?? new List<PluginViewerDescriptor>();

		/// <summary>
		/// 校验清单必填项。返回 null 表示合法，否则返回面向用户的失败原因。
		/// 宿主协议版本不符在这里拦掉（而非等到渲染时才失败）。
		/// </summary>
		public string Validate()
		{
			if (string.IsNullOrWhiteSpace(Id))
			{
				return "plugin.json is missing required field 'id'.";
			}
			if (Host == null || string.IsNullOrWhiteSpace(Host.Executable))
			{
				return "plugin.json is missing required field 'host.executable'.";
			}
			if (ApiVersion != PluginProtocol.Version)
			{
				return $"plugin requires host API version {ApiVersion}, but this build implements {PluginProtocol.Version}.";
			}
			if (ViewersOrEmpty.Count == 0)
			{
				return "plugin.json declares no viewers.";
			}
			foreach (PluginViewerDescriptor viewer in ViewersOrEmpty)
			{
				if (viewer == null || string.IsNullOrWhiteSpace(viewer.Id))
				{
					return "plugin.json has a viewer entry without 'id'.";
				}
				if (viewer.ExtensionsOrEmpty.Count == 0)
				{
					return $"plugin.json viewer '{viewer.Id}' declares no extensions (use \"{PluginViewerDescriptor.WildcardExtension}\" to claim any file).";
				}
				foreach (string extension in viewer.ExtensionsOrEmpty)
				{
					// 只接受 ".png" 这类带点后缀或 "*"：写到 "png"（漏点）会静默永不命中，
					// 这类拼写错误在插件侧很难自查，故在装载时就拒绝。
					if (string.IsNullOrEmpty(extension)
						|| (!string.Equals(extension, PluginViewerDescriptor.WildcardExtension, StringComparison.Ordinal)
							&& !extension.StartsWith(".", StringComparison.Ordinal)))
					{
						return $"plugin.json viewer '{viewer.Id}' has an invalid extension '{extension}' (expected \".png\" style, or \"{PluginViewerDescriptor.WildcardExtension}\").";
					}
				}
			}
			return null;
		}

		/// <summary>解析宿主可执行文件的绝对路径（相对路径按插件目录解析）。</summary>
		public string ResolveHostExecutable()
		{
			string executable = Host?.Executable;
			if (string.IsNullOrWhiteSpace(executable))
			{
				return null;
			}
			if (Path.IsPathRooted(executable))
			{
				return executable;
			}
			return Path.GetFullPath(Path.Combine(BaseDirectory ?? ".", executable));
		}

		/// <summary>从 JSON 文本解析清单；格式错误返回 null 并给出原因。</summary>
		public static PluginManifest Parse(string json, string baseDirectory, out string error)
		{
			error = null;
			if (string.IsNullOrWhiteSpace(json))
			{
				error = "plugin.json is empty.";
				return null;
			}
			PluginManifest manifest;
			try
			{
				manifest = JsonConvert.DeserializeObject<PluginManifest>(json);
			}
			catch (JsonException ex)
			{
				error = "plugin.json is not valid JSON: " + ex.Message;
				return null;
			}
			if (manifest == null)
			{
				error = "plugin.json did not contain a manifest object.";
				return null;
			}
			manifest.BaseDirectory = baseDirectory;
			error = manifest.Validate();
			if (error != null)
			{
				return null;
			}
			return manifest;
		}
	}

	/// <summary>v4.5.0：插件宿主进程的启动方式。</summary>
	public sealed class PluginHostEntry
	{
		/// <summary>可执行文件路径（相对插件目录）或 PATH 上的命令名。</summary>
		[JsonProperty("executable")]
		public string Executable { get; set; }

		/// <summary>附加启动参数（可选）。</summary>
		[JsonProperty("arguments")]
		public List<string> Arguments { get; set; }

		public string[] ArgumentsOrEmpty => Arguments?.ToArray() ?? new string[0];
	}

	/// <summary>
	/// v4.5.0：插件声明的单个对比视图。宿主据此把它注册进查看器注册表；
	/// 实际匹配与渲染都由插件进程完成，宿主只负责转交请求并显示返回的位图。
	/// </summary>
	public sealed class PluginViewerDescriptor
	{
		/// <summary>通配扩展名：声明它表示"愿意处理任何文件"，用作二进制兜底视图。</summary>
		public const string WildcardExtension = "*";

		/// <summary>视图 id（在插件内唯一）；宿主用它拼出全局注册 id。</summary>
		[JsonProperty("id")]
		public string Id { get; set; }

		/// <summary>展示名（用于设置页 / 诊断）。</summary>
		[JsonProperty("displayName")]
		public string DisplayName { get; set; }

		/// <summary>判定优先级；越大越先被询问（同内置查看器同一标尺）。</summary>
		[JsonProperty("priority")]
		public int Priority { get; set; }

		/// <summary>
		/// 声明感兴趣的扩展名（小写，含点，如 ".png"）；或 <see cref="WildcardExtension"/> 表示通配。
		/// 两者语义差别见 <see cref="MatchesPath"/>。
		/// </summary>
		[JsonProperty("extensions")]
		public List<string> Extensions { get; set; }

		public List<string> ExtensionsOrEmpty => Extensions ?? new List<string>();

		/// <summary>是否声明了通配 "*"（"任何文件都愿意看"，供二进制兜底视图使用）。</summary>
		public bool IsWildcard
		{
			get
			{
				foreach (string candidate in ExtensionsOrEmpty)
				{
					if (string.Equals(candidate, WildcardExtension, StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}
		}

		/// <summary>
		/// 该视图是否认领指定路径（仅按扩展名做廉价预筛，最终仍由插件 CanHandle 决定）。
		///
		/// <paramref name="allowWildcard"/> 的区别很关键：为 false 时只认具体扩展名——宿主用它回答
		/// "该文件是否按图片/字节内容处理"（PathHelper.IsImagePath）。通配视图若在这里也算数，
		/// 就会把每个文本文件都判成"图片"，从而改变文本/二进制 diff 的分流，并让
		/// "跳过加载超大未跟踪文件"的性能闸门失效。为 true 时再退一步接受 "*" 兜底，
		/// 宿主只在已确认是二进制的分支里这样问。
		/// </summary>
		public bool MatchesPath(string path, bool allowWildcard)
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}
			string extension = Path.GetExtension(path);
			if (!string.IsNullOrEmpty(extension))
			{
				foreach (string candidate in ExtensionsOrEmpty)
				{
					if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			// 通配兜底：无扩展名的文件（Makefile / LICENSE）也应由 "*" 覆盖。
			return allowWildcard && IsWildcard;
		}

		/// <summary>只按具体扩展名认领（不含通配兜底）。</summary>
		public bool MatchesExtension(string path)
		{
			return MatchesPath(path, allowWildcard: false);
		}
	}
}