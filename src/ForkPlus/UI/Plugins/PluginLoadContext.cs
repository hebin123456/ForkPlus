using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.1.0：单个插件的独立 <see cref="AssemblyLoadContext"/>（可回收）。
	///
	/// 每个插件目录（<c>plugins/&lt;AssemblyName&gt;/</c>）用**自己的**加载上下文，
	/// 从而做到：
	///  1) 私有依赖隔离——两个插件各带一份不同版本的同一依赖时互不打架（同名程序集各加载各的）；
	///  2) 卸载干净——删目录 + 卸载上下文后，该插件加载的全部程序集可被回收。
	///
	/// 共享程序集（契约 ForkPlus.Plugins.Abstractions、共享视图组件 ForkPlus.Plugins.Ui、
	/// 宿主 ForkPlus，以及 Avalonia / SkiaSharp / HarfBuzzSharp / MicroCom / NLog 等）**委托宿主
	/// 默认上下文**解析（<see cref="Load"/> 返回 null 即回落到 Default），保证与宿主是同一份
	/// 类型身份（is/as、接口实现判定才成立）。其余程序集只在插件自身目录内查找。
	/// </summary>
	internal sealed class PluginLoadContext : AssemblyLoadContext
	{
		/// <summary>必须与宿主共享同一份类型身份的程序集简单名（全名匹配）。</summary>
		private static readonly string[] SharedAssemblyNames = new string[5]
		{
			"ForkPlus",
			"ForkPlus.Plugins.Abstractions",
			"ForkPlus.Plugins.Ui",
			"NLog",
			"Newtonsoft.Json"
		};

		/// <summary>必须共享的宿主框架/UI 程序集前缀（版本漂移会导致 XAML/原生互操作崩溃）。</summary>
		private static readonly string[] SharedAssemblyPrefixes = new string[5]
		{
			"Avalonia",
			"SkiaSharp",
			"HarfBuzzSharp",
			"MicroCom",
			"NuGet.Versioning"
		};

		private readonly string _pluginDirectory;

		public PluginLoadContext(string pluginDirectory)
			: base("ForkPlusPlugin:" + Path.GetFileName(pluginDirectory), isCollectible: true)
		{
			_pluginDirectory = pluginDirectory;
		}

		/// <summary>插件目录（卸载时据此删目录）。</summary>
		public string PluginDirectory => _pluginDirectory;

		protected override Assembly Load(AssemblyName assemblyName)
		{
			string simpleName = assemblyName.Name;
			if (string.IsNullOrEmpty(simpleName) || IsSharedAssembly(simpleName))
			{
				// 共享程序集：显式复用默认上下文里已加载的同一份（保证类型身份一致，且规避插件
				// 编译期程序集版本高于宿主时默认绑定失败的问题）；未加载则返回 null 回落默认解析。
				return FindDefaultContextAssembly(simpleName);
			}
			string candidate = Path.Combine(_pluginDirectory, simpleName + ".dll");
			if (File.Exists(candidate))
			{
				return LoadFromAssemblyPath(candidate);
			}
			// 插件目录没有则回落默认上下文（框架程序集等）。
			return null;
		}

		/// <summary>在默认上下文按简单名查找已加载的程序集（找不返回 null，由运行时回落默认解析）。</summary>
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
					&& GetLoadContext(assembly) == Default)
				{
					return assembly;
				}
			}
			return null;
		}

		private static bool IsSharedAssembly(string simpleName)
		{
			foreach (string shared in SharedAssemblyNames)
			{
				if (string.Equals(shared, simpleName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			foreach (string prefix in SharedAssemblyPrefixes)
			{
				if (simpleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
	}
}