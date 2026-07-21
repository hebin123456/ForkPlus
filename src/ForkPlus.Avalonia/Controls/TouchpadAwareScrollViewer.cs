using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TouchpadAwareScrollViewer（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TouchpadAwareScrollViewer.cs（108 行）：
    //   - WPF TouchpadAwareScrollViewer : ScrollViewer
    //   - WM_MOUSEHWHEEL = 526（Windows 横向滚轮消息）
    //   - Loaded：PresentationSource.FromVisual + HwndSource.AddHook(MouseHorizontalScrollHook)
    //   - Unloaded：RemoveHook + 置空
    //   - OnMouseWheel：
    //     - Shift 修饰 → LineLeft/LineRight（横向滚动）
    //     - |Delta| < 120（触摸板精细滚动）→ LineDown/LineUp
    //     - 否则 → MouseWheelDown/Up
    //   - MouseHorizontalScrollHook：拦截 WM_MOUSEHWHEEL → LineLeft/LineRight
    //     仅当 IsMouseOver + ComputedHorizontalScrollBarVisibility == Collapsed
    //   - HiWord(wParam)：取高 16 位
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ScrollViewer + 触摸板事件简化）：
    //   1. 基类 ScrollViewer → Avalonia.Controls.ScrollViewer（API 一致）
    //   2. WPF WM_MOUSEHWELL + HwndSource Hook → spike 跳过
    //      （Windows-specific P/Invoke，Avalonia 跨平台目标，spike 用 PointerWheelChanged 统一处理）
    //   3. WPF OnMouseWheel (MouseWheelEventArgs) → Avalonia PointerWheelChanged 事件
    //   4. WPF ScrollInfo.LineLeft/Right/Up/Down → Avalonia LineLeft/LineRight/LineUp/LineDown 方法
    //   5. WPF Keyboard.Modifiers == ModifierKeys.Shift → Avalonia KeyModifiers.Shift
    //   6. WPF ComputedHorizontalScrollBarVisibility → Avalonia 无对应属性（spike 跳过判断）
    //   7. spike 简化：触摸板精细滚动 + 横向滚动逻辑保留
    //      横向滚轮用 PointerWheelChanged 的 Delta.X 检测
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ScrollViewer
    //   - PointerWheelChanged 处理 Shift 修饰 + 触摸板精细滚动 + 横向滚动
    public class TouchpadAwareScrollViewer : ScrollViewer
    {
        public TouchpadAwareScrollViewer()
        {
            // 对照 WPF: OnMouseWheel (MouseWheelEventArgs e)
            // spike 版：PointerWheelChanged 替代 OnMouseWheel
            PointerWheelChanged += TouchpadAwareScrollViewer_PointerWheelChanged;
        }

        // 对照 WPF: protected override void OnMouseWheel(MouseWheelEventArgs e)
        //   if (ScrollInfo != null) {
        //     if (Keyboard.Modifiers == Shift) { LineLeft / LineRight }
        //     else if (|Delta| < 120) { LineDown / LineUp }
        //     else { MouseWheelDown / MouseWheelUp } }
        //   e.Handled = true;
        // spike 版：PointerWheelChanged 替代 OnMouseWheel
        private void TouchpadAwareScrollViewer_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            // 对照 WPF: 横向滚轮（WM_MOUSEHWHEEL）→ LineLeft / LineRight
            // spike 版：用 Delta.X 检测横向滚动
            if (e.Delta.X != 0)
            {
                if (e.Delta.X < 0)
                {
                    LineRight();
                }
                else
                {
                    LineLeft();
                }
                e.Handled = true;
                return;
            }

            // 对照 WPF: Keyboard.Modifiers == ModifierKeys.Shift → 横向滚动
            // spike 版：KeyModifiers.Shift 替代
            if (e.KeyModifiers == KeyModifiers.Shift)
            {
                if (e.Delta.Y < 0)
                {
                    LineRight();
                }
                else
                {
                    LineLeft();
                }
                e.Handled = true;
                return;
            }

            // 对照 WPF: |Delta| < 120（触摸板精细滚动）→ LineDown / LineUp
            // spike 版：Avalonia PointerWheelEventArgs.Delta.Y 是 normalized（-1/+1），
            // 不存在 WPF 的 |Delta| < 120 判断；spike 直接走 LineDown/LineUp 路径
            if (e.Delta.Y < 0)
            {
                LineDown();
            }
            else
            {
                LineUp();
            }
            e.Handled = true;
        }
    }
}
