using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// WPF 版通过 Win32 HwndSource 钩 WM_MOUSEHWHEEL 处理触控板横向滚动，
	/// 并把小幅滚轮增量（&lt;120）当作"触控板平滑滚动"逐行滚动。
	/// Avalonia 11+ 的 PointerWheelEventArgs.Delta 为 Vector(X=横向, Y=纵向)，
	/// 横向滚动已由基类原生处理，无需 Win32 钩子。
	/// Migration note：保留类名以兼容 XAML 引用；如需恢复"小步长=逐行"的触控板手感，
	/// 可在此 override OnPointerWheelChanged 按 |Delta.Y| 阈值分派 Line/SmallStep 滚动。
	/// </summary>
	public class TouchpadAwareScrollViewer : ScrollViewer
	{
		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}

			if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Math.Abs(e.Delta.Y) > 0)
			{
				ScrollBy(-e.Delta.Y * HorizontalStep(), 0.0);
				e.Handled = true;
				return;
			}

			if (Math.Abs(e.Delta.X) > 0)
			{
				ScrollBy(-e.Delta.X * HorizontalStep(), 0.0);
				e.Handled = true;
				return;
			}

			// WPF used small wheel deltas as touchpad-style line scrolling.
			// Normal mouse notches are still handled by Avalonia's native ScrollViewer.
			if (Math.Abs(e.Delta.Y) is > 0 and < 1)
			{
				ScrollBy(0.0, -e.Delta.Y * VerticalStep());
				e.Handled = true;
				return;
			}

			base.OnPointerWheelChanged(e);
		}

		private double HorizontalStep()
		{
			return SmallChange.Width > 0 ? SmallChange.Width : 16.0;
		}

		private double VerticalStep()
		{
			return SmallChange.Height > 0 ? SmallChange.Height : 16.0;
		}

		private void ScrollBy(double deltaX, double deltaY)
		{
			double x = Math.Clamp(Offset.X + deltaX, 0.0, Math.Max(0.0, ScrollBarMaximum.X));
			double y = Math.Clamp(Offset.Y + deltaY, 0.0, Math.Max(0.0, ScrollBarMaximum.Y));
			Offset = new Vector(x, y);
		}
	}
}
