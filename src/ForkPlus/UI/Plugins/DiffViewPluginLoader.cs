using System;
using System.IO;
using System.Reflection;

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

		/// <summary>本次进程经加载器注册成功的插件个数（诊断用）。</summary>
		public static int RegisteredCount { get; private set; }

		/// <summary>默认插件目录：可执行文件旁的 plugins/。</summary>
		public static string DefaultPluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

		/// <summary>扫描默认插件目录（App 启动期调用一次）。</summary>
		public static void LoadDefault()
		{
			LoadFromDirectory(DefaultPluginDirectory);
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
				int count = RegisterPluginsFrom(assembly);
				RegisteredCount += count;
				if (count == 0)
				{
					Log.Info("No IDiffViewPlugin in '" + Path.GetFileName(path) + "' (dependency library, skipped)");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to load diff view plugin '{path}'", ex);
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
		private static int RegisterPluginsFrom(Assembly assembly)
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
				if (!typeof(global::ForkPlus.Plugins.Abstractions.IDiffViewPlugin).IsAssignableFrom(type))
				{
					continue;
				}
				if (type.GetConstructor(Type.EmptyTypes) == null)
				{
					Log.Error("Diff view plugin '" + type.FullName + "' has no parameterless constructor");
					continue;
				}
				try
				{
					DiffViewPluginRegistry.Register((global::ForkPlus.Plugins.Abstractions.IDiffViewPlugin)Activator.CreateInstance(type));
					count++;
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to instantiate diff view plugin '{type.FullName}'", ex);
				}
			}
			return count;
		}
	}
}
