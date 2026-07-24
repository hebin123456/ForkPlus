using System;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Helpers
{
	/// <summary>
	/// 提供当前键盘修饰键状态的查询。
	///
	/// WPF 通过静态 <c>Keyboard.IsKeyDown(Key.LeftCtrl)</c> 查询全局按键状态。
	/// Avalonia 11 已将 <c>IKeyboardDevice</c> 标记为 [PrivateApi] 且不再公开
	/// <c>Modifiers</c> 属性，推荐在事件处理中直接使用 <see cref="KeyEventArgs.KeyModifiers"/>。
	///
	/// 本类保留静态门面以避免改动 37 个调用点：通过在主窗口订阅
	/// <see cref="InputElement.KeyDownEvent"/>/<see cref="InputElement.KeyUpEvent"/>
	/// 跟踪最近一次按键事件携带的 <see cref="KeyModifiers"/>。窗口失活时重置为
	/// <see cref="KeyModifiers.None"/>，避免跨窗口/对话框状态串扰。
	/// </summary>
	public static class KeyboardHelper
	{
		private static KeyModifiers _modifiers = KeyModifiers.None;

		public static bool IsShiftDown => _modifiers.HasFlag(KeyModifiers.Shift);

		public static bool IsCtrlDown => _modifiers.HasFlag(KeyModifiers.Control);

		public static bool IsAltDown => _modifiers.HasFlag(KeyModifiers.Alt);

		/// <summary>
		/// 在主窗口（TopLevel）上订阅键盘事件以跟踪修饰键状态。
	/// 应在窗口创建后调用一次（见 <see cref="ForkPlus.UI.MainWindow"/> 构造函数）。
		/// </summary>
		public static void Initialize(Avalonia.Controls.TopLevel topLevel)
		{
			if (topLevel == null)
			{
				return;
			}
			topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyEvent, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
			topLevel.AddHandler(InputElement.KeyUpEvent, OnKeyEvent, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
			if (topLevel is Avalonia.Controls.Window window)
			{
				window.Deactivated += OnDeactivated;
			}
		}

		private static void OnKeyEvent(object sender, KeyEventArgs e)
		{
			// KeyEventArgs.KeyModifiers 反映事件发生时刻的修饰键状态，
			// 在 KeyDown/KeyUp 上同步更新即可保持当前状态准确。
			_modifiers = e.KeyModifiers;
		}

		private static void OnDeactivated(object sender, EventArgs e)
		{
			// 窗口失活后不再接收按键事件，重置修饰键状态避免陈旧读取。
			_modifiers = KeyModifiers.None;
		}
	}
}
