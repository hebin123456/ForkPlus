using System;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus;

internal static class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			// v4.0.6：native 崩溃转储（SIGSEGV/SIGABRT 等运行时级硬崩的现场保留）。
			// 必须早于一切可能崩溃的初始化——设置 DOTNET_DbgEnableMiniDump 系列环境变量，
			// 运行时在 native 崩溃发生时自动拉起 createdump 写 dump-<pid>.dmp 到日志目录。
			NativeDumps.Initialize();
			BuildAvaloniaApp()
				.StartWithClassicDesktopLifetime(args);
		}

    // 字体/回退配置见 FontSetup（v4.0.5：内置 CJK 字体子集，修复无字体环境的方框问题）。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(FontSetup.CreateFontManagerOptions())
            .LogToTrace();
}
