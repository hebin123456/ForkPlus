using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的本地化服务抽象。
	/// 当前主工程的 <c>PreferencesLocalization</c> 混合了纯字符串方法（5 个）和 WPF 树操作方法（Apply*，3 个）。
	/// 本接口抽出纯字符串方法，让业务层（Core）可用，UI 层的 Apply* 方法留主工程。
	///
	/// 实施策略（Phase 0.3+）：
	/// 1. 主工程实现 WpfLocalizationService，包装现有 PreferencesLocalization 的纯字符串逻辑。
	/// 2. ServiceLocator.Initialize 时注入实例。
	/// 3. 业务层把 PreferencesLocalization.Current(...) 改为 ServiceLocator.Localization.Current(...)。
	/// 4. 影响：100 个文件、433 处调用（最大耦合点）。
	/// </summary>
	public interface ILocalizationService
	{
		/// <summary>
		/// 取当前激活语言的翻译文本（等价于 PreferencesLocalization.Current）。
		/// </summary>
		string Current(string text);

		/// <summary>
		/// 取指定语言的翻译文本（等价于 PreferencesLocalization.Translate）。
		/// </summary>
		string Translate(string text, string language);

		/// <summary>
		/// 格式化当前激活语言的翻译文本（等价于 PreferencesLocalization.FormatCurrent）。
		/// </summary>
		string FormatCurrent(string text, params object[] args);

		/// <summary>
		/// 菜单头格式（带快捷键 _ 前缀处理，等价于 PreferencesLocalization.MenuHeader）。
		/// </summary>
		string MenuHeader(string text);

		/// <summary>
		/// 格式化菜单头（等价于 PreferencesLocalization.FormatMenuHeader）。
		/// </summary>
		string FormatMenuHeader(string text, params object[] args);
	}
}
