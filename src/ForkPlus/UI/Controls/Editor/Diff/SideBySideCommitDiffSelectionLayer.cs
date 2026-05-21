using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SideBySideCommitDiffSelectionLayer : ChunkSelectionLayer<CommitDiffSelectedRange>, IWeakEventListener, ICommitDiffSelectionLayer
	{
		[Null]
		private CommitDiffSelectedRange _activeSiblingChunk;

		private readonly CommitCodeEditor _textEditor;

		private FloatingButton _unStageButton;

		private FloatingButton _stageButton;

		private FloatingButton _discardButton;

		[Null]
		public override CommitDiffSelectedRange ActiveChunk
		{
			get
			{
				return base.ActiveChunk;
			}
			set
			{
				if (base.ActiveChunk != value)
				{
					Sibling.ActiveSiblingChunk = value;
					base.ActiveChunk = value;
				}
			}
		}

		public SideBySideCommitDiffSelectionLayer Sibling { get; internal set; }

		[Null]
		public CommitDiffSelectedRange ActiveSiblingChunk
		{
			get
			{
				return _activeSiblingChunk;
			}
			set
			{
				if (_activeSiblingChunk == value)
				{
					return;
				}
				_activeSiblingChunk = value;
				if (_activeSiblingChunk == null)
				{
					ActiveSiblingChunkView = null;
					InvalidateAdornerVisibility();
					InvalidateVisual();
					return;
				}
				_activeChunk = null;
				VisualChunk[] visualChunks = _textEditor.VisualPatch.VisualDiff.VisualChunks;
				foreach (VisualChunk visualChunk in visualChunks)
				{
					if (visualChunk.Node == _activeSiblingChunk.VisualChunk.Node)
					{
						ActiveSiblingChunkView = new CommitDiffSelectedRange(visualChunk, _activeSiblingChunk.CustomHunkIndex);
						InvalidateAdornerVisibility();
						InvalidateVisual();
						break;
					}
				}
			}
		}

		[Null]
		private CommitDiffSelectedRange ActiveSiblingChunkView { get; set; }

		public event EventHandler Stage;

		public event EventHandler UnStage;

		public event EventHandler Discard;

		public SideBySideCommitDiffSelectionLayer(CommitCodeEditor textEditor)
			: base((CodeEditor)textEditor)
		{
			_textEditor = textEditor;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			TextArea textArea = _textEditor.TextArea;
			if (textArea.TextView.ActualWidth == 0.0)
			{
				return;
			}
			if (textArea.Selection.Length != 0)
			{
				Geometry geometry = CreateSelectionGeometry(textArea);
				if (geometry != null)
				{
					drawingContext.DrawGeometry(ChunkBackgroundBrush, null, geometry);
					DrawSelectionBorder(drawingContext, textArea);
				}
			}
			else
			{
				CommitDiffSelectedRange commitDiffSelectedRange = ActiveChunk ?? ActiveSiblingChunkView;
				if (commitDiffSelectedRange != null)
				{
					DrawChunk(drawingContext, textArea.TextView, commitDiffSelectedRange);
				}
			}
		}

		protected override void OnTextAreaSelectionChanged()
		{
			if (_textEditor.TextArea.Selection.Length > 0)
			{
				Sibling.ClearSelection();
			}
			base.OnTextAreaSelectionChanged();
		}

		protected override void InvalidateAdornerVisibility()
		{
			if ((_activeChunk != null && _textEditor.DiffViewMode == DiffViewMode.SideBySideNew) || _textEditor.TextArea.Selection.Length > 0)
			{
				ShowChunkAdorner(0.0);
			}
			else
			{
				RemoveChunkAdorner();
			}
		}

		protected override void RefreshActiveChunk()
		{
			if (_textEditor.TextArea.Selection.Length > 0 || Sibling._textEditor.TextArea.Selection.Length > 0)
			{
				ActiveChunk = null;
			}
			else
			{
				ActiveChunk = GetChunkUnderMousePointer();
			}
		}

		private void ClearSelection()
		{
			_textEditor.TextArea.ClearSelection();
			InvalidateAdornerVisibility();
		}

		protected override Rect? GetRectForChunk(CommitDiffSelectedRange chunk)
		{
			TextView textView = _textEditor.TextArea.TextView;
			Range range = chunk.VisualChunk.CustomHunks[chunk.CustomHunkIndex];
			for (int i = range.Start; i < range.End; i++)
			{
				ICSharpCode.AvalonEdit.Rendering.VisualLine visualLine = textView.GetVisualLine(chunk.VisualChunk.VisualLines[i].LineNumber + 1);
				if (visualLine != null)
				{
					int lineCount = range.End - i;
					return CreateLineBlockRect(visualLine, lineCount);
				}
			}
			return null;
		}

		protected override void ShowAdornerOnMouseOver(double topPosition)
		{
			if (_textEditor.DiffViewMode == DiffViewMode.SideBySideNew)
			{
				base.ShowAdornerOnMouseOver(topPosition);
			}
		}

		protected override void DrawBorder(Rect rect, DrawingContext drawingContext)
		{
			if (_textEditor.DiffViewMode == DiffViewMode.SideBySideOld)
			{
				base.DrawBorder(rect, drawingContext);
			}
			else if (_textEditor.DiffViewMode == DiffViewMode.SideBySideNew)
			{
				DrawCroppedBorder(rect, drawingContext);
			}
		}

		private void DrawCroppedBorder(Rect rect, DrawingContext drawingContext)
		{
			int num = 2;
			int num2 = num * 2;
			drawingContext.DrawGeometry(geometry: new RectangleGeometry(new Rect(rect.X - (double)num, rect.Y, rect.Width + (double)num2, rect.Height)), brush: ChunkBackgroundBrush, pen: ChunkSelectionLayer<CommitDiffSelectedRange>._chunkBorderPen);
		}

		[Null]
		protected override CommitDiffSelectedRange GetChunkByOffset(int offset)
		{
			VisualPatch visualPatch = _textEditor.VisualPatch;
			if (visualPatch == null)
			{
				return null;
			}
			VisualChunk visualChunkAt = visualPatch.VisualDiff.GetVisualChunkAt(offset);
			if (visualChunkAt != null)
			{
				int? groupAt = visualChunkAt.GetGroupAt(offset);
				if (groupAt.HasValue)
				{
					int valueOrDefault = groupAt.GetValueOrDefault();
					return new CommitDiffSelectedRange(visualChunkAt, valueOrDefault);
				}
			}
			return null;
		}

		protected override FrameworkElement CreateAdornerContent(TextEditor textEditor)
		{
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Background = Brushes.Transparent
			};
			if (_textEditor.IsStaged)
			{
				_unStageButton = new FloatingButton(textEditor)
				{
					Content = PreferencesLocalization.Current("Unstage")
				};
				WeakEventManager<FloatingButton, RoutedEventArgs>.AddHandler(_unStageButton, "Click", delegate
				{
					this.UnStage?.Invoke(this, EventArgs.Empty);
				});
				stackPanel.Children.Add(_unStageButton);
			}
			else
			{
				_stageButton = new FloatingButton(textEditor)
				{
					Content = PreferencesLocalization.Current("Stage")
				};
				_discardButton = new FloatingButton(textEditor)
				{
					Content = PreferencesLocalization.Current("Discard..."),
					Margin = new Thickness(0.0, 2.0, 2.0, 2.0)
				};
				WeakEventManager<FloatingButton, RoutedEventArgs>.AddHandler(_stageButton, "Click", delegate
				{
					this.Stage?.Invoke(this, EventArgs.Empty);
				});
				WeakEventManager<FloatingButton, RoutedEventArgs>.AddHandler(_discardButton, "Click", delegate
				{
					this.Discard?.Invoke(this, EventArgs.Empty);
				});
				stackPanel.Children.Add(_stageButton);
				stackPanel.Children.Add(_discardButton);
			}
			return new Border
			{
				Child = stackPanel,
				Background = Theme.Diff.FloatingButtonContainerBackground,
				CornerRadius = new CornerRadius(3.0)
			};
		}

	}
}
