namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// v5.0.1：插件元数据契约（可选实现）。偏好设置 → 插件页据此展示插件的
	/// 名称、版本号与描述；未实现本接口的插件由宿主回退到 <see cref="IDiffViewPlugin.DisplayNameKey"/>
	/// 的翻译结果 + 程序集版本 + 空描述，不强求第三方改造。
	///
	/// 版本号是插件自身的版本（与宿主版本解耦），建议语义化版本（如 "1.0.0"）。
	/// v5.0.1 的名称与描述固定为中文，元数据国际化（多语言名称/描述）留待后续版本。
	/// </summary>
	public interface IPluginMetadata
	{
		/// <summary>插件自身版本号，如 "1.0.0"。</summary>
		string Version { get; }

		/// <summary>插件显示名（5.0.1 固定中文）。</summary>
		string DisplayName { get; }

		/// <summary>一句话描述插件能力（5.0.1 固定中文）。</summary>
		string Description { get; }
	}
}