using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/WindowExtensions.cs（23 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - ShowAtCenter(this Window, Window parent, double ratio=0.9)
    //     → parent.GetWindowLocationStateX() 获取 Left/Top/Width/Height
    //     → 计算居中位置 + ratio 缩放尺寸
    //     → 设置 window.Left/Top/Width/Height + window.Show()
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. Window → Avalonia.Controls.Window（spike 规范）
    //   2. WindowLocationStateExtensions.GetWindowLocationStateX()（WPF 工程专有）
    //      → Avalonia 用 parent.Bounds.Width/Height + parent.Position 替代
    //   3. window.Left/Top → Avalonia Window.Position（PixelPoint，物理像素）
    //      spike 版用 Position = new PixelPoint(...) 设置窗口位置（与 WPF Left/Top 语义不同但功能等价）
    //   4. window.Width/Height → Avalonia Window.Width/Height（double，DIP，与 WPF 一致）
    //   5. spike 版 Position 单位为物理像素，Bounds 单位为 DIP，存在 DPI 缩放差异（Phase 4 统一处理）
    public static class WindowExtensions
    {
        public static void ShowAtCenter(this Window window, Window parent, double ratio = 0.9)
        {
            // spike: WPF WindowLocationStateX → Avalonia parent.Bounds + parent.Position
            double parentWidth = parent.Bounds.Width;
            double parentHeight = parent.Bounds.Height;
            double centerX = parent.Position.X + parentWidth / 2.0;
            double centerY = parent.Position.Y + parentHeight / 2.0;
            double width = parentWidth * ratio;
            double height = parentHeight * ratio;
            double left = centerX - width / 2.0;
            double top = centerY - height / 2.0;
            window.Width = width;
            window.Height = height;
            window.Position = new PixelPoint((int)left, (int)top);
            window.Show();
        }
    }
}
