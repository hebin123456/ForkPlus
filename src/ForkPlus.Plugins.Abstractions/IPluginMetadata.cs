namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// 插件元数据契约（可选实现）。偏好设置 → 插件页据此展示插件的
	/// 名称、版本号与描述；未实现本接口的插件由宿主回退到 <see cref="IDiffViewPlugin.DisplayNameKey"/>
	/// 的翻译结果 + 程序集版本 + 空描述，不强求第三方改造。
	///
	/// 版本号是插件自身的版本（与宿主版本解耦），建议语义化版本（如 "1.0.0"）。
	///
	/// v5.0.3 起支持多语言：<see cref="DisplayName"/> / <see cref="Description"/> 约定为
	/// <strong>英文原文（缺省语言）</strong>；插件按需覆写 <see cref="GetDisplayName"/> /
	/// <see cref="GetDescription"/>，依据宿主下发的界面语言（<see cref="global::ForkPlus.Plugins.PluginEnvironment.CurrentLanguage"/>）
	/// 返回对应译文，并可用 <see cref="PluginLocalization.Resolve"/> 做「当前语言 → 英文」回退。
	/// 两个方法均带默认实现——按旧契约编译的插件零改动即可运行（自动显示英文原文）。
	/// </summary>
	public interface IPluginMetadata
	{
		/// <summary>插件自身版本号，如 "1.0.0"。</summary>
		string Version { get; }

		/// <summary>插件显示名（英文原文，同时作为多语言的缺省值）。</summary>
		string DisplayName { get; }

		/// <summary>一句话描述插件能力（英文原文，同时作为多语言的缺省值）。</summary>
		string Description { get; }

		/// <summary>
		/// v5.0.3：按界面语言取显示名。默认返回英文原文 <see cref="DisplayName"/>，
		/// 未提供多语言的插件无需覆写。
		/// </summary>
		string GetDisplayName(string language)
		{
			return DisplayName;
		}

		/// <summary>
		/// v5.0.3：按界面语言取描述。默认返回英文原文 <see cref="Description"/>，
		/// 未提供多语言的插件无需覆写。
		/// </summary>
		string GetDescription(string language)
		{
			return Description;
		}
	}
}
