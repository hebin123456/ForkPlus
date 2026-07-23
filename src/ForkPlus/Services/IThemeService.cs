using Avalonia.Media;

namespace ForkPlus.Services
{
	/// <summary>
	/// 主题字典刷新与系统强调色抽象（阶段 4 里程碑 4.3）。
	/// 替换 WPF <c>Application.Current.Resources.MergedDictionaries.Add/Remove</c>
	/// 强制 <c>DynamicResource</c> 失效的机制，以及 <c>SystemThemeHelper.GetSystemBrush</c>。
	/// </summary>
	public interface IThemeService
	{
		/// <summary>强制刷新主题资源字典（等价 WPF MergedDictionaries Add 新 + Remove 旧）。</summary>
		void Refresh();

		/// <summary>获取系统强调色画刷，不可用时返回 fallback。</summary>
		IBrush GetSystemBrush(SystemColorType colorType, IBrush fallback);
	}

	/// <summary>系统强调色类型（从 Theme.cs 提取，供 IThemeService 引用）。</summary>
	public enum SystemColorType
	{
		Accent,
		Accent1,
		Accent2
	}
}
