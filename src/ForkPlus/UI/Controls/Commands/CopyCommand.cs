// 阶段 4.5：WPF System.Windows.* → Avalonia.*。WPF KeyGesture → Avalonia.Input.KeyGesture。WPF ModifierKeys → Avalonia.Input.KeyModifiers。
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Controls.Editor;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;

namespace ForkPlus.UI.Controls.Commands
{
	public class CopyCommand
	{
		public void AddMenuItems(CodeEditor editor, ContextMenu menu)
		{
			menu.AddMenuItem("Copy", delegate
			{
				editor.Copy();
			}, null, new KeyGesture(Key.C, KeyModifiers.Control), CanCopy(editor));
		}

		private static bool CanCopy(TextEditor editor)
		{
			TextArea textArea = editor.TextArea;
			if (textArea != null && textArea.Document != null)
			{
				return !textArea.Selection.IsEmpty;
			}
			return false;
		}
	}
}
