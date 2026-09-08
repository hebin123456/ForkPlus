using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Input;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class SwitchApplicationThemeCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Switch Theme";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		/// <summary>无参切换：在基底 Light 与 Dark 之间 toggle（快捷键场景）。
		/// 当前皮肤基底为 Light 切到 Dark（反之亦然），保留用户在同类皮肤内的选择。</summary>
		public void Execute()
		{
			ThemeType current = ForkPlusSettings.Default.Theme;
			ThemeType target = current.IsDarkBase() ? ThemeType.Light : ThemeType.Dark;
			ForkPlusSettings.Default.Theme = target;
			Execute(target);
		}

		public void Execute(ThemeType newTheme, bool followSystemTheme = false)
		{
			ForkPlusSettings.Default.Theme = newTheme;
			ForkPlusSettings.Default.FollowSystemTheme = followSystemTheme;
			// 切换主题时关闭自定义颜色覆盖，使用新主题的原色（避免自定义覆盖与主题色混乱）。
			// CustomColors 字典保留，用户重新勾选"自定义颜色"时可恢复。
			ForkPlusSettings.Default.UseCustomColors = false;
			// Bug 修复（2026-09-08，"彩色主题下弹窗外圈颜色不一致"）：RefreshWindowBorderBrush 原先
			// 在此处（换主题字典之前）调用，取到的 AccentColor 还是旧主题的——切到紫色后边框仍是
			// 上一主题的颜色。移到新字典加载 + ApplyCustomColors 之后（与启动路径
			// InitializeTheme → RefreshWindowBorderBrush 的时序对齐），确保边框取到新主题色。
			// App.RefreshWindowBorderBrush();
			// Bug 修复（2026-09-07，"切换主题就有可能导致 UI 崩溃，参见外观下拉的按钮和菜单栏
			// 窗口里面的按钮"）：换字典前先释放 popup 托管的孤儿 ItemsPresenter——旧模板撕毁的
			// 视觉树遍历够不到 PopupHost 里的内容，详见 PopupItemsPresenterRelease 类注释。
			PopupItemsPresenterRelease.ReleaseOrphaned();
			// 匹配任意 Generic.{SkinName}.xaml（不再写死 Light|Dark），支持多预设皮肤
			// Migration note：WPF 写法 MergedDictionaries.Where/FirstOrDefault((ResourceDictionary rd) => rd.Source ...)
			// 在 Avalonia 报 CS1929/CS1061（MergedDictionaries 是 IList<IResourceProvider>，
			// 且 ResourceDictionary 没有 Source）。改为 App.FindThemeResourceInclude()：遍历
			// MergedDictionaries 按 ResourceInclude.Source（avares://ForkPlus/Theme/Generic.*.axaml）
			// 识别旧主题字典，保持"按 Uri 识别主题字典"的原语义；新字典用 ResourceInclude 加载。
			ResourceInclude oldThemeInclude = App.FindThemeResourceInclude();
			ResourceInclude item = App.CreateThemeResourceInclude(newTheme.ResourceUri());
			Application.Current.Resources.MergedDictionaries.Add(item);
			if (oldThemeInclude != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(oldThemeInclude);
			}
				// 同步 FluentTheme/AvaloniaEdit/OxyPlot 的明暗变体（2026-09-04 突兀根因，
				// 详见 App.SyncThemeVariant 注释）：切换命令是运行时换肤唯一入口，必须在这里同步
				App.SyncThemeVariant(newTheme);
				global::ForkPlus.UI.Theme.Refresh();
				// 切换皮肤后重新应用用户自定义颜色覆盖（旧字典随主题字典移除后需重建）
				App.ApplyCustomColors();
				// 新主题字典与自定义色覆盖就绪后再刷新窗口边框刷（见方法头部注释）
				App.RefreshWindowBorderBrush();
				NotificationCenter.Current.RaiseApplicationThemeChanged(this, newTheme);
		}
	}
}
