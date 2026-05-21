using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.UI.UserControls.Preferences;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	public class MergeChunkSelectionLayer : ChunkSelectionLayer<MergeConflictView.Chunk>
	{
		private readonly MergeCodeEditor _textEditor;

		private FloatingButton _selectButton;

		public MergeChunkSelectionLayer(MergeCodeEditor mergeCodeEditor)
			: base((CodeEditor)mergeCodeEditor)
		{
			_textEditor = mergeCodeEditor;
		}

		protected override FrameworkElement CreateAdornerContent(TextEditor textEditor)
		{
			_selectButton = new FloatingButton(textEditor);
			RefreshButtonsState();
			WeakEventManager<FloatingButton, RoutedEventArgs>.AddHandler(_selectButton, "Click", delegate
			{
				MergeConflictView.Chunk activeChunk = ActiveChunk;
				if (activeChunk != null)
				{
					if (activeChunk.AllItemSelected(_textEditor.ViewMode))
					{
						_textEditor.OnMergeChunkRemoved(activeChunk);
					}
					else
					{
						_textEditor.OnMergeChunkAdded(activeChunk);
					}
					RefreshButtonsState();
				}
			});
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Background = Brushes.Transparent
			};
			stackPanel.Children.Add(_selectButton);
			return new Border
			{
				Child = stackPanel,
				Background = Theme.Diff.FloatingButtonContainerBackground,
				CornerRadius = new CornerRadius(3.0),
				Margin = new Thickness(0.0, 0.0, 20.0, 0.0)
			};
		}

		protected override void RefreshActiveChunk()
		{
			MergeConflictView.Chunk chunkUnderMousePointer = GetChunkUnderMousePointer();
			if (chunkUnderMousePointer != ActiveChunk)
			{
				ActiveChunk = chunkUnderMousePointer;
				RefreshButtonsState();
			}
		}

		private void RefreshButtonsState()
		{
			MergeConflictView.Chunk activeChunk = ActiveChunk;
			if (activeChunk == null)
			{
				RemoveChunkAdorner();
			}
			else if (_textEditor.ViewMode == MergeConflictPart.Local)
			{
				if (activeChunk.AllItemSelected(_textEditor.ViewMode))
				{
					_selectButton.Content = PreferencesLocalization.Current("Remove Right");
				}
				else
				{
					_selectButton.Content = PreferencesLocalization.Current("Select Right");
				}
			}
			else if (_textEditor.ViewMode == MergeConflictPart.Remote)
			{
				if (activeChunk.AllItemSelected(_textEditor.ViewMode))
				{
					_selectButton.Content = PreferencesLocalization.Current("Remove Left");
				}
				else
				{
					_selectButton.Content = PreferencesLocalization.Current("Select Left");
				}
			}
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			TextArea textArea = _textEditor.TextArea;
			if (_textEditor.ViewMode == MergeConflictPart.Local || _textEditor.ViewMode == MergeConflictPart.Remote)
			{
				MergeConflictView.Chunk activeChunk = ActiveChunk;
				if (activeChunk != null && activeChunk.Node is MergeConflict.ConflictChunk)
				{
					DrawChunk(drawingContext, textArea.TextView, activeChunk);
				}
			}
		}

		protected override Rect? GetRectForChunk(MergeConflictView.Chunk chunk)
		{
			TextView textView = _textEditor.TextArea.TextView;
			Rect? result = null;
			MergeConflictView.Line[] lines = chunk.Lines;
			foreach (MergeConflictView.Line line in lines)
			{
				VisualLine visualLine = textView.GetVisualLine(line.LineNumber + 1);
				if (visualLine != null)
				{
					int lineNumber = line.LineNumber;
					int lineNumber2 = chunk.Lines[chunk.Lines.Length - 1].LineNumber;
					result = CreateLineBlockRect(visualLine, lineNumber2 - lineNumber + 1);
					break;
				}
			}
			return result;
		}

		protected override MergeConflictView.Chunk GetChunkByOffset(int offset)
		{
			return _textEditor.MergeConflictView?.GetConflictedChunkAt(offset);
		}

		protected override void ShowAdornerOnMouseOver(double topPosition)
		{
			topPosition = ((!(topPosition < 0.0)) ? (topPosition - 15.0) : 0.0);
			base.ShowAdornerOnMouseOver(topPosition);
		}
	}
}
