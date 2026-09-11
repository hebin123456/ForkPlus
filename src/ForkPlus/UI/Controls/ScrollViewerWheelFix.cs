using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 2026-09-11：全局修复"所有 ScrollViewer 点过滚动条后滚轮滚不动 / thumb 不跟随"。
	/// TouchpadAwareScrollViewer 的修复只覆盖显式用它的地方；普通 ScrollViewer（AI 辅助开发弹窗、
	/// 提交界面等）仍是裸 Avalonia ScrollViewer，有同款 bug。这里用附加属性把同样的修复
	/// （Tunnel 阶段挂滚轮 + ScrollBy 设 Offset 并同步 vbar.Value）应用到任意 ScrollViewer——
	/// 在 ScrollViewer 主题里设一个 Setter 即可全局生效，无需逐处改 XAML。
	/// </summary>
	public class ScrollViewerWheelFix
	{
		public static readonly AttachedProperty<bool> ApplyProperty =
			AvaloniaProperty.RegisterAttached<ScrollViewerWheelFix, ScrollViewer, bool>("Apply", false);

		public static bool GetApply(ScrollViewer sv) => sv.GetValue(ApplyProperty);
		public static void SetApply(ScrollViewer sv, bool value)
		{
			sv.SetValue(ApplyProperty, value);
			if (value) Hook(sv);
		}

		private static void Hook(ScrollViewer sv)
		{
			if (sv == null) return;
			// TouchpadAwareScrollViewer 已有自己的同款修复，跳过避免双重处理。
			if (sv is TouchpadAwareScrollViewer) return;
			sv.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
			sv.TemplateApplied += OnTemplateApplied;
			// 修复（2026-09-11，"RevisionDetails 等视图滚动条 thumb 仍不跟随"）：若模板在
			// Apply setter 之前已应用（ThemeApplied 已触发过），上面的 TemplateApplied 订阅就错过
			// 了，PART_VerticalScrollBar 不会被挂 Tunnel 滚轮处理。这里立即用 GetTemplateChild
			// 尝试挂一次（模板已应用时能拿到 ScrollBar），覆盖"模板先于 setter 应用"的场景。
			HookScrollBars(sv);
		}

		private static void HookScrollBars(ScrollViewer sv)
		{
			if (sv.GetTemplateChild("PART_VerticalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar vbar)
			{
				vbar.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
			}
			if (sv.GetTemplateChild("PART_HorizontalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar hbar)
			{
				hbar.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
			}
		}

		private static void OnTemplateApplied(object sender, TemplateAppliedEventArgs e)
		{
			if (e.NameScope.Find<global::Avalonia.Controls.Primitives.ScrollBar>("PART_VerticalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar vbar)
			{
				vbar.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
			}
			if (e.NameScope.Find<global::Avalonia.Controls.Primitives.ScrollBar>("PART_HorizontalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar hbar)
			{
				hbar.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
			}
		}

		private static void OnWheel(object sender, PointerWheelEventArgs e)
		{
			if (e.Handled) return;
			ScrollViewer sv = sender as ScrollViewer;
			if (sv == null && sender is global::Avalonia.Controls.Primitives.ScrollBar bar)
			{
				// 落在 ScrollBar 上时，找其所在的 ScrollViewer 祖先
				for (global::Avalonia.Visual v = bar; v != null; v = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
				{
					if (v is ScrollViewer s) { sv = s; break; }
				}
			}
			if (sv == null) return;

			if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Math.Abs(e.Delta.Y) > 0)
			{
				ScrollBy(sv, -e.Delta.Y * 16.0, 0.0);
				e.Handled = true;
				return;
			}
			if (Math.Abs(e.Delta.X) > 0)
			{
				ScrollBy(sv, -e.Delta.X * 16.0, 0.0);
				e.Handled = true;
				return;
			}
			if (Math.Abs(e.Delta.Y) > 0)
			{
				double step = Math.Abs(e.Delta.Y) < 1 ? 16.0 : 48.0;
				ScrollBy(sv, 0.0, -e.Delta.Y * step);
				e.Handled = true;
			}
		}

		private static void ScrollBy(ScrollViewer sv, double deltaX, double deltaY)
		{
			double max = Math.Max(0.0, sv.ScrollBarMaximum.Y);
			double x = Math.Clamp(sv.Offset.X + deltaX, 0.0, Math.Max(0.0, sv.ScrollBarMaximum.X));
			double y = Math.Clamp(sv.Offset.Y + deltaY, 0.0, max);
			sv.Offset = new Vector(x, y);
			// 同步 thumb：Avalonia 内部 Offset→ScrollBar.Value 反向同步在拖过 thumb 后失效，
			// 手动把 vbar/hbar.Value 设成新 Offset，强制 thumb 跟随。
			if (sv.GetTemplateChild("PART_VerticalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar vbar2)
			{
				vbar2.Value = y;
				// 2026-09-11：仅设 Value 不够（thumb 视觉可能不刷新），强制 invalidate
				// 让 ScrollBar 模板重算 thumb 位置，确保 thumb 视觉跟随。
				vbar2.InvalidateVisual();
			}
			if (sv.GetTemplateChild("PART_HorizontalScrollBar") is global::Avalonia.Controls.Primitives.ScrollBar hbar2)
			{
				hbar2.Value = x;
				hbar2.InvalidateVisual();
			}
		}
	}
}
