using System;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // 字体/回退配置见 FontSetup（v4.0.5：内置 CJK 字体子集，修复无字体环境的方框问题）。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(FontSetup.CreateFontManagerOptions())
            .LogToTrace();
}
