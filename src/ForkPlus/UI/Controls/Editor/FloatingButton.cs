using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public class FloatingButton : Button
	{
		private WeakReference<TextEditor> _weakEditor;

		public FloatingButton(TextEditor editor)
		{
			_weakEditor = new WeakReference<TextEditor>(editor);
		}

		protected override void OnPointerWheelChanged(global::Avalonia.Input.PointerWheelEventArgs e)
		{
			e.Handled = true;
			if (_weakEditor.TryGetTarget(out var target))
			{
				// Migration note：WPF new MouseWheelEventArgs(...) 转发 → Avalonia 12 构造器需 rootVisual 等复杂参数，
				// 直接复用原事件参数（先复位 Handled 再转发，保持“子级已处理、编辑器继续滚动”的语义）。
				e.Handled = false;
				target.TextArea.TextView.RaiseEvent(e);
				e.Handled = true;
			}
		}
	}
}
