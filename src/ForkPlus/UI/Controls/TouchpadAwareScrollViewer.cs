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
		// 修复（2026-09-10，"滚轮滚动有用但 thumb 不跟随"）：Avalonia 内部 ScrollBar.Value ↔ Offset
		// 双向绑定在拖过 thumb 后偶发不反向同步（Offset 变了 Value 不跟），thumb 卡住。
		// 这里在 ScrollBy 设 Offset 后直接把 vbar.Value 也设成同一值，强制 thumb 跟随。
		[Null]
		private global::Avalonia.Controls.Primitives.ScrollBar _vbar;
		[Null]
		private global::Avalonia.Controls.Primitives.ScrollBar _hbar;
		public TouchpadAwareScrollViewer()
		{
			// 修复（2026-09-10，"点过滚动条后滚轮滚不动"）：点过 ScrollBar thumb 后 ScrollBar 捕获焦点，
			// 滚轮事件在 bubble 阶段被 ScrollBar 标记 Handled 并停止冒泡，ScrollViewer 的
			// OnPointerWheelChanged（bubble）永远不触发。改为在 Tunnel（预览）阶段挂处理——
			// 事件从根向目标下行，ScrollViewer（父）先于 ScrollBar（子）收到，这里先于 ScrollBar
			// 处理并标记 Handled，ScrollBar 不再拦截，滚轮总能滚动。
			AddHandler(global::Avalonia.Input.InputElement.PointerWheelChangedEvent, OnPointerWheelCore, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_vbar = this.GetTemplateChild("PART_VerticalScrollBar") as global::Avalonia.Controls.Primitives.ScrollBar;
			_hbar = this.GetTemplateChild("PART_HorizontalScrollBar") as global::Avalonia.Controls.Primitives.ScrollBar;
			// 修复（2026-09-10，加强力）：Tunnel 阶段挂 ScrollViewer 自身仍不够——点过 ScrollBar
			// 后焦点在 ScrollBar 上，滚轮事件目标=ScrollBar，Tunnel 路径可能不经过 ScrollViewer
			// （ScrollBar 作为模板部件其路由路径取决于实现）。直接在 PART_VerticalScrollBar 上
			// 挂 Tunnel 滚轮处理，转发给本 ScrollViewer 的 ScrollBy，确保滚轮在 ScrollBar
			// 上方/有焦点时也能滚动内容。
			if (_vbar != null)
			{
				_vbar.AddHandler(global::Avalonia.Input.InputElement.PointerWheelChangedEvent, OnPointerWheelCore, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			}
			if (_hbar != null)
			{
				_hbar.AddHandler(global::Avalonia.Input.InputElement.PointerWheelChangedEvent, OnPointerWheelCore, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			}
		}

		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			// bubble 阶段：ScrollBar 已标记 Handled 时这里不触发，由 Tunnel 阶段的 OnPointerWheelCore 兜底。
			OnPointerWheelCore(this, e);
		}

		private void OnPointerWheelCore(object sender, PointerWheelEventArgs e)
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

			// 垂直滚轮始终自行处理（触控板小步长逐行，鼠标每档 48px），不依赖 e.Handled。
			if (Math.Abs(e.Delta.Y) > 0)
			{
				double step = Math.Abs(e.Delta.Y) < 1 ? VerticalStep() : 48.0;
				ScrollBy(0.0, -e.Delta.Y * step);
				e.Handled = true;
				return;
			}
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
		// 修复（2026-09-10，"滚轮滚动有用但 thumb 不跟随"）：Avalonia 内部 Offset→ScrollBar.Value
		// 反向同步在拖过 thumb 后失效，thumb 卡住。这里手动把 vbar/hbar.Value 设成新 Offset，
		// 强制 thumb 跟随移动（Value 与 Offset 同单位，设同值不触发循环）。
		if (_vbar != null)
		{
			_vbar.Value = y;
		}
		if (_hbar != null)
		{
			_hbar.Value = x;
		}
	}
	}
}
