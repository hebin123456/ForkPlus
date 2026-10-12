using System.Collections.Generic;

namespace ForkPlus.UI.Plugins
{
	/// <summary>v5.0.1：插件在偏好设置 → 插件页的展示状态。</summary>
	public enum DiffViewPluginStatus
	{
		/// <summary>已加载并参与路由。</summary>
		Enabled,
		/// <summary>已被用户禁用，保留条目但不参与路由。</summary>
		Disabled,
		/// <summary>DLL 加载 / 实例化失败，不可切换。</summary>
		Failed,
		/// <summary>v5.1.0：清单声明的最低宿主版本高于当前宿主（不兼容），不加载、不可启用。</summary>
		Incompatible
	}

	/// <summary>
	/// v5.0.1：插件信息快照（宿主 → 偏好设置 UI 的只读视图模型）。
	/// 名称/版本/描述来自 <see cref="global::ForkPlus.Plugins.Abstractions.IPluginMetadata"/>；
	/// 插件未实现该接口时回退到显示名翻译 + 程序集版本 + 空描述。
	/// v5.1.0：新增插件自身版本（清单优先）、最低支持主软件版本与来源目录。
	/// </summary>
	public sealed class DiffViewPluginInfo
	{
		public string Id { get; }

		public string Name { get; }

		public string Version { get; }

		/// <summary>v5.1.0：清单声明的最低支持主软件版本（缺失时为 "—"）。</summary>
		public string MinHostVersion { get; }

		public string Description { get; }

		public int Priority { get; }

		public IReadOnlyList<string> FileExtensions { get; }

		public DiffViewPluginStatus Status { get; }

		/// <summary>加载失败/不兼容原因（仅 Failed / Incompatible 时非空）。</summary>
		public string Error { get; }

		/// <summary>是否允许切换启用/禁用（加载失败或不兼容的插件不可切换）。</summary>
		public bool CanToggle => Status == DiffViewPluginStatus.Enabled || Status == DiffViewPluginStatus.Disabled;

		/// <summary>v5.0.3：是否为宿主自带的内置插件（v5.1.0 起仅作展示标签，不再限制卸载）。</summary>
		public bool IsBuiltIn { get; }

		/// <summary>v5.1.0：插件所在目录（plugins/&lt;AssemblyName&gt;）；卸载即删除该目录。</summary>
		public string SourceDirectory { get; }

		/// <summary>
		/// v5.1.0：是否允许卸载——只要条目有明确的插件 Id 且不是加载失败的坏条目即可卸载
		/// （内置插件自 v5.1.0 起也可卸载，卸载后可用插件仓的对应插件包重装）。
		/// </summary>
		public bool CanUninstall => Status != DiffViewPluginStatus.Failed && !string.IsNullOrEmpty(Id);

		public DiffViewPluginInfo(string id, string name, string version, string description, int priority, IReadOnlyList<string> fileExtensions, DiffViewPluginStatus status, string error, bool isBuiltIn = false, string minHostVersion = "", string sourceDirectory = "")
		{
			Id = id ?? string.Empty;
			Name = name ?? string.Empty;
			Version = version ?? string.Empty;
			MinHostVersion = minHostVersion ?? string.Empty;
			Description = description ?? string.Empty;
			Priority = priority;
			FileExtensions = fileExtensions ?? new string[0];
			Status = status;
			Error = error ?? string.Empty;
			IsBuiltIn = isBuiltIn;
			SourceDirectory = sourceDirectory ?? string.Empty;
		}
	}
}