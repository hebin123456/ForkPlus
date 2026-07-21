using System;
using Avalonia;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 RectExtensions（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/RectExtensions.cs（46 行）：
    //   - WPF RectExtensions : internal static class
    //   - Inset(this Rect, dx, dy)：内缩矩形（X/Y 偏移，Width/Height 减去 2*dx/2*dy，最小 0）
    //   - DivideFromTop(this Rect, distance)：从顶部按距离分割为 (topRect, bottomRect)
    //   - DivideFromLeft(this Rect, separatorX)：从左侧按 X 坐标分割为 (leftRect, rightRect)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Rect → Avalonia.Rect
    //      （API 形状一致：X/Y/Width/Height 属性 + 构造函数 + Bottom/Right 计算属性）
    //   2. WPF Rect.Bottom / Rect.Right 属性 → Avalonia Rect.Bottom / Rect.Right 属性
    //      （Avalonia Rect 也提供这些计算属性）
    //   3. WPF Tuple<Rect, Rect> → spike 保持 Tuple<Rect, Rect>
    //      （C# 标准库 Tuple，无 WPF 依赖）
    //   4. WPF internal 类 → spike 改为 public
    //      （Avalonia 工程内部使用，spike 简化为 public 便于跨工程访问）
    //   5. 算法完整移植，无 WPF 依赖
    //
    // spike 简化（task spec 关键 API）：
    //   - public static class
    //   - Inset / DivideFromTop / DivideFromLeft 扩展方法完整移植
    public static class RectExtensions
    {
        // 对照 WPF: public static Rect Inset(this Rect rect, double dx, double dy)
        //   return new Rect(rect.X + dx, rect.Y + dy,
        //     Math.Max(rect.Width - 2.0 * dx, 0.0), Math.Max(rect.Height - 2.0 * dy, 0.0));
        public static Rect Inset(this Rect rect, double dx, double dy)
        {
            return new Rect(
                rect.X + dx,
                rect.Y + dy,
                Math.Max(rect.Width - 2.0 * dx, 0.0),
                Math.Max(rect.Height - 2.0 * dy, 0.0));
        }

        // 对照 WPF: public static Tuple<Rect, Rect> DivideFromTop(this Rect rect, double distance)
        // 从顶部按 distance 分割为 (topRect, bottomRect)
        public static Tuple<Rect, Rect> DivideFromTop(this Rect rect, double distance)
        {
            double num = distance;
            if (num > rect.Height)
            {
                num = rect.Height;
            }
            Rect topRect = new Rect(rect.X, rect.Y, rect.Width, num);
            double bottom = topRect.Bottom;
            double num2 = rect.Height - distance;
            if (num2 < 0.0)
            {
                num2 = 0.0;
            }
            Rect bottomRect = new Rect(rect.X, bottom, rect.Width, num2);
            return new Tuple<Rect, Rect>(topRect, bottomRect);
        }

        // 对照 WPF: public static Tuple<Rect, Rect> DivideFromLeft(this Rect rect, double separatorX)
        // 从左侧按 separatorX 分割为 (leftRect, rightRect)
        public static Tuple<Rect, Rect> DivideFromLeft(this Rect rect, double separatorX)
        {
            double num = separatorX;
            if (num > rect.Width)
            {
                num = rect.Width;
            }
            Rect leftRect = new Rect(rect.X, rect.Y, num, rect.Height);
            double right = leftRect.Right;
            double num2 = rect.Width - separatorX;
            if (num2 < 0.0)
            {
                num2 = 0.0;
            }
            Rect rightRect = new Rect(right, rect.Y, num2, rect.Height);
            return new Tuple<Rect, Rect>(leftRect, rightRect);
        }
    }
}
