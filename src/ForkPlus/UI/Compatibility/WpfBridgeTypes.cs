// ⚠ 临时桥接类型 ─ 阶段 5 编译过渡用。
// 集中提供 WPF System.Windows.* 命名空间下被代码引用但 Avalonia 无对应的类型。
// 真正的迁移（阶段 6）会逐步替换为原生 Avalonia API，届时删除本文件。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

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
