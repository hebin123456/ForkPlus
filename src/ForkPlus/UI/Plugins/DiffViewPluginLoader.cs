using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ForkPlus.Plugins;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Settings;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.1.0：对比视图插件的动态加载器（每插件目录 + 独立加载上下文）。
	///
	/// 扫描可执行文件旁的 <c>plugins/</c> 目录下的**每个子目录**，要求子目录根有
	/// <c>plugin.json</c> 清单（<see cref="PluginManifest"/>）。加载前先做兼容校验：
	/// 缺少清单 / 缺少 <c>minHostVersion</c> / 宿主版本低于清单要求，一律不加载并在
	/// 偏好设置页标为「不兼容」。校验通过后，每个插件用**独立**的
	/// <see cref="PluginLoadContext"/> 加载自身与私有依赖（同名不同版本互不打架），
	/// 契约/共享组件/Avalonia 等则委托宿主默认上下文，保证类型身份统一。
	///
	/// 兼容两种运行环境：
	///  1) 生产分发——plugins/&lt;AssemblyName&gt;/*.dll 从磁盘经独立 ALC 加载；
	///  2) 测试环境——插件被测试工程静态引用（已在默认上下文），直接复用该程序集，
	///     避免跨上下文类型身份不一致（E2E 用例按类型查找视图控件依赖于此）。
	///
	/// 单个插件加载/实例化失败只记日志不拖垮启动（坏插件不连坐）。
	/// </summary>
	public static class DiffViewPluginLoader
	{
		/// <summary>
		/// v5.1.0：内置插件 Id——随主程序分发（插件仓的 Image / Hex 包，主仓构建期内置）。
		/// 仅用于偏好设置页展示「内置」标签；自 v5.1.0 起内置插件**也可卸载**
		/// （卸载后可从插件仓下载对应插件包重装）。
		/// </summary>
		public static readonly string[] BuiltInPluginIds = new string[2] { "forkplus.image", "forkplus.hex" };

		private static readonly object CatalogSyncRoot = new object();

		/// <summary>加载失败记录（插件目录名 → 错误信息），坏插件单独标记、不连坐。</summary>
		private static readonly List<KeyValuePair<string, string>> LoadFailures = new List<KeyValuePair<string, string>>();

		/// <summary>不兼容插件条目（清单声明的最低宿主版本高于当前宿主）。</summary>
		private static readonly List<DiffViewPluginInfo> IncompatiblePlugins = new List<DiffViewPluginInfo>();

		/// <summary>插件 Id → 插件所在目录（供卸载删除整目录）。</summary>
		private static readonly Dictionary<string, string> PluginDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>插件 Id → 清单（供偏好页展示版本与最低支持主软件版本）。</summary>
		private static readonly Dictionary<string, PluginManifest> PluginManifests = new Dictionary<string, PluginManifest>(StringComparer.OrdinalIgnoreCase);

		/// <summary>本次加载建立的插件加载上下文（重载/卸载时统一 Unload 回收）。</summary>
		private static readonly List<PluginLoadContext> LoadContexts = new List<PluginLoadContext>();

		/// <summary>本次进程经加载器注册成功的插件个数（诊断用）。</summary>
		public static int RegisteredCount { get; private set; }

		/// <summary>默认插件目录：可执行文件旁的 plugins/。</summary>
		public static string DefaultPluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

		/// <summary>
		/// 扫描默认插件目录（App 启动期调用一次）。先套用持久化的禁用列表，
		/// 再扫描注册——禁用状态跨进程会话保持。
		/// </summary>
		public static void LoadDefault()
		{
			ApplyDisabledPluginsFromSettings();
			LoadFromDirectory(DefaultPluginDirectory);
		}

		/// <summary>
		/// v5.0.1：重新加载插件（偏好设置 → 插件页的「重新加载插件」按钮）。
		/// 语义：卸载既有加载上下文 + 重建注册表 + 重新扫描 plugins/ 目录 →
		/// 新增/删除插件与启禁用立即生效，无需重启 ForkPlus。禁用状态与用户绑定跨重载保持。
		/// </summary>
		public static void Reload()
		{
			DiffViewPluginRegistry.ClearPlugins();
			UnloadContexts();
			lock (CatalogSyncRoot)
			{
				LoadFailures.Clear();
				IncompatiblePlugins.Clear();
				PluginDirectories.Clear();
				PluginManifests.Clear();
			}
			RegisteredCount = 0;
			LoadFromDirectory(DefaultPluginDirectory);
			Log.Info($"Diff view plugins reloaded: {RegisteredCount} registered, {LoadFailures.Count} failure(s)");
		}

		/// <summary>v5.0.3：插件是否内置（v5.1.0 起仅用于展示标签，不再限制卸载）。</summary>
		public static bool IsBuiltInPlugin(string pluginId)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return false;
			}
			foreach (string id in BuiltInPluginIds)
			{
				if (string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>v5.1.0：取插件所在目录（plugins/&lt;AssemblyName&gt;；未注册时返回 null）。</summary>
		[Null]
		public static string GetPluginDirectory(string pluginId)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return null;
			}
			lock (CatalogSyncRoot)
			{
				return PluginDirectories.TryGetValue(pluginId, out string directory) ? directory : null;
			}
		}

		/// <summary>v5.1.0：已注册插件 Id → 插件目录的快照。</summary>
		public static Dictionary<string, string> GetPluginDirectories()
		{
			lock (CatalogSyncRoot)
			{
				return new Dictionary<string, string>(PluginDirectories, StringComparer.OrdinalIgnoreCase);
			}
		}

		/// <summary>v5.0.1：用持久化的禁用列表覆盖注册表禁用标记。</summary>
		public static void ApplyDisabledPluginsFromSettings()
		{
			DiffViewPluginRegistry.SetDisabledIds(ForkPlusSettings.Default.DisabledDiffViewPlugins);
		}

		/// <summary>v5.0.1：启用/禁用插件并持久化（只影响路由，注册条目保留）。</summary>
		public static void SetPluginEnabled(string pluginId, bool enabled)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return;
			}
			DiffViewPluginRegistry.SetEnabled(pluginId, enabled);
			List<string> disabled = new List<string>(ForkPlusSettings.Default.DisabledDiffViewPlugins ?? new string[0]);
			disabled.RemoveAll((string id) => string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));
			if (!enabled)
			{
				disabled.Add(pluginId);
			}
			ForkPlusSettings.Default.DisabledDiffViewPlugins = disabled.ToArray();
			ForkPlusSettings.Default.Save();
		}

		/// <summary>
		/// v5.0.1：插件信息快照（供偏好设置 → 插件页展示）。
		/// 成功注册的插件按注册优先序列出（含禁用项），不兼容与加载失败的条目追加在末尾。
		/// </summary>
		public static DiffViewPluginInfo[] GetPluginInfos()
		{
			List<DiffViewPluginInfo> result = new List<DiffViewPluginInfo>();
			foreach (IDiffViewPlugin plugin in DiffViewPluginRegistry.GetRegisteredPlugins())
			{
				bool enabled = DiffViewPluginRegistry.IsEnabled(plugin.Id);
				PluginManifest manifest = GetManifest(plugin.Id);
				result.Add(new DiffViewPluginInfo(
					plugin.Id,
					ResolveDisplayName(plugin),
					ResolveVersion(plugin, manifest),
					ResolveDescription(plugin),
					plugin.Priority,
					plugin.FileExtensions,
					enabled ? DiffViewPluginStatus.Enabled : DiffViewPluginStatus.Disabled,
					string.Empty,
					IsBuiltInPlugin(plugin.Id),
					manifest != null ? manifest.MinHostVersion : string.Empty,
					GetPluginDirectory(plugin.Id) ?? string.Empty));
			}
			DiffViewPluginInfo[] incompatibles;
			KeyValuePair<string, string>[] failures;
			lock (CatalogSyncRoot)
			{
				incompatibles = IncompatiblePlugins.ToArray();
				failures = LoadFailures.ToArray();
			}
			foreach (DiffViewPluginInfo incompatible in incompatibles)
			{
				result.Add(incompatible);
			}
			foreach (KeyValuePair<string, string> failure in failures)
			{
				result.Add(new DiffViewPluginInfo(
					string.Empty,
					failure.Key,
					"—",
					string.Empty,
					0,
					new string[0],
					DiffViewPluginStatus.Failed,
					failure.Value));
			}
			return result.ToArray();
		}

		/// <summary>v5.0.1：确保插件目录存在（「打开插件目录」前调用）。</summary>
		public static string EnsurePluginDirectory()
		{
			string directory = DefaultPluginDirectory;
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			return directory;
		}

		/// <summary>扫描指定目录下的每个插件子目录并注册其中的对比视图插件。</summary>
		public static void LoadFromDirectory(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
			{
				Log.Info("Diff view plugin directory not found: " + (directory ?? "<null>"));
				return;
			}
			string[] pluginDirectories = Directory.GetDirectories(directory);
			int scanned = 0;
			int skipped = 0;
			foreach (string pluginDirectory in pluginDirectories)
			{
				string name = Path.GetFileName(pluginDirectory);
				// 跳过隐藏目录（如 .pending-uninstall 落点等以 '.' 开头的内部目录）。
				if (string.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal))
				{
					continue;
				}
				scanned++;
				// v5.1.0：已被卸载但因文件占用延迟到重启删除的插件目录，本次重载先跳过。
				if (PluginUninstaller.IsPendingDeletion(pluginDirectory))
				{
					skipped++;
					Log.Info("Skipped pending-deletion plugin directory: " + name);
					continue;
				}
				LoadPluginDirectory(pluginDirectory);
			}
			Log.Info($"Diff view plugins loaded from '{directory}': {RegisteredCount} registered, {scanned} plugin directory(ies) scanned, {skipped} pending-deletion skipped");
		}

		private static void LoadPluginDirectory(string pluginDirectory)
		{
			string directoryName = Path.GetFileName(pluginDirectory);
			PluginManifest manifest = PluginManifest.ReadFromDirectory(pluginDirectory);
			if (manifest == null)
			{
				RecordFailure(directoryName, "缺少或非法的 plugin.json 清单（需含 id / version / minHostVersion / assembly）。");
				return;
			}
			if (!PluginManifest.IsHostCompatible(manifest.MinHostVersion))
			{
				string reason = "需要主程序版本 ≥ " + manifest.MinHostVersion + "，当前为 " + PluginManifest.HostVersion + "。";
				Log.Info("Incompatible plugin '" + directoryName + "': " + reason);
				lock (CatalogSyncRoot)
				{
					// 记录目录/清单，使不兼容插件在偏好页也能被卸载（删除其目录后重装兼容版本）。
					if (!string.IsNullOrWhiteSpace(manifest.Id))
					{
						PluginDirectories[manifest.Id] = pluginDirectory;
						PluginManifests[manifest.Id] = manifest;
					}
					IncompatiblePlugins.Add(new DiffViewPluginInfo(
						manifest.Id,
						string.IsNullOrWhiteSpace(manifest.Id) ? directoryName : manifest.Id,
						string.IsNullOrWhiteSpace(manifest.Version) ? "—" : manifest.Version,
						string.Empty,
						0,
						new string[0],
						DiffViewPluginStatus.Incompatible,
						reason,
						IsBuiltInPlugin(manifest.Id),
						manifest.MinHostVersion,
						pluginDirectory));
				}
				return;
			}
			string assemblyPath = Path.Combine(pluginDirectory, manifest.Assembly + ".dll");
			if (!File.Exists(assemblyPath))
			{
				RecordFailure(directoryName, "清单声明的插件程序集缺失：" + manifest.Assembly + ".dll");
				return;
			}
			try
			{
				// 测试环境：插件被测试工程静态引用（默认上下文已有同名程序集）→ 直接复用，
				// 保证类型身份一致（E2E 按类型查找视图控件依赖于此）。
				Assembly assembly = FindDefaultContextAssembly(manifest.Assembly);
				PluginLoadContext context = null;
				if (assembly == null)
				{
					// 生产分发：每个插件一个独立、可回收的加载上下文（私有依赖隔离）。
					context = new PluginLoadContext(pluginDirectory);
					assembly = context.LoadFromAssemblyPath(assemblyPath);
				}
				int count = RegisterPluginsFrom(assembly, manifest, pluginDirectory);
				RegisteredCount += count;
				if (count == 0)
				{
					Log.Info("No IDiffViewPlugin in '" + manifest.Assembly + "' (dependency library or empty plugin, skipped)");
					context?.Unload();
				}
				else if (context != null)
				{
					lock (CatalogSyncRoot)
					{
						LoadContexts.Add(context);
					}
				}
			}
			catch (BadImageFormatException)
			{
				// 插件目录里的原生依赖（如 pdfium.dll）无程序集元数据——静默跳过，不记为「加载失败」。
				Log.Info("Non-managed primary assembly in plugin directory (skipped): " + directoryName);
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load diff view plugin '{pluginDirectory}'", ex);
				RecordFailure(directoryName, ex.Message);
			}
		}

		/// <summary>v5.1.0：在默认上下文中按简单名查找已加载的程序集（测试静态引用场景复用）。</summary>
		[Null]
		private static Assembly FindDefaultContextAssembly(string simpleName)
		{
			if (string.IsNullOrEmpty(simpleName))
			{
				return null;
			}
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)
					&& AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default)
				{
					return assembly;
				}
			}
			return null;
		}

		/// <summary>v5.1.0：卸载全部插件加载上下文（重载前调用；失败只记日志）。</summary>
		private static void UnloadContexts()
		{
			PluginLoadContext[] contexts;
			lock (CatalogSyncRoot)
			{
				contexts = LoadContexts.ToArray();
				LoadContexts.Clear();
			}
			foreach (PluginLoadContext context in contexts)
			{
				try
				{
					context.Unload();
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to unload plugin context '" + context.PluginDirectory + "'", ex);
				}
			}
		}

		[Null]
		private static PluginManifest GetManifest(string pluginId)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return null;
			}
			lock (CatalogSyncRoot)
			{
				return PluginManifests.TryGetValue(pluginId, out PluginManifest manifest) ? manifest : null;
			}
		}

		private static string ResolveDisplayName(IDiffViewPlugin plugin)
		{
			if (plugin is IPluginMetadata metadata)
			{
				// v5.0.3：按宿主当前界面语言取多语言名称（未覆写时默认返回英文原文）。
				string localized = metadata.GetDisplayName(PluginEnvironment.CurrentLanguage);
				if (!string.IsNullOrWhiteSpace(localized))
				{
					return localized;
				}
			}
			string key = plugin.DisplayNameKey;
			string translated = string.IsNullOrEmpty(key) ? null : PluginEnvironment.Translate(key);
			if (!string.IsNullOrWhiteSpace(translated))
			{
				return translated;
			}
			return string.IsNullOrEmpty(key) ? plugin.Id : key;
		}

		private static string ResolveVersion(IDiffViewPlugin plugin, [Null] PluginManifest manifest)
		{
			// v5.1.0：清单声明的插件版本优先（与分发包一致）；其次 IPluginMetadata；最后程序集版本。
			if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Version))
			{
				return manifest.Version;
			}
			if (plugin is IPluginMetadata metadata && !string.IsNullOrWhiteSpace(metadata.Version))
			{
				return metadata.Version;
			}
			Version version = plugin.GetType().Assembly.GetName().Version;
			return version != null ? version.ToString(3) : "1.0.0";
		}

		private static string ResolveDescription(IDiffViewPlugin plugin)
		{
			if (plugin is IPluginMetadata metadata)
			{
				// v5.0.3：按宿主当前界面语言取多语言描述（未覆写时默认返回英文原文）。
				return metadata.GetDescription(PluginEnvironment.CurrentLanguage) ?? string.Empty;
			}
			return string.Empty;
		}

		/// <summary>v5.0.1：记录一次加载失败（供偏好页展示，坏插件不影响其他插件）。</summary>
		private static void RecordFailure(string pluginName, string message)
		{
			lock (CatalogSyncRoot)
			{
				LoadFailures.Add(new KeyValuePair<string, string>(pluginName, message ?? string.Empty));
			}
		}

		/// <summary>扫描程序集中带无参构造的 IDiffViewPlugin 实现，实例化并注册（单个失败不连坐）。</summary>
		private static int RegisterPluginsFrom(Assembly assembly, PluginManifest manifest, string pluginDirectory)
		{
			Type[] types;
			try
			{
				types = assembly.GetExportedTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				types = ex.Types;
			}
			catch
			{
				return 0;
			}
			int count = 0;
			foreach (Type type in types)
			{
				if (type == null || !type.IsClass || type.IsAbstract)
				{
					continue;
				}
				if (!typeof(IDiffViewPlugin).IsAssignableFrom(type))
				{
					continue;
				}
				if (type.GetConstructor(Type.EmptyTypes) == null)
				{
					Log.Error("Diff view plugin '" + type.FullName + "' has no parameterless constructor");
					RecordFailure(Path.GetFileName(pluginDirectory), "插件类型缺少无参构造函数：" + type.FullName);
					continue;
				}
				try
				{
					IDiffViewPlugin plugin = (IDiffViewPlugin)Activator.CreateInstance(type);
					DiffViewPluginRegistry.Register(plugin);
					LogMinHostVersionMismatch(plugin, manifest);
					lock (CatalogSyncRoot)
					{
						PluginDirectories[plugin.Id] = pluginDirectory;
						PluginManifests[plugin.Id] = manifest;
					}
					count++;
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to instantiate diff view plugin '{type.FullName}'", ex);
					RecordFailure(Path.GetFileName(pluginDirectory), "插件实例化失败：" + ex.Message);
				}
			}
			return count;
		}

		/// <summary>清单与代码内的 minHostVersion 不一致时记日志（清单为安装/加载的事实来源）。</summary>
		private static void LogMinHostVersionMismatch(IDiffViewPlugin plugin, PluginManifest manifest)
		{
			if (plugin is IPluginMetadata metadata
				&& !string.IsNullOrWhiteSpace(metadata.MinHostVersion)
				&& !string.Equals(metadata.MinHostVersion, manifest.MinHostVersion, StringComparison.OrdinalIgnoreCase))
			{
				Log.Warn("Plugin '" + plugin.Id + "' declares minHostVersion '" + metadata.MinHostVersion
					+ "' in code but '" + manifest.MinHostVersion + "' in plugin.json; using the manifest value.");
			}
		}
	}
}