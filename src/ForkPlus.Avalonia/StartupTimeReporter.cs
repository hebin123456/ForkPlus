using System;
// Avalonia spike 版 StartupTimeReporter（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/StartupTimeReporter.cs（41 行）：
//   - WPF: public class StartupTimeReporter
//   - 静态字段：_processStartTime / _appStartTime / _mainWindowCreatedTime / _mainWindowLoadedTime
//   - AppStarted(processStartTime)：记录进程启动 + 应用启动时间
//   - MainWindowCreated()：记录主窗口创建时间
//   - MainWindowLoaded()：记录主窗口加载时间
//   - UIReady()：计算各阶段耗时并通过 Log.Info 输出
//   - 依赖：Log.Info（ForkPlus.Core.Log，跨平台可用）
//
// Avalonia 版差异：
//   1. WPF 无平台依赖 → Avalonia 直接复用（仅命名空间改为 ForkPlus.Avalonia）
//   2. Log.Info 来自 ForkPlus.Core（Avalonia 工程已引用），无需改动
//
// spike 简化（task spec 关键 API）：
//   - 静态方法 AppStarted / MainWindowCreated / MainWindowLoaded / UIReady
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    public class StartupTimeReporter
    {
        private static DateTime _processStartTime;
        private static DateTime _appStartTime;
        private static DateTime _mainWindowCreatedTime;
        private static DateTime _mainWindowLoadedTime;

        public static void AppStarted(DateTime processStartTime)
        {
            _processStartTime = processStartTime;
            _appStartTime = DateTime.Now;
        }

        public static void MainWindowCreated()
        {
            _mainWindowCreatedTime = DateTime.Now;
        }

        public static void MainWindowLoaded()
        {
            _mainWindowLoadedTime = DateTime.Now;
        }

        public static void UIReady()
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSpan = _appStartTime - _processStartTime;
            TimeSpan timeSpan2 = _mainWindowCreatedTime - _appStartTime;
            TimeSpan timeSpan3 = _mainWindowLoadedTime - _mainWindowCreatedTime;
            TimeSpan timeSpan4 = now - _mainWindowLoadedTime;
            TimeSpan timeSpan5 = now - _processStartTime;
            Log.Info($"App start: {timeSpan.TotalMilliseconds:F0}ms, AppInitialization: {timeSpan2.TotalMilliseconds:F0}ms, MainWindowOpening: {timeSpan3.TotalMilliseconds:F0}ms, MainWindowActivation: {timeSpan4.TotalMilliseconds:F0}ms, Total: {timeSpan5.TotalMilliseconds:F0}ms");
        }
    }
}
