using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
// Avalonia spike 版 SlidingPanelHelper（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/SlidingPanelHelper.cs（52 行）：
//   - WPF: public static class SlidingPanelHelper
//   - ShowPanel(Grid, TranslateTransform, height)：DoubleAnimation 动画 Y→0 + Height→height
//   - HidePanel(Grid, TranslateTransform, height)：DoubleAnimation 动画 Y→-height + Height→0
//   - 用 QuadraticEase EaseOut 缓动
//   - 依赖：System.Windows.Media.Animation.DoubleAnimation / TranslateTransform /
//     FrameworkElement.HeightProperty / TranslateTransform.YProperty
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF DoubleAnimation + BeginAnimation → Avalonia 无等价 API
//      spike 版：直接设置 RenderTransform + Height（无动画过渡，spike 简化）
//   2. WPF TranslateTransform.YProperty → Avalonia TranslateTransform（用 MatrixTransform 替代）
//      spike 版：用 TranslateTransform 谯整 Y 偏移
//   3. WPF FrameworkElement.HeightProperty → Avalonia Grid.Height 直接赋值
//
// spike 简化（task spec 关键 API）：
//   - ShowPanel / HidePanel（spike 直接设置无动画过渡）
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    public static class SlidingPanelHelper
    {
        // 对照 WPF: private static TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.3);
        // spike 版：常量保留（spike 不使用动画）
        private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.3);

        // 对照 WPF: public static bool ShowPanel(Grid placeholder, TranslateTransform transform, double height)
        //   if (transform.Y == 0.0 && placeholder.Height == height) return false;
        //   transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(transform.Y, 0.0, ...));
        //   placeholder.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(0.0, height, ...));
        //   return true;
        // spike 版：直接设置 Y=0 + Height=height（无动画过渡）
        public static bool ShowPanel(Grid placeholder, TranslateTransform transform, double height)
        {
            if (transform.Y == 0.0 && placeholder.Height == height)
            {
                return false;
            }
            transform.Y = 0;
            placeholder.Height = height;
            return true;
        }

        // 对照 WPF: public static void HidePanel(Grid placeholder, TranslateTransform transform, double height)
        //   if (transform.Y != -height || placeholder.Height != 0.0) {
        //     transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, -height, ...));
        //     placeholder.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(height, 0.0, ...));
        //   }
        // spike 版：直接设置 Y=-height + Height=0（无动画过渡）
        public static void HidePanel(Grid placeholder, TranslateTransform transform, double height)
        {
            if (transform.Y != (0.0 - height) || placeholder.Height != 0.0)
            {
                transform.Y = -height;
                placeholder.Height = 0;
            }
        }
    }
}
