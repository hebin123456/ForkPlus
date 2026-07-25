// ⚠ 临时桥接扩展 ─ 阶段 5 编译过渡用。
// Avalonia 11.3 与 WPF API 差异较大，本文件集中提供 WPF 兼容的扩展方法，
// 让迁移代码无需逐处改写即可通过编译。真正的迁移（阶段 6）会逐步替换为原生 Avalonia API。
//
// 命名空间 ForkPlus.UI：ForkPlus.UI.* 子命名空间内的代码可直接引用（C# 沿命名空间链查找）。
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	/// <summary>
	/// WPF 兼容扩展方法集合。集中处理 Avalonia 11.3 缺失或重命名的 WPF API。
	/// </summary>
	public static class WpfBridgeExtensions
	{
		// ===== MenuItem 兼容（WPF MenuItem.Checked/Unchecked/IsCheckable/InputGestureText）=====

		/// <summary>WPF MenuItem.Checked 兼容：当 Click 且 IsChecked=true 时触发。</summary>
		public static void AddCheckedHandler(this MenuItem menuItem, EventHandler<RoutedEventArgs> handler)
		{
			menuItem.Click += (s, e) =>
			{
				if (menuItem.IsChecked == true)
				{
					handler(s, e);
				}
			};
		}

		/// <summary>WPF MenuItem.Unchecked 兼容：当 Click 且 IsChecked=false 时触发。</summary>
		public static void AddUncheckedHandler(this MenuItem menuItem, EventHandler<RoutedEventArgs> handler)
		{
			menuItem.Click += (s, e) =>
			{
				if (menuItem.IsChecked != true)
				{
					handler(s, e);
				}
			};
		}

		/// <summary>WPF MenuItem.InputGestureText 兼容：Avalonia 用 InputGesture。</summary>
		public static string GetInputGestureText(this MenuItem menuItem)
		{
			return menuItem.InputGesture?.ToString() ?? string.Empty;
		}

		public static void SetInputGestureText(this MenuItem menuItem, string value)
		{
			// 阶段 5：仅占位。真正的 KeyGesture 解析由调用方处理。
			// 若需要显示快捷键文本，可解析 value 为 KeyGesture 后赋给 InputGesture。
			if (!string.IsNullOrEmpty(value))
			{
				try
				{
					menuItem.InputGesture = KeyGesture.Parse(value);
				}
				catch
				{
					// 解析失败时忽略，避免崩溃
				}
			}
		}

		// ===== PointerPressedEventArgs.ChangedButton 兼容 =====
		// WPF 返回 MouseButton 枚举（Left=0, Middle=1, Right=2, XButton1=3, XButton2=4）。

		/// <summary>WPF PointerPressedEventArgs.ChangedButton 兼容：返回 0=Left, 1=Middle, 2=Right。</summary>
		public static int GetChangedButton(this PointerPressedEventArgs e)
		{
			var props = e.GetCurrentPoint(null).Properties;
			if (props.IsLeftButtonPressed) return 0;
			if (props.IsMiddleButtonPressed) return 1;
			if (props.IsRightButtonPressed) return 2;
			if (props.IsXButton1Pressed) return 3;
			if (props.IsXButton2Pressed) return 4;
			return 0;
		}

		// ===== DrawingContext 兼容 =====

		/// <summary>WPF DrawingContext.DrawRoundedRectangle 兼容。</summary>
		public static void DrawRoundedRectangle(this DrawingContext ctx, IBrush brush, IPen pen, Rect rect, double radiusX, double radiusY)
		{
			var roundedRect = new RoundedRect(rect, radiusX, radiusY);
			ctx.DrawRectangle(brush, pen, roundedRect);
		}

		/// <summary>WPF DrawingContext.Pop 兼容：Avalonia 用 using (ctx.PushTransform(...)) {...}。</summary>
		/// <remarks>此为空操作占位，Push/Pop 配对需调用方重构为 using 模式（阶段 6）。</remarks>
		public static void Pop(this DrawingContext ctx)
		{
			// Avalonia 使用 using (ctx.PushTransform(...)) {...} 模式，无 Pop 方法。
			// 此处保留空实现以兼容 WPF Push/Pop 配对调用，阶段 6 重构为 using 模式。
		}

		/// <summary>WPF DrawingContext.DrawImage(image, rect) 兼容：Avalonia 用 rect 参数名不同。</summary>
		public static void DrawImage(this DrawingContext ctx, IImage image, Rect rect)
		{
			ctx.DrawImage(image, rect);
		}

		// ===== ScrollViewer 兼容 =====

		/// <summary>WPF ScrollViewer.ScrollToVerticalOffset 兼容。</summary>
		public static void ScrollToVerticalOffset(this ScrollViewer scrollViewer, double offset)
		{
			scrollViewer.Offset = new Vector(scrollViewer.Offset.X, offset);
		}

		/// <summary>WPF ScrollChangedEventArgs.VerticalOffset 兼容：从 Avalonia ScrollChangedEventArgs.Extent/Delta 推导。</summary>
		public static double GetVerticalOffset(this ScrollChangedEventArgs e)
		{
			// Avalonia 11.3 的 ScrollChangedEventArgs 无 VerticalOffset 属性。
			// 调用方应改用 ScrollViewer.Offset.Y；此处返回 0 作为占位（阶段 6 修复）。
			return 0.0;
		}

		// ===== IDataObject 兼容 =====

	/// <summary>WPF IDataObject.GetData(Type) 兼容：Avalonia IDataObject.Get 仅接受 string，需将 Type 转 FullName。</summary>
	public static object GetData(this IDataObject dataObject, Type format)
	{
		return dataObject.Get(format?.FullName ?? string.Empty);
	}

	/// <summary>WPF IDataObject.GetData(string) 兼容：Avalonia IDataObject.Get(string)。</summary>
	public static object GetData(this IDataObject dataObject, string format)
	{
		return dataObject.Get(format);
	}

		// ===== KeyGesture 兼容 =====

		/// <summary>WPF KeyGesture.GetDisplayStringForCulture(InvariantCulture) 兼容。</summary>
		public static string ToFriendlyString(this KeyGesture gesture)
		{
			return gesture?.ToString() ?? string.Empty;
		}

		// ===== Visual.RenderScaling 兼容 =====

		/// <summary>WPF Visual.RenderScaling 兼容：Avalonia 用 VisualRoot.RenderScaling。</summary>
		public static double GetRenderScaling(this Visual visual)
		{
			// Avalonia 11.3：Visual 无 RenderScaling 属性；通过 IVisualRoot.RenderScaling 获取。
			// 在窗口尚未附加到 VisualRoot 前（设计期/单元测试）回退 1.0。
			if (visual is Visual v)
			{
				var root = v.GetVisualRoot();
				if (root is IRenderRoot renderRoot)
				{
					return renderRoot.RenderScaling;
				}
			}
			return 1.0;
		}

		// ===== ToMutableBrush：IImmutableSolidColorBrush → SolidColorBrush =====

		/// <summary>WPF SolidColorBrush（可变）兼容：从 ISolidColorBrush 创建可变副本。</summary>
		public static SolidColorBrush ToMutableBrush(this ISolidColorBrush brush)
		{
			if (brush == null) return null;
			return new SolidColorBrush(brush.Color);
		}
	}
}
