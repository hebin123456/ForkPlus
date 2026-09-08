using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Controls.Editor;
using AvaloniaEdit;
using AvaloniaEdit.Editing;

namespace ForkPlus.UI.Controls.Commands
{
	public class CopyCommand
	{
		public void AddMenuItems(CodeEditor editor, ContextMenu menu)
		{
			menu.AddMenuItem("Copy", delegate
			{
				editor.Copy();
			}, null, new KeyGesture(Key.C, global::Avalonia.Input.KeyModifiers.Control), CanCopy(editor));
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
