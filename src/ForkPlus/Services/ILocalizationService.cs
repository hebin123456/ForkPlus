namespace ForkPlus.Services
{
	/// <summary>
	/// 本地化抽象（替换对 <c>ForkPlus.UI.UserControls.Preferences.PreferencesLocalization</c> 静态类的直接调用）。
	/// 该静态类位于 UI 层并 <c>using System.Windows.*</c>，领域层（如 <c>OpenAiService</c>）直接引用它
	/// 会传递依赖 WPF。本接口只暴露领域层用到的文案查询方法，UI 层的 <c>Apply</c>/<c>ApplyElement</c>
	/// 等 DependencyObject 操作仍由 View 直接调用静态类。
	/// WPF 实现委托到 <see cref="ForkPlus.UI.UserControls.Preferences.PreferencesLocalization"/>；
	/// Avalonia 实现需复制翻译字典加载逻辑或共享底层翻译存储。
	/// </summary>
	public interface ILocalizationService
	{
		/// <summary>按当前 UI 语言翻译文案，无翻译时返回原文。</summary>
		string Current(string text);

		/// <summary>按指定语言翻译文案，无翻译时返回原文。</summary>
		string Translate(string text, string language);

		/// <summary>按当前 UI 语言翻译并格式化文案。</summary>
		string FormatCurrent(string text, params object[] args);
	}
}
