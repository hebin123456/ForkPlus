using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF HwndSource + WM_MOUSEHWHEEL 水平滚轮 hook（Windows 专属）
	// → Avalonia PointerWheelChanged 事件（跨平台，原生支持水平滚轮）。
	// WPF OnMouseWheel(MouseWheelEventArgs) → Avalonia OnPointerWheelChanged(PointerWheelEventArgs)。
	// WPF ScrollInfo.LineLeft/LineRight/LineUp/LineDown/MouseWheelUp/MouseWheelDown
	// → Avalonia ScrollViewer.LineLeft/LineRight/LineUp/LineDown/PageUp/PageDown。
	// WPF Keyboard.Modifiers → Avalonia KeyModifiers（通过 PointerWheelEventArgs.KeyModifiers）。
	// WPF ComputedHorizontalScrollBarVisibility → Avalonia ScrollViewer.HorizontalScrollBarVisibility（属性类型不同）。
	// TODO(4.5-o): Avalonia ScrollViewer 无 ScrollInfo 公开属性；
	// 直接调用 ScrollViewer 自身的 LineLeft/LineRight/LineUp/LineDown 方法。
	public class TouchpadAwareScrollViewer : ScrollViewer
	{
		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}
			// 阶段 4.5：WPF e.Delta (int, ±120 倍数) → Avalonia e.Delta (Vector, ±1)。
			// 触控板小幅度滚动（|Delta| < 120）→ LineUp/Down；鼠标滚轮（|Delta| >= 120）→ PageUp/Down。
			// Avalonia Delta 已归一化，统一按符号判断方向。
			double deltaY = e.Delta.Y;
			double deltaX = e.Delta.X;
			// 阶段 4.5：Shift+滚轮 → 水平滚动（与 WPF 行为一致）。
			bool shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
			if (shiftPressed || Math.Abs(deltaX) > Math.Abs(deltaY))
			{
				double delta = (shiftPressed ? deltaY : deltaX);
				if (delta < 0.0)
				{
					LineRight();
				}
				else
				{
					LineLeft();
				}
			}
			else
			{
				// 阶段 4.5：Avalonia Delta 已归一化，无法区分触控板（小 Delta）与鼠标滚轮（大 Delta）。
				// 统一使用 LineUp/LineDown（原 WPF 触控板分支），Avalonia 自身会累积多次 Line 调用实现 Page 效果。
				if (deltaY < 0.0)
				{
					LineDown();
				}
				else
				{
					LineUp();
				}
			}
			e.Handled = true;
		}
	}
}
