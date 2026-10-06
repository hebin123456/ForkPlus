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
		Failed
	}

	/// <summary>
	/// v5.0.1：插件信息快照（宿主 → 偏好设置 UI 的只读视图模型）。
	/// 名称/版本/描述来自 <see cref="global::ForkPlus.Plugins.Abstractions.IPluginMetadata"/>；
	/// 插件未实现该接口时回退到显示名翻译 + 程序集版本 + 空描述。
	/// </summary>
	public sealed class DiffViewPluginInfo
	{
		public string Id { get; }

		public string Name { get; }

		public string Version { get; }

		public string Description { get; }

		public int Priority { get; }

		public IReadOnlyList<string> FileExtensions { get; }

		public DiffViewPluginStatus Status { get; }

		/// <summary>加载失败原因（仅 <see cref="DiffViewPluginStatus.Failed"/> 时非空）。</summary>
		public string Error { get; }

		/// <summary>是否允许切换启用/禁用（加载失败的插件不可切换）。</summary>
		public bool CanToggle => Status != DiffViewPluginStatus.Failed;

		/// <summary>v5.0.3：是否为宿主自带的内置插件（内置插件不可卸载）。</summary>
		public bool IsBuiltIn { get; }

		/// <summary>v5.0.3：是否允许卸载（内置插件与加载失败的条目不可卸载）。</summary>
		public bool CanUninstall => !IsBuiltIn && Status != DiffViewPluginStatus.Failed && !string.IsNullOrEmpty(Id);

		public DiffViewPluginInfo(string id, string name, string version, string description, int priority, IReadOnlyList<string> fileExtensions, DiffViewPluginStatus status, string error, bool isBuiltIn = false)
		{
			Id = id ?? string.Empty;
			Name = name ?? string.Empty;
			Version = version ?? string.Empty;
			Description = description ?? string.Empty;
			Priority = priority;
			FileExtensions = fileExtensions ?? new string[0];
			Status = status;
			Error = error ?? string.Empty;
			IsBuiltIn = isBuiltIn;
		}
	}
}