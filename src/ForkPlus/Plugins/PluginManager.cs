using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForkPlus.UI.UserControls.BinaryDiff;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：插件声明的某个视图 + 它所属的插件清单。宿主据此把"某个文件的某一侧"
	/// 路由到对应插件进程，并以 <see cref="QualifiedId"/> 在日志/诊断里区分同名视图。
	/// </summary>
	public sealed class PluginViewerHandle
	{
		public PluginManifest Manifest { get; }

		public PluginViewerDescriptor Viewer { get; }

		public string QualifiedId => Manifest.Id + ":" + Viewer.Id;

		public PluginViewerHandle(PluginManifest manifest, PluginViewerDescriptor viewer)
		{
			Manifest = manifest;
			Viewer = viewer;
		}
	}

	/// <summary>
	/// v4.5.0：宿主侧的插件管理器——插件机制的"宿主半边"。
	///
	/// 职责与边界：
	///   1) 启动时扫描插件目录（<see cref="PluginDiscovery"/>），把每个插件声明的视图收集成
	///      <see cref="PluginViewerHandle"/>，并按优先级排序供 <see cref="FindViewer"/> 路由。
	///   2) 惰性拉起插件进程：只有在真正要渲染某个文件时才 <see cref="PluginSession.Start"/>，
	///      避免装了插件但没用到时白起进程。
	///   3) 渲染失败/超时/进程崩溃一律转成"返回 null + 错误文案"，绝不向上抛异常，
	///      也绝不拖垮 UI 线程（契约与 <see cref="PluginSession"/> 一致）。
	///
	/// 隔离（本机制存在的理由）：插件进程与主程序只通过 stdin/stdout 交换 JSON。插件里的
	/// GPL 依赖、原生库、崩溃、内存爆掉都关在它自己的进程里，主程序（MIT）不链接、不加载
	/// 任何插件程序集——这正是把第三方/GPL 对比视图做成插件而非直接编进主程序的价值。
	/// </summary>
	public sealed class PluginManager
	{
		/// <summary>插件根目录可被环境变量覆盖（开发/测试用），否则取程序目录下的 plugins/。</summary>
		public const string PluginsRootEnvironmentVariable = "FORKPLUS_PLUGINS_DIR";

		private const string PluginsDirectoryName = "plugins";

		/// <summary>握手超时：插件冷启动 + 首次加载依赖，给宽一点。</summary>
		private const int HandshakeTimeoutMs = 15000;

		/// <summary>单次渲染超时：超过即杀进程并按失败处理，防止坏插件挂死宿主的打开文件流程。</summary>
		private const int RenderTimeoutMs = 20000;

		public static PluginManager Instance { get; } = new PluginManager();

		private readonly object _sync = new object();

		private readonly List<PluginViewerHandle> _viewers = new List<PluginViewerHandle>();

		private readonly Dictionary<string, PluginSession> _sessions = new Dictionary<string, PluginSession>(StringComparer.Ordinal);

		private bool _initialized;

		private bool _shutdown;

		private string _pluginsRoot;

		private string _hostName;

		private string _hostVersion;

		public bool IsInitialized
		{
			get
			{
				lock (_sync)
				{
					return _initialized;
				}
			}
		}

		public string PluginsRoot
		{
			get
			{
				lock (_sync)
				{
					return _pluginsRoot;
				}
			}
		}

		public IReadOnlyList<PluginViewerHandle> Viewers
		{
			get
			{
				lock (_sync)
				{
					return _viewers.ToArray();
				}
			}
		}

		/// <summary>
		/// v4.5.0：全部已登记插件视图里的最高优先级。桥接查看器（<see cref="PluginBinaryViewer"/>）
		/// 用它参与和内置查看器的排序——插件声明了 priority 250 的视图，整体就必须排到内置动图
		/// （200）之前，否则动图这类被内置高优先级挡住的格式永远轮不到插件接管。
		///
		/// 无插件视图时回落到 150（内置静态图 100 与内置动图 200 之间）；此时桥接查看器
		/// <c>CanHandle</c> 恒为 false，档位不影响任何判定结果。
		/// </summary>
		public int MaxViewerPriority
		{
			get
			{
				lock (_sync)
				{
					int max = 150;
					for (int i = 0; i < _viewers.Count; i++)
					{
						if (_viewers[i].Viewer.Priority > max)
						{
							max = _viewers[i].Viewer.Priority;
						}
					}
					return max;
				}
			}
		}

		/// <summary>默认插件根目录：环境变量优先，否则程序目录下的 plugins/。</summary>
		public static string ResolveDefaultPluginsRoot()
		{
			string overridden = Environment.GetEnvironmentVariable(PluginsRootEnvironmentVariable);
			if (!string.IsNullOrWhiteSpace(overridden))
			{
				return overridden;
			}
			return Path.Combine(AppContext.BaseDirectory, PluginsDirectoryName);
		}

		/// <summary>
		/// 扫描并登记插件。幂等（重复调用直接返回）。单个坏插件只记日志跳过，不影响整批。
		/// 本方法只读清单、不起进程；进程在首次渲染时才拉起。
		/// </summary>
		public void Initialize(string pluginsRoot, string hostName, string hostVersion)
		{
			lock (_sync)
			{
				if (_initialized)
				{
					return;
				}
				_initialized = true;
				_pluginsRoot = pluginsRoot;
				_hostName = hostName;
				_hostVersion = hostVersion;
			}

			List<PluginViewerHandle> handles = new List<PluginViewerHandle>();
			foreach (PluginDiscoveryResult discovered in PluginDiscovery.Discover(pluginsRoot))
			{
				if (!discovered.IsUsable)
				{
					Log.Warn("Skipped plugin '" + discovered.ManifestPath + "': " + discovered.Error);
					continue;
				}
				foreach (PluginViewerDescriptor viewer in discovered.Manifest.ViewersOrEmpty)
				{
					handles.Add(new PluginViewerHandle(discovered.Manifest, viewer));
				}
			}
			// 优先级降序；OrderByDescending 稳定排序，同优先级保持扫描顺序，便于复现。
			handles = handles.OrderByDescending((PluginViewerHandle handle) => handle.Viewer.Priority).ToList();

			lock (_sync)
			{
				_viewers.Clear();
				_viewers.AddRange(handles);
			}

			// 桥接查看器（让宿主按扩展名把格式路由到插件）+ 文件类型认领钩子
			// （让插件格式走与图片相同的"加载字节"路径）。两者都只在初始化后生效。
			BinaryViewerRegistry.Register(new PluginBinaryViewer());
			PathHelper.SetExternalMediaPathClaim(ClaimsPath);

			if (handles.Count > 0)
			{
				Log.Info("Loaded " + handles.Count + " plugin viewer(s) from '" + pluginsRoot + "'.");
			}
		}

		/// <summary>返回认领该路径的视图（扩展名预筛 + 优先级最高者）；无人认领返回 null。</summary>
		public PluginViewerHandle FindViewer(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			lock (_sync)
			{
				if (!_initialized || _shutdown)
				{
					return null;
				}
				for (int i = 0; i < _viewers.Count; i++)
				{
					if (_viewers[i].Viewer.MatchesExtension(path))
					{
						return _viewers[i];
					}
				}
			}
			return null;
		}

		/// <summary>该路径是否由任一插件声明（供 PathHelper 的文件类型认领钩子使用）。</summary>
		public bool ClaimsPath(string path)
		{
			return FindViewer(path) != null;
		}

		/// <summary>
		/// 把一次渲染转交给插件进程。失败返回 null 并给出面向日志/UI 的错误文案，不抛异常。
		/// </summary>
		public RenderResult Render(PluginViewerHandle handle, RenderParams parameters, out string error)
		{
			error = null;
			if (handle == null)
			{
				error = "No plugin viewer was resolved for this file.";
				return null;
			}
			PluginSession session = GetOrStartSession(handle.Manifest, out error);
			if (session == null)
			{
				return null;
			}
			RenderResult result = session.Render(parameters, RenderTimeoutMs, out PluginError pluginError);
			if (result == null)
			{
				error = pluginError?.ToString() ?? "Plugin render failed.";
				// 会话已死（超时被杀 / 崩溃 / 报文损坏）：丢弃缓存，下次重新拉起。
				if (!session.IsAlive)
				{
					DropSession(handle.Manifest.Id);
				}
				return null;
			}
			return result;
		}

		/// <summary>退出时释放全部插件进程并撤掉桥接（注册表里的桥接查看器随之失效）。</summary>
		public void Shutdown()
		{
			PluginSession[] sessions;
			lock (_sync)
			{
				_shutdown = true;
				sessions = _sessions.Values.ToArray();
				_sessions.Clear();
				_viewers.Clear();
			}
			foreach (PluginSession session in sessions)
			{
				try
				{
					session.Dispose();
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to dispose plugin session", ex);
				}
			}
			PathHelper.SetExternalMediaPathClaim(null);
		}

		private PluginSession GetOrStartSession(PluginManifest manifest, out string error)
		{
			error = null;
			string hostName;
			string hostVersion;
			lock (_sync)
			{
				if (_shutdown)
				{
					error = "Plugin subsystem has been shut down.";
					return null;
				}
				if (_sessions.TryGetValue(manifest.Id, out PluginSession existing) && existing.IsAlive)
				{
					return existing;
				}
				hostName = _hostName;
				hostVersion = _hostVersion;
			}
			// 起进程不持锁：握手最长十几秒，持锁会把并发的其它插件渲染一起堵住。
			PluginSession started = PluginSession.Start(manifest, hostName, hostVersion, HandshakeTimeoutMs, out string startError);
			if (started == null)
			{
				error = startError;
				Log.Warn("Failed to start plugin '" + manifest.Id + "': " + startError);
				return null;
			}
			lock (_sync)
			{
				if (_shutdown)
				{
					started.Dispose();
					error = "Plugin subsystem has been shut down.";
					return null;
				}
				// 并发下可能已有别的线程先建好会话：保留先到的，丢弃后到的。
				if (_sessions.TryGetValue(manifest.Id, out PluginSession winner) && winner.IsAlive)
				{
					started.Dispose();
					return winner;
				}
				_sessions[manifest.Id] = started;
			}
			return started;
		}

		private void DropSession(string pluginId)
		{
			PluginSession removed;
			lock (_sync)
			{
				if (!_sessions.TryGetValue(pluginId, out removed))
				{
					return;
				}
				_sessions.Remove(pluginId);
			}
			try
			{
				removed.Dispose();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to dispose a dead plugin session", ex);
			}
		}
	}
}