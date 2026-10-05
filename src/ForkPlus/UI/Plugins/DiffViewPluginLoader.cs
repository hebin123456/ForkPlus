using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ForkPlus.Plugins;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Settings;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.0.0：对比视图插件的动态加载器。启动期扫描可执行文件旁的 plugins/ 目录，
	/// 逐个 DLL 发现 <see cref="global::ForkPlus.Plugins.Abstractions.IDiffViewPlugin"/>
	/// 实现并注册进 <see cref="DiffViewPluginRegistry"/>——一个插件一个 DLL
	/// （内置 ForkPlus.Plugins.Image.dll / ForkPlus.Plugins.Hex.dll，第三方同名丢入即生效）。
	/// 宿主与插件之间只剩 ForkPlus.Plugins.Abstractions（契约）+ ForkPlus.Plugins.Ui
	/// （共享视图组件）两个静态引用，不再编译期绑定任何具体插件。
	///
	/// 加载语义（两步走，保证类型身份统一）：
	///  1) 先按程序集名 <see cref="Assembly.Load(AssemblyName)"/>——命中宿主 deps.json
	///     已可解析的同一程序集时直接复用（测试环境静态引用插件工程的场景）；
	///  2) 失败再 <see cref="Assembly.LoadFrom(string)"/> 从 plugins/ 加载（生产分发场景）。
	/// 插件的依赖（契约/共享组件/Avalonia/SkiaSharp…）优先从宿主目录解析——与宿主共享
	/// 同一份程序集；plugins/ 里随第三方插件一起放的依赖 DLL 经 Default.Resolving 兜底
	/// 也能被解析（默认探测不覆盖 plugins/ 子目录）。同名程序集以先加载者为准，不支持
	/// 多版本并存——插件生态约定与宿主同版本 Avalonia（与 OxyPlot.Avalonia fork 同款约束）。
	/// 单个 DLL 加载/实例化失败只记日志不拖垮启动（坏插件不连坐）。
	/// </summary>
	public static class DiffViewPluginLoader
	{
		private static bool _resolvingHooked;

		/// <summary>v5.0.1：加载失败记录（DLL 文件名 → 错误信息），坏插件单独标记、不连坐。</summary>
		private static readonly List<KeyValuePair<string, string>> LoadFailures = new List<KeyValuePair<string, string>>();

		private static readonly object CatalogSyncRoot = new object();

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
		/// 语义：重建注册表 + 重新扫描 plugins/ 目录 → 新增/删除插件与启禁用立即生效，
		/// 无需重启 ForkPlus。禁用状态与用户绑定跨重载保持。
		///
		/// 边界（沿用 v5.0.0 加载器约束）：宿主用默认 AssemblyLoadContext，
		/// 同名程序集无法卸载——用新版本 DLL 覆盖同名插件后，同进程内不保证生效
		/// （真·DLL 热替换需 collectible ALC，非本版本范围）。
		/// </summary>
		public static void Reload()
		{
			DiffViewPluginRegistry.ClearPlugins();
			lock (CatalogSyncRoot)
			{
				LoadFailures.Clear();
			}
			RegisteredCount = 0;
			LoadFromDirectory(DefaultPluginDirectory);
			Log.Info($"Diff view plugins reloaded: {RegisteredCount} registered, {LoadFailures.Count} failure(s)");
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
		/// 成功注册的插件按注册优先序列出（含禁用项），失败的 DLL 追加在末尾。
		/// </summary>
		public static DiffViewPluginInfo[] GetPluginInfos()
		{
			List<DiffViewPluginInfo> result = new List<DiffViewPluginInfo>();
			foreach (IDiffViewPlugin plugin in DiffViewPluginRegistry.GetRegisteredPlugins())
			{
				bool enabled = DiffViewPluginRegistry.IsEnabled(plugin.Id);
				result.Add(new DiffViewPluginInfo(
					plugin.Id,
					ResolveDisplayName(plugin),
					ResolveVersion(plugin),
					ResolveDescription(plugin),
					plugin.Priority,
					plugin.FileExtensions,
					enabled ? DiffViewPluginStatus.Enabled : DiffViewPluginStatus.Disabled,
					string.Empty));
			}
			KeyValuePair<string, string>[] failures;
			lock (CatalogSyncRoot)
			{
				failures = LoadFailures.ToArray();
			}
			foreach (KeyValuePair<string, string> failure in failures)
			{
				result.Add(new DiffViewPluginInfo(
					string.Empty,
					Path.GetFileNameWithoutExtension(failure.Key),
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

		private static string ResolveDisplayName(IDiffViewPlugin plugin)
		{
			if (plugin is IPluginMetadata metadata && !string.IsNullOrWhiteSpace(metadata.DisplayName))
			{
				return metadata.DisplayName;
			}
			string key = plugin.DisplayNameKey;
			string translated = string.IsNullOrEmpty(key) ? null : PluginEnvironment.Translate(key);
			if (!string.IsNullOrWhiteSpace(translated))
			{
				return translated;
			}
			return string.IsNullOrEmpty(key) ? plugin.Id : key;
		}

		private static string ResolveVersion(IDiffViewPlugin plugin)
		{
			if (plugin is IPluginMetadata metadata && !string.IsNullOrWhiteSpace(metadata.Version))
			{
				return metadata.Version;
			}
			Version version = plugin.GetType().Assembly.GetName().Version;
			return version != null ? version.ToString(3) : "1.0.0";
		}

		private static string ResolveDescription(IDiffViewPlugin plugin)
		{
			return plugin is IPluginMetadata metadata ? (metadata.Description ?? string.Empty) : string.Empty;
		}

		/// <summary>扫描指定目录下的所有 DLL 并注册其中发现的对比视图插件。</summary>
		public static void LoadFromDirectory(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
			{
				Log.Info("Diff view plugin directory not found: " + (directory ?? "<null>"));
				return;
			}
			HookResolving(directory);
			string[] dllFiles = Directory.GetFiles(directory, "*.dll");
			foreach (string dllFile in dllFiles)
			{
				LoadAssemblyFile(dllFile);
			}
			Log.Info($"Diff view plugins loaded from '{directory}': {RegisteredCount} registered, {dllFiles.Length} file(s) scanned");
		}

		/// <summary>
		/// .NET 默认探测（宿主目录 + deps.json）不覆盖 plugins/ 子目录——第三方插件随包
		/// 放进 plugins/ 的私有依赖在此兜底解析。契约/共享组件/Avalonia 等宿主已有的
		/// 程序集永远走默认解析（先于本钩子），类型身份不受影响。
		/// </summary>
		private static void HookResolving(string directory)
		{
			if (_resolvingHooked)
			{
				return;
			}
			_resolvingHooked = true;
			global::System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += delegate (global::System.Runtime.Loader.AssemblyLoadContext context, AssemblyName name)
			{
				try
				{
					string candidate = Path.Combine(directory, name.Name + ".dll");
					return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
				}
				catch
				{
					return null;
				}
			};
		}

		private static void LoadAssemblyFile(string path)
		{
			try
			{
				Assembly assembly = LoadAssembly(path);
				int count = RegisterPluginsFrom(assembly, path);
				RegisteredCount += count;
				if (count == 0)
				{
					Log.Info("No IDiffViewPlugin in '" + Path.GetFileName(path) + "' (dependency library, skipped)");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load diff view plugin '{path}'", ex);
				RecordFailure(path, ex.Message);
			}
		}

		/// <summary>v5.0.1：记录一次加载失败（供偏好页展示，坏插件不影响其他插件）。</summary>
		private static void RecordFailure(string path, string message)
		{
			lock (CatalogSyncRoot)
			{
				LoadFailures.Add(new KeyValuePair<string, string>(Path.GetFileName(path), message ?? string.Empty));
			}
		}

		/// <summary>两步加载：按名优先复用已加载/可探测的程序集（测试静态引用场景），失败再从磁盘加载。</summary>
		private static Assembly LoadAssembly(string path)
		{
			AssemblyName name = AssemblyName.GetAssemblyName(path);
			try
			{
				return Assembly.Load(name);
			}
			catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException)
			{
				return Assembly.LoadFrom(path);
			}
		}

		/// <summary>扫描程序集中带无参构造的 IDiffViewPlugin 实现，实例化并注册（单个失败不连坐）。</summary>
		private static int RegisterPluginsFrom(Assembly assembly, string path)
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
					RecordFailure(path, "插件类型缺少无参构造函数：" + type.FullName);
					continue;
				}
				try
				{
					DiffViewPluginRegistry.Register((IDiffViewPlugin)Activator.CreateInstance(type));
					count++;
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to instantiate diff view plugin '{type.FullName}'", ex);
					RecordFailure(path, "插件实例化失败：" + ex.Message);
				}
			}
			return count;
		}
	}
}
