// 阶段 4.5：WPF System.Windows.Controls → Avalonia.Controls（ContextMenu 类型迁移）。
using Avalonia.Controls;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using ICSharpCode.AvalonEdit.Document;

namespace ForkPlus.UI.Controls.Commands
{
	public class HunkHistoryCommand
	{
		public void AddMenuItems(RepositoryUserControl repositoryUserControl, DiffCodeEditor editor, string path, ContextMenu menu)
		{
			(Range, VisualPatch)? selection = GetCharSelection(editor);
			menu.AddMenuItem("History for Selected Lines", delegate
			{
				Range item = selection.Value.Item1;
				VisualPatch item2 = selection.Value.Item2;
				int? visualLineNumber = item2.GetVisualLineNumber(item.Start);
				if (visualLineNumber.HasValue)
				{
					int valueOrDefault = visualLineNumber.GetValueOrDefault();
					visualLineNumber = item2.GetVisualLineNumber(item.End);
					if (visualLineNumber.HasValue)
					{
						int valueOrDefault2 = visualLineNumber.GetValueOrDefault();
						RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, new ShowFileHistoryWindowCommand.Mode.Hunk(path, new Range(valueOrDefault, valueOrDefault2)), null);
					}
					else
					{
						Log.Error($"Failed to find end line at {item.End}");
					}
				}
				else
				{
					Log.Error($"Failed to find start line at {item.Start}");
				}
			}, null, null, selection.HasValue);
		}

		public void AddMenuItems(RepositoryUserControl repositoryUserControl, TextContentControl editor, string path, ContextMenu menu)
		{
			Range? selection = GetLineSelection(editor);
			menu.AddMenuItem("History for Selected Lines", delegate
			{
				if (selection.HasValue)
				{
					Range valueOrDefault = selection.GetValueOrDefault();
					RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, new ShowFileHistoryWindowCommand.Mode.Hunk(path, valueOrDefault), null);
				}
			}, null, null, selection.HasValue);
		}

		private (Range, VisualPatch)? GetCharSelection(DiffCodeEditor editor)
		{
			VisualPatch visualPatch = editor.VisualPatch;
			if (visualPatch == null)
			{
				return null;
			}
			Range item = new Range(editor.SelectionStart, editor.SelectionStart + editor.SelectionLength);
			if (item.Length == 0)
			{
				return null;
			}
			return (item, visualPatch);
		}

		private Range? GetLineSelection(TextContentControl editor)
		{
			int selectionStart = editor.SelectionStart;
			int offset = editor.SelectionStart + editor.SelectionLength;
			DocumentLine lineByOffset = editor.Document.GetLineByOffset(selectionStart);
			DocumentLine lineByOffset2 = editor.Document.GetLineByOffset(offset);
			Range value = new Range(lineByOffset.LineNumber, lineByOffset2.LineNumber);
			if (value.Length == 0)
			{
				return null;
			}
			return value;
		}
	}
}
