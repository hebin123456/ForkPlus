using System;
using Avalonia.Controls;
using Avalonia.Input;
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

		// 阶段 4 里程碑 4.7-a：WPF OnPreviewMouseWheel + RaiseEvent(MouseWheelEvent) →
		// Avalonia OnPointerWheelChanged + 程序化滚动。Avalonia 无法在另一控件上重新引发
		// PointerWheelChanged 事件，改为直接调用 TextEditor.ScrollToVerticalOffset 转发滚轮增量。
		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			e.Handled = true;
			if (_weakEditor.TryGetTarget(out var target))
			{
				// Delta.Y > 0 = 向上滚，Delta.Y < 0 = 向下滚。每次滚动约 3 行（~40px/行）。
				double delta = e.Delta.Y * 40.0;
				target.ScrollToVerticalOffset(target.VerticalOffset - delta);
			}
		}
	}
}
