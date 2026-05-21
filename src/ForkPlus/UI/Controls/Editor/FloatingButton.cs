using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace ForkPlus.UI.Controls.Editor
{
	public class FloatingButton : Button
	{
		private WeakReference<TextEditor> _weakEditor;

		public FloatingButton(TextEditor editor)
		{
			_weakEditor = new WeakReference<TextEditor>(editor);
		}

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			e.Handled = true;
			MouseWheelEventArgs mouseWheelEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
			mouseWheelEventArgs.RoutedEvent = UIElement.MouseWheelEvent;
			mouseWheelEventArgs.Source = this;
			if (_weakEditor.TryGetTarget(out var target))
			{
				target.TextArea.TextView.RaiseEvent(mouseWheelEventArgs);
			}
		}
	}
}
