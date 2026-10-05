using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.0.0：对比视图插件注册表（宿主侧）。
	/// 路由规则：用户绑定（BindExtension，最高）&gt; 精确扩展名（按插件优先级）&gt; 通配 "*"（按优先级）。
	/// 注册表初始为空——插件一律由 <see cref="DiffViewPluginLoader"/> 启动期从 plugins/ 目录
	/// 动态加载注册（内置 Image/Hex 两个 DLL + 第三方），宿主不编译期绑定任何具体插件。
	/// </summary>
	public static class DiffViewPluginRegistry
	{
		private static readonly object SyncRoot = new object();

		private static readonly List<IDiffViewPlugin> Plugins = new List<IDiffViewPlugin>();

		/// <summary>用户绑定：扩展名（小写含点）→ 插件 Id。覆盖一切自动路由。</summary>
		private static readonly Dictionary<string, string> UserBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// v5.0.1：被用户禁用的插件 Id 集合。禁用只把插件排除出路由，注册条目仍保留
		/// （偏好设置 → 插件页需要展示其名称/版本/描述，可随时重新启用）。
		/// </summary>
		private static readonly HashSet<string> DisabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>注册插件（同 Id 替换），并按优先级降序保持顺序（同级则后注册的排在后面）。</summary>
		public static void Register(IDiffViewPlugin plugin)
		{
			if (plugin == null)
			{
				return;
			}
			lock (SyncRoot)
			{
				Plugins.RemoveAll((IDiffViewPlugin existing) => existing.Id == plugin.Id);
				int index = 0;
				while (index < Plugins.Count && Plugins[index].Priority >= plugin.Priority)
				{
					index++;
				}
				Plugins.Insert(index, plugin);
			}
		}

		/// <summary>清空全部插件、用户绑定与禁用标记（诊断/测试用；正常流程插件由加载器注册）。</summary>
		public static void Clear()
		{
			lock (SyncRoot)
			{
				Plugins.Clear();
				UserBindings.Clear();
				DisabledIds.Clear();
			}
		}

		/// <summary>
		/// v5.0.1：仅清空已注册插件，保留用户绑定与禁用标记。
		/// 供「重新加载插件」重建注册表时使用（禁用状态跨重载保持）。
		/// </summary>
		public static void ClearPlugins()
		{
			lock (SyncRoot)
			{
				Plugins.Clear();
			}
		}

		/// <summary>v5.0.1：插件是否参与路由（未被禁用即启用）。</summary>
		public static bool IsEnabled(string pluginId)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return false;
			}
			lock (SyncRoot)
			{
				return !DisabledIds.Contains(pluginId);
			}
		}

		/// <summary>v5.0.1：启用/禁用插件（只影响路由，注册条目保留）。</summary>
		public static void SetEnabled(string pluginId, bool enabled)
		{
			if (string.IsNullOrEmpty(pluginId))
			{
				return;
			}
			lock (SyncRoot)
			{
				if (enabled)
				{
					DisabledIds.Remove(pluginId);
				}
				else
				{
					DisabledIds.Add(pluginId);
				}
			}
		}

		/// <summary>v5.0.1：用持久化的禁用列表整体覆盖当前禁用标记（启动/重载前调用）。</summary>
		public static void SetDisabledIds(IEnumerable<string> pluginIds)
		{
			lock (SyncRoot)
			{
				DisabledIds.Clear();
				if (pluginIds == null)
				{
					return;
				}
				foreach (string pluginId in pluginIds)
				{
					if (!string.IsNullOrEmpty(pluginId))
					{
						DisabledIds.Add(pluginId);
					}
				}
			}
		}

		/// <summary>用户把扩展名绑定到指定插件（覆盖自动路由）；pluginId 不存在时忽略。</summary>
		public static void BindExtension(string extension, string pluginId)
		{
			if (string.IsNullOrEmpty(extension) || pluginId == null)
			{
				return;
			}
			lock (SyncRoot)
			{
				string normalized = NormalizeExtension(extension);
				if (FindPluginById(pluginId) != null)
				{
					UserBindings[normalized] = pluginId;
				}
			}
		}

		/// <summary>解除扩展名的用户绑定（回到自动路由）。</summary>
		public static void UnbindExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return;
			}
			lock (SyncRoot)
			{
				UserBindings.Remove(NormalizeExtension(extension));
			}
		}

		/// <summary>当前已注册插件快照（按优先级降序；供绑定 UI 列表）。</summary>
		public static IDiffViewPlugin[] GetRegisteredPlugins()
		{
			lock (SyncRoot)
			{
				return Plugins.ToArray();
			}
		}

		/// <summary>
		/// 按文件路径解析对比视图插件。路由顺序：用户绑定 &gt; 精确扩展名（优先级）&gt; 通配（优先级）。
		/// 内置通配兜底恒命中，正常不返回 null；返回 null 表示无任何插件可用（调用方自行回退）。
		/// </summary>
		[Null]
		public static IDiffViewPlugin Resolve([Null] string path, long? srcSize = null, long? dstSize = null)
		{
			DiffViewRequest request = new DiffViewRequest(path, srcSize, dstSize);
			string extension = null;
			if (!string.IsNullOrEmpty(path))
			{
				extension = NormalizeExtension(Path.GetExtension(path));
			}
			IDiffViewPlugin[] snapshot;
			string boundId = null;
			lock (SyncRoot)
			{
				snapshot = Plugins.ToArray();
				if (extension != null)
				{
					UserBindings.TryGetValue(extension, out boundId);
				}
			}
			// 1) 用户绑定
			if (boundId != null)
			{
				IDiffViewPlugin bound = FindPluginById(boundId, snapshot);
				if (bound != null && IsEnabled(bound.Id) && bound.CanHandle(request))
				{
					return bound;
				}
			}
			// 2) 精确扩展名（按优先级，快照已排序）
			if (extension != null)
			{
				foreach (IDiffViewPlugin plugin in snapshot)
				{
					if (IsEnabled(plugin.Id) && HasExtension(plugin, extension) && plugin.CanHandle(request))
					{
						return plugin;
					}
				}
			}
			// 3) 通配兜底（按优先级）
			foreach (IDiffViewPlugin plugin2 in snapshot)
			{
				if (IsEnabled(plugin2.Id) && HasWildcard(plugin2) && plugin2.CanHandle(request))
				{
					return plugin2;
				}
			}
			return null;
		}

		[Null]
		private static IDiffViewPlugin FindPluginById(string pluginId)
		{
			return FindPluginById(pluginId, GetRegisteredPlugins());
		}

		[Null]
		private static IDiffViewPlugin FindPluginById(string pluginId, IDiffViewPlugin[] snapshot)
		{
			foreach (IDiffViewPlugin plugin in snapshot)
			{
				if (plugin.Id == pluginId)
				{
					return plugin;
				}
			}
			return null;
		}

		private static bool HasExtension(IDiffViewPlugin plugin, string extension)
		{
			foreach (string ext in plugin.FileExtensions)
			{
				if (string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static bool HasWildcard(IDiffViewPlugin plugin)
		{
			foreach (string ext in plugin.FileExtensions)
			{
				if (ext == "*")
				{
					return true;
				}
			}
			return false;
		}

		private static string NormalizeExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return extension;
			}
			extension = extension.ToLowerInvariant();
			return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
		}
	}
}
