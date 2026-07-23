using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace ForkPlus
{
	/// <summary>
	/// Avalonia 应用入口（阶段 4 里程碑 4.1 新建）。
	/// WPF 由 SDK 自动生成 Main，Avalonia 必须显式提供。
	/// </summary>
	internal sealed class Program
	{
		// STAThread：COM/输入/剪贴板依赖。
		[STAThread]
		public static void Main(string[] args)
			=> BuildAvaloniaApp()
				.StartWithClassicDesktopLifetime(args);

		/// <summary>
		/// AppBuilder 配置。IDE 预览器基础设施需要此方法（不要删除）。
		/// </summary>
		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()   // 自动选择 Win32 / X11 / Native 后端 + Skia 渲染
				.WithInterFont()       // 内嵌 Inter 字体，跨平台一致性
				.LogToTrace();         // 日志输出到 System.Diagnostics.Trace
	}
}
