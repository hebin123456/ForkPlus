using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class DiffSelectionLayer : ChunkSelectionLayer<CommitDiffSelectedRange>, ICommitDiffSelectionLayer
	{
		private readonly CommitCodeEditor _textEditor;

		private FloatingButton _unStageButton;

		private FloatingButton _stageButton;

		private FloatingButton _discardButton;

		[Null]
		CommitDiffSelectedRange ICommitDiffSelectionLayer.ActiveChunk => ActiveChunk;

		public event EventHandler Stage;

		public event EventHandler UnStage;

		public event EventHandler Discard;

		public DiffSelectionLayer(CommitCodeEditor editor)
			: base((CodeEditor)editor)
		{
			_textEditor = editor;
		}

		protected override void RefreshActiveChunk()
		{
			CommitDiffSelectedRange chunkUnderMousePointer = GetChunkUnderMousePointer();
			if (chunkUnderMousePointer != ActiveChunk)
			{
				ActiveChunk = chunkUnderMousePointer;
			}
		}

		protected override Control CreateAdornerContent(TextEditor textEditor)
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
			_unStageButton.Click += delegate
			{
				this.UnStage?.Invoke(this, EventArgs.Empty);
			};
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
				_stageButton.Click += delegate
			{
				this.Stage?.Invoke(this, EventArgs.Empty);
			};
			_discardButton.Click += delegate
			{
				this.Discard?.Invoke(this, EventArgs.Empty);
			};
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


		protected override void OnRender(DrawingContext drawingContext)
		{
			TextArea textArea = _textEditor.TextArea;
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
				CommitDiffSelectedRange activeChunk = ActiveChunk;
				if (activeChunk != null)
				{
					DrawChunk(drawingContext, textArea.TextView, activeChunk);
				}
			}
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
	}
}
