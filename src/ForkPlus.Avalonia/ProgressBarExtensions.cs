using Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/ProgressBarExtensions.cs（12 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - ShowWithProgress(this ProgressBar, double progress) → progressBar.Show() + progressBar.Value = progress
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. ProgressBar → Avalonia.Controls.ProgressBar（spike 规范）
    //   2. progressBar.Show() → UIElementExtensions.Show(this Visual) 扩展方法
    //      （ProgressBar 继承 Control → Visual，Show() 设置 IsVisible=true，同 namespace 可直接调用）
    //   3. progressBar.Value → Avalonia ProgressBar.Value（Avalonia 11 同名属性，double 类型）
    public static class ProgressBarExtensions
    {
        public static void ShowWithProgress(this ProgressBar progressBar, double progress)
        {
            progressBar.Show();
            progressBar.Value = progress;
        }
    }
}
