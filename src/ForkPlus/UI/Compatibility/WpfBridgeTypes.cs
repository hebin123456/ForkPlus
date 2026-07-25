// ⚠ 临时桥接类型 ─ 阶段 5 编译过渡用。
// 集中提供 WPF System.Windows.* 命名空间下被代码引用但 Avalonia 无对应的类型。
// 真正的迁移（阶段 6）会逐步替换为原生 Avalonia API，届时删除本文件。
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	/// <summary>
	/// WPF System.Windows.Input.Keyboard 静态类的 Avalonia 兼容占位。
	/// WPF Keyboard.IsKeyDown(Key) / Keyboard.FocusedElement → Avalonia 无全局键盘状态查询，
	/// 需通过事件参数 KeyModifiers 或 FocusManager 获取。本类仅提供编译占位，运行时行为有限。
	/// </summary>
	public static class Keyboard
	{
		/// <summary>WPF Keyboard.IsKeyDown 兼容：Avalonia 无全局键盘状态，始终返回 false。</summary>
		/// <remarks>调用方应改用事件参数 e.KeyModifiers.HasFlag(KeyModifiers.Control)。</remarks>
		public static bool IsKeyDown(Key key) => false;

		/// <summary>WPF Keyboard.Modifiers 兼容：返回 None。</summary>
		public static Avalonia.Input.KeyModifiers Modifiers => Avalonia.Input.KeyModifiers.None;

		/// <summary>WPF Keyboard.FocusedElement 兼容：返回 null。</summary>
		/// <remarks>调用方应改用 FocusManager.GetFocusedElement()。</remarks>
		public static IInputElement FocusedElement => null;
	}

	/// <summary>
	/// WPF System.Windows.Controls.Primitives.PopupAnimation 枚举的 Avalonia 兼容占位。
	/// WPF Popup 用 PopupAnimation 控制弹出动画；Avalonia Popup 无动画属性，本枚举仅用于 XAML 资源引用兼容。
	/// </summary>
	public enum PopupAnimation
	{
		/// <summary>无动画（默认）。</summary>
		None = 0,
		/// <summary>淡入。</summary>
		Fade = 1,
		/// <summary>滑动。</summary>
		Slide = 2,
		/// <summary>滚动。</summary>
		Scroll = 3
	}

	/// <summary>
	/// WPF System.Windows.SystemParameters 静态类的 Avalonia 兼容占位。
	/// WPF SystemParameters 提供系统级度量（滚动条宽度、窗口边框等），Avalonia 无等价物。
	/// 本类返回常量值（与 WPF 默认值接近），仅供编译占位。
	/// </summary>
	public static class SystemParameters
	{
		/// <summary>WPF SystemParameters.VerticalScrollBarButtonHeight。返回默认 60。</summary>
		public const double VerticalScrollBarButtonHeight = 60.0;

		/// <summary>WPF SystemParameters.MenuPopupAnimationKey。返回 None。</summary>
		public static PopupAnimation MenuPopupAnimationKey => PopupAnimation.None;

		/// <summary>WPF SystemParameters.ComboBoxPopupAnimationKey。返回 None。</summary>
		public static PopupAnimation ComboBoxPopupAnimationKey => PopupAnimation.None;

		/// <summary>WPF SystemParameters.VerticalScrollBarButtonHeightKey。返回 60。</summary>
		public static double VerticalScrollBarButtonHeightKey => 60.0;

		/// <summary>WPF SystemParameters.WindowGlassBrush。返回透明画刷。</summary>
		public static IBrush WindowGlassBrush => Brushes.Transparent;

		/// <summary>WPF SystemParameters.MinimumHorizontalDragDistance / MinimumVerticalDragDistance。</summary>
		public const double MinimumHorizontalDragDistance = 10.0;
		public const double MinimumVerticalDragDistance = 10.0;

		/// <summary>WPF SystemParameters.VerticalScrollBarWidth：垂直滚动条宽度。Avalonia 跨平台无统一值，占位 18.0（Windows 默认）。</summary>
		public const double VerticalScrollBarWidth = 18.0;

		/// <summary>WPF SystemParameters.HorizontalScrollBarHeight：水平滚动条高度。占位 18.0。</summary>
		public const double HorizontalScrollBarHeight = 18.0;

		/// <summary>WPF SystemParameters.ScrollWidth：等价 VerticalScrollBarWidth。占位 18.0。</summary>
		public const double ScrollWidth = 18.0;

		/// <summary>WPF SystemParameters.ScrollHeight：等价 HorizontalScrollBarHeight。占位 18.0。</summary>
		public const double ScrollHeight = 18.0;
	}

	/// <summary>
	/// WPF System.Media.SystemSounds 静态类的 Avalonia 兼容占位。
	/// WPF SystemSounds.Beep.Play() 播放系统提示音；Avalonia 无跨平台等价物。
	/// 本类提供空操作占位，调用方无崩溃风险。
	/// </summary>
	public static class SystemSounds
	{
		/// <summary>WPF SystemSounds.Beep 占位。</summary>
		public static SystemSound Beep => SystemSound.Default;

		/// <summary>WPF SystemSounds.Asterisk 占位。</summary>
		public static SystemSound Asterisk => SystemSound.Default;

		/// <summary>WPF SystemSounds.Exclamation 占位。</summary>
		public static SystemSound Exclamation => SystemSound.Default;

		/// <summary>WPF SystemSounds.Hand 占位。</summary>
		public static SystemSound Hand => SystemSound.Default;

		/// <summary>WPF SystemSounds.Question 占位。</summary>
		public static SystemSound Question => SystemSound.Default;
	}

	/// <summary>
	/// WPF System.Media.Sound 概念的 Avalonia 兼容占位。Play() 为空操作。
	/// </summary>
	public class SystemSound
	{
		internal static SystemSound Default { get; } = new SystemSound();

		/// <summary>WPF SystemSound.Play() 兼容：Avalonia 无跨平台系统音 API，此处空操作。</summary>
		public void Play() { }
	}

	/// <summary>
	/// WPF System.Windows.Controls.Control.TextGuidelineHelper 兼容占位。
	/// 原 WPF 实现计算文本页指导线位置（基于字符宽度和字号）。
	/// Avalonia 无等价物，此处返回 0（无指导线偏移）。
	/// </summary>
	public static class TextGuidelineHelper
	{
		/// <summary>WPF TextGuidelineHelper.GuideLinePosition 兼容：返回 0。</summary>
		public static double GuideLinePosition(Control textBox, int position)
		{
			// 阶段 5：Avalonia TextBox 无 GetRectFromCharacterIndex 等度量 API，
			// 无法精确计算指导线位置。返回 0 作为占位，阶段 6 实现真正计算。
			return 0.0;
		}
	}

	/// <summary>
	/// 将 Action&lt;T&gt; 适配为 IObserver&lt;T&gt;，用于 Subscribe(IObservable&lt;T&gt;)。
	/// Avalonia 的 GetObservable/GetPropertyChangedObservable 返回 IObservable&lt;T&gt;，
	/// 而 WPF 代码常以 EventHandler 订阅属性变更。本类提供 OnNext 桥接，OnCompleted/OnError 为空。
	/// </summary>
	public sealed class ActionObserver<T> : IObserver<T>
	{
		private readonly Action<T> _onNext;
		public ActionObserver(Action<T> onNext) { _onNext = onNext; }
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void OnNext(T value) => _onNext?.Invoke(value);
	}
}

// ====================================================================
// 阶段 5：WPF XAML 桥接类型（AVLN2000 修复）
// 以下类型仅用于让 XAML 中引用的 WPF 控件/静态类通过编译。
// 真正的迁移（阶段 6）会逐步替换为原生 Avalonia API 或删除引用。
// 命名空间 Avalonia.Controls / Avalonia.Controls.Primitives / Avalonia.Media /
// Avalonia.SystemParams 已在 AssemblyXmlnsDefinitions.cs 中映射到默认 XAML 命名空间，
// 故 XAML 无需额外 xmlns 声明即可解析这些类型。
// ====================================================================

namespace Avalonia.Controls
{
	/// <summary>WPF TabPanel bridge. Avalonia uses TabControl's default panel.</summary>
	public class TabPanel : Panel
	{
		// WPF TabPanel was a specialized layout panel; Avalonia uses default Panel.
		// Empty subclass keeps XAML &lt;TabPanel&gt; tags compiling.
	}

	/// <summary>WPF ResizeGrip bridge (bottom-right window resize handle).
	/// Avalonia handles resize natively; this is an empty Control placeholder.</summary>
	public class ResizeGrip : Control
	{
	}

	/// <summary>WPF RichTextBox bridge. Avalonia has no built-in RichTextBox.
	/// Empty TextBox subclass keeps XAML compiling; rich text features lost.</summary>
	public class RichTextBox : TextBox
	{
	}

	/// <summary>WPF AdornerDecorator bridge. Avalonia has no adorners; empty Control placeholder.</summary>
	public class AdornerDecorator : Decorator
	{
	}

	/// <summary>WPF WindowChrome bridge. Avalonia uses different chrome APIs.
	/// Empty class with no-op properties keeps XAML compiling.</summary>
	public class WindowChrome
	{
		public static readonly AttachedProperty<bool> ResizeBorderThicknessProperty =
			AvaloniaProperty.RegisterAttached<WindowChrome, Window, bool>("ResizeBorderThickness");
		public static readonly AttachedProperty<bool> IsHitTestVisibleInChromeProperty =
			AvaloniaProperty.RegisterAttached<WindowChrome, Control, bool>("IsHitTestVisibleInChrome");
		public static readonly AttachedProperty<ResizeGripDirection> ResizeGripDirectionProperty =
			AvaloniaProperty.RegisterAttached<WindowChrome, Control, ResizeGripDirection>("ResizeGripDirection");

		public static bool GetIsHitTestVisibleInChrome(Control element) => element.GetValue(IsHitTestVisibleInChromeProperty);
		public static void SetIsHitTestVisibleInChrome(Control element, bool value) => element.SetValue(IsHitTestVisibleInChromeProperty, value);
		public static ResizeGripDirection GetResizeGripDirection(Control element) => element.GetValue(ResizeGripDirectionProperty);
		public static void SetResizeGripDirection(Control element, ResizeGripDirection value) => element.SetValue(ResizeGripDirectionProperty, value);
		// Stub - no implementation
	}

	/// <summary>WPF ResizeGripDirection enum. Avalonia handles resize natively; stub for XAML.</summary>
	public enum ResizeGripDirection
	{
		None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, Caption
	}

	/// <summary>WPF ListView bridge. Avalonia uses ListBox; this subclass keeps XAML &lt;ListView&gt; tags compiling.</summary>
	public class ListView : ListBox
	{
		/// <summary>WPF ListView.View property. 设置时自动从 GridView 构建 ItemTemplate 以渲染多列内容。</summary>
		public static readonly StyledProperty<GridView> ViewProperty =
			AvaloniaProperty.Register<ListView, GridView>(nameof(View));

		static ListView()
		{
			// 阶段 6：View 属性变化时自动从 GridView 构建 ItemTemplate，让多列内容能渲染。
			ViewProperty.Changed.AddClassHandler<ListView>((list, e) =>
			{
				if (e.NewValue is GridView gridView)
				{
					IDataTemplate template = GridViewRenderer.BuildItemTemplate(gridView);
					if (template != null)
					{
						list.ItemTemplate = template;
					}
				}
			});
		}

		public GridView View
		{
			get => GetValue(ViewProperty);
			set => SetValue(ViewProperty, value);
		}

		// 阶段 5：WPF ListView.VerticalContentAlignment 桥接。Avalonia ListBox 仅暴露 HorizontalContentAlignment，
		// 此处补充 VerticalContentAlignment 以兼容 WPF XAML 模板（实际仅占位，不影响渲染）。
		public static readonly StyledProperty<Avalonia.Layout.VerticalAlignment> VerticalContentAlignmentProperty =
			AvaloniaProperty.Register<ListView, Avalonia.Layout.VerticalAlignment>(nameof(VerticalContentAlignment), Avalonia.Layout.VerticalAlignment.Stretch);
		public Avalonia.Layout.VerticalAlignment VerticalContentAlignment
		{
			get => GetValue(VerticalContentAlignmentProperty);
			set => SetValue(VerticalContentAlignmentProperty, value);
		}
	}

	/// <summary>WPF ListViewItem bridge. Avalonia uses ListBoxItem; this subclass keeps XAML &lt;ListViewItem&gt; tags and Selector="ListViewItem" compiling.</summary>
	public class ListViewItem : ListBoxItem
	{
	}
}

namespace Avalonia.Controls.Primitives
{
	/// <summary>WPF GridViewHeaderRowPresenter bridge.</summary>
	public class GridViewHeaderRowPresenter : Control
	{
		// 阶段 5：使用 StyledProperty 以支持 XAML 中 Columns="{Binding ...}" 绑定语法。
		public static readonly StyledProperty<object> ColumnsProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(Columns));

		public static readonly StyledProperty<bool> AllowsColumnReorderProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, bool>(nameof(AllowsColumnReorder));

		public static readonly StyledProperty<object> ColumnHeaderContainerStyleProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderContainerStyle));

		public static readonly StyledProperty<object> ColumnHeaderTemplateProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderTemplate));

		public static readonly StyledProperty<object> ColumnHeaderTemplateSelectorProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderTemplateSelector));

		public static readonly StyledProperty<object> ColumnHeaderStringFormatProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderStringFormat));

		public static readonly StyledProperty<object> ColumnHeaderContextMenuProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderContextMenu));

		public static readonly StyledProperty<object> ColumnHeaderToolTipProperty =
			AvaloniaProperty.Register<GridViewHeaderRowPresenter, object>(nameof(ColumnHeaderToolTip));

		public object Columns
		{
			get => GetValue(ColumnsProperty);
			set => SetValue(ColumnsProperty, value);
		}

		public bool AllowsColumnReorder
		{
			get => GetValue(AllowsColumnReorderProperty);
			set => SetValue(AllowsColumnReorderProperty, value);
		}

		public object ColumnHeaderContainerStyle
		{
			get => GetValue(ColumnHeaderContainerStyleProperty);
			set => SetValue(ColumnHeaderContainerStyleProperty, value);
		}

		public object ColumnHeaderTemplate
		{
			get => GetValue(ColumnHeaderTemplateProperty);
			set => SetValue(ColumnHeaderTemplateProperty, value);
		}

		public object ColumnHeaderTemplateSelector
		{
			get => GetValue(ColumnHeaderTemplateSelectorProperty);
			set => SetValue(ColumnHeaderTemplateSelectorProperty, value);
		}

		public object ColumnHeaderStringFormat
		{
			get => GetValue(ColumnHeaderStringFormatProperty);
			set => SetValue(ColumnHeaderStringFormatProperty, value);
		}

		public object ColumnHeaderContextMenu
		{
			get => GetValue(ColumnHeaderContextMenuProperty);
			set => SetValue(ColumnHeaderContextMenuProperty, value);
		}

		public object ColumnHeaderToolTip
		{
			get => GetValue(ColumnHeaderToolTipProperty);
			set => SetValue(ColumnHeaderToolTipProperty, value);
		}
	}

	/// <summary>WPF GridViewRowPresenter bridge.</summary>
	public class GridViewRowPresenter : Control
	{
		public static readonly StyledProperty<object> ColumnsProperty =
			AvaloniaProperty.Register<GridViewRowPresenter, object>(nameof(Columns));

		public object Columns
		{
			get => GetValue(ColumnsProperty);
			set => SetValue(ColumnsProperty, value);
		}
	}
}

namespace Avalonia.Media
{
	/// <summary>WPF BooleanToVisibilityConverter bridge for Avalonia XAML.
	/// Converts bool → IsVisible (true=Visible, false=Collapsed).</summary>
	public class BooleanToVisibilityConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is bool b)
				return b;
			return false;
		}
	}
}

namespace Avalonia.SystemParams
{
	/// <summary>WPF SystemColors bridge - static brush keys for theme resources.</summary>
	public static class SystemColors
	{
		public static readonly object ControlTextBrushKey = "SystemControlTextBrush";
		public static readonly object WindowBrushKey = "SystemWindowBrush";
		public static readonly object ActiveCaptionBrushKey = "SystemActiveCaptionBrush";
		public static readonly object InactiveCaptionBrushKey = "SystemInactiveCaptionBrush";
		public static readonly object MenuBrushKey = "SystemMenuBrush";
		public static readonly object MenuBarBrushKey = "SystemMenuBarBrush";
		public static readonly object MenuTextBrushKey = "SystemMenuTextBrush";
		public static readonly object WindowTextBrushKey = "SystemWindowTextBrush";
		public static readonly object HighlightBrushKey = "SystemHighlightBrush";
		public static readonly object HighlightTextBrushKey = "SystemHighlightTextBrush";
		public static readonly object ControlBrushKey = "SystemControlBrush";
		public static readonly object ControlDarkBrushKey = "SystemControlDarkBrush";
		public static readonly object ControlLightBrushKey = "SystemControlLightBrush";
		public static readonly object GrayTextBrushKey = "SystemGrayTextBrush";
		public static readonly object InactiveSelectionHighlightBrushKey = "SystemInactiveSelectionHighlightBrush";
		public static readonly object InactiveSelectionHighlightTextBrushKey = "SystemInactiveSelectionHighlightTextBrush";
	}

	/// <summary>WPF SystemParameters bridge - static values for system metrics.</summary>
	public static class SystemParameters
	{
		public static double ScrollWidth => 17;
		public static double ScrollHeight => 17;
		public static double VerticalScrollBarWidth => 17;
		public static double HorizontalScrollBarHeight => 17;
		public static double ClientAreaWidth => 0;
		public static double ClientAreaHeight => 0;
		public static double FullPrimaryScreenWidth => 0;
		public static double FullPrimaryScreenHeight => 0;
		public static double WorkAreaWidth => 0;
		public static double WorkAreaHeight => 0;
		public static double MaximizedPrimaryScreenWidth => 0;
		public static double MaximizedPrimaryScreenHeight => 0;
		public static double CaptionHeight => 23;
		public static double MenuHeight => 18;
		public static double SmallIconWidth => 16;
		public static double SmallIconHeight => 16;
		public static double IconWidth => 32;
		public static double IconHeight => 32;
		public static double FixedFrameVerticalScrollBarWidth => 17;
		public static double FixedFrameHorizontalScrollBarHeight => 17;
		public static double WindowNonclientFrameThickness => 8;
		public static double WindowResizeBorderThickness => 4;

		// 以下成员兼容 Theme/Styles/*.xaml 中 {x:Static SystemParameters.*} 引用。
		// 原 WPF SystemParameters 提供 *Key 资源键与弹窗动画键，Avalonia 无等价物，
		// 此处返回常量占位值，确保 XAML 编译通过。
		/// <summary>WPF SystemParameters.MenuPopupAnimationKey 占位：返回 None。</summary>
		public static ForkPlus.UI.PopupAnimation MenuPopupAnimationKey => ForkPlus.UI.PopupAnimation.None;

		/// <summary>WPF SystemParameters.ComboBoxPopupAnimationKey 占位：返回 None。</summary>
		public static ForkPlus.UI.PopupAnimation ComboBoxPopupAnimationKey => ForkPlus.UI.PopupAnimation.None;

		/// <summary>WPF SystemParameters.VerticalScrollBarButtonHeightKey 占位：返回 60。</summary>
		public static double VerticalScrollBarButtonHeightKey => 60.0;

		/// <summary>WPF SystemParameters.VerticalScrollBarButtonHeight 占位常量。</summary>
		public const double VerticalScrollBarButtonHeight = 60.0;

		/// <summary>WPF SystemParameters.WindowGlassBrush 占位：返回透明画刷。</summary>
		public static IBrush WindowGlassBrush => Brushes.Transparent;

		/// <summary>WPF SystemParameters.MinimumHorizontalDragDistance / MinimumVerticalDragDistance 占位常量。</summary>
		public const double MinimumHorizontalDragDistance = 10.0;
		public const double MinimumVerticalDragDistance = 10.0;
	}
}
