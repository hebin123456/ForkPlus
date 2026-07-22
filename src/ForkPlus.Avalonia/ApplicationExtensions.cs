using System.Diagnostics;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/ApplicationExtensions.cs（28 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - TabManager(this Application) → (application.MainWindow as MainWindow)?.TabManager
    //   - ActiveRepositoryUserControl(this Application) → (MainWindow as MainWindow)?.TabManager.ActiveRepositoryUserControl
    //   - RefreshLayoutScaling(this Application) → ForkPlusSettings.Default.LayoutScaling + Resources["LayoutScaleTransform"] = new ScaleTransform(...)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. Application → Avalonia.Application（同样有 MainWindow 属性）
    //   2. ForkPlus.UI.MainWindow / TabManager / RepositoryUserControl 为 WPF 工程专有类型，
    //      Avalonia 工程尚未迁移（Phase 4 主窗口迁移后启用），spike 版这两个方法暂注释
    //   3. ForkPlusSettings 为 WPF 工程专有，spike 版 RefreshLayoutScaling 暂注释
    //   4. ScaleTransform → Avalonia.Media.ScaleTransform（命名空间相同，类型等价）
    //   5. Application.Current → Avalonia.Application.Current!（spike 规范）
    public static class ApplicationExtensions
    {
        // spike: TabManager 依赖 WPF 工程的 MainWindow / TabManager 类型，Avalonia 工程尚未迁移
        // [DebuggerStepThrough]
        // public static TabManager TabManager(this Application application)
        // {
        //     return (application.MainWindow as MainWindow)?.TabManager;
        // }

        // spike: ActiveRepositoryUserControl 依赖 WPF 工程的 MainWindow / RepositoryUserControl 类型
        // [DebuggerStepThrough]
        // public static RepositoryUserControl ActiveRepositoryUserControl(this Application application)
        // {
        //     return (application.MainWindow as MainWindow)?.TabManager.ActiveRepositoryUserControl;
        // }

        // spike: RefreshLayoutScaling 依赖 ForkPlusSettings.Default.LayoutScaling（WPF 工程专有）
        // public static void RefreshLayoutScaling(this Application application)
        // {
        //     double num = (double)ForkPlusSettings.Default.LayoutScaling * 0.01;
        //     application.Resources["LayoutScaleTransform"] = new ScaleTransform(num, num);
        // }
    }
}
