using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SplitTextDiffControl : Grid, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly DiffCodeEditor _editor = new DiffCodeEditor();

		[Null]
		public CodeEditorScrollPositionCache PositionCache { get; set; }

		public ScrollBarVisibility VerticalScrollBarVisibility
		{
			get
			{
				return _editor.VerticalScrollBarVisibility;
			}
			set
			{
				_editor.VerticalScrollBarVisibility = value;
			}
		}

		public ScrollBarVisibility HorizontalScrollBarVisibility
		{
			get
			{
				return _editor.HorizontalScrollBarVisibility;
			}
			set
			{
				_editor.HorizontalScrollBarVisibility = value;
			}
		}

		public double VerticalOffset => _editor.TextArea.TextView.VerticalOffset;

		[Null]
		public ForkPlus.Git.Diff.Diff Diff { get; private set; }

		public int TabWidth { get; private set; }

		public bool EntireFile { get; private set; }

		public DiffLocation Location { get; private set; }

		[Null]
		public VisualPatch VisualPatch => _editor.VisualPatch;

		public double FontSize
		{
			set
			{
				_editor.FontSize = value;
			}
		}

		public event EventHandler ScrollOffsetChanged
		{
			add
			{
				_editor.TextArea.TextView.ScrollOffsetChanged += value;
			}
			remove
			{
				_editor.TextArea.TextView.ScrollOffsetChanged -= value;
			}
		}

		public event EventHandler<ContextMenuEventArgs> EditorContextMenuOpening
		{
			add
			{
				_editor.ContextMenuOpening += value;
			}
			remove
			{
				_editor.ContextMenuOpening -= value;
			}
		}

		public SplitTextDiffControl()
		{
			base.Children.Add(_editor);
			_editor.ContextMenu = new ContextMenu();
			_editor.ContextMenuClosing += delegate
			{
				_editor.ContextMenu.Items.Clear();
			};
		}

		public void ScrollToLine(int line)
		{
			_editor.TextArea.Caret.Line = line;
			_editor.ScrollToLine(line);
		}

		public void ControlWillBeRemovedFromFileDiffControl()
		{
			PositionCache?.SaveScrollPosition(_editor);
		}

		public void SetDiff([Null] ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location)
		{
			Diff = diff;
			TabWidth = tabWidth;
			EntireFile = entireFile;
			Location = location;
			PositionCache?.SaveScrollPosition(_editor);
			_editor.Options.IndentationSize = tabWidth;
			_editor.VisualPatch = VisualPatch.CreateVisualPatch(Diff, EntireFile, Location);
			base.Dispatcher.Async(delegate
			{
				PositionCache?.RestoreScrollPosition(_editor);
			});
		}

		public void ScrollToVerticalOffset(double verticalOffset)
		{
			_editor.ScrollToVerticalOffset(verticalOffset);
		}

		public void RefreshDiffFont(double codeEditorFontSize)
		{
			_editor.FontSize = codeEditorFontSize;
		}

		public void RefreshDiffWordWrap(bool diffWordWrap)
		{
			_editor.WordWrap = diffWordWrap;
		}

		public void RefreshDiffShowHiddenSymbols(bool diffShowHiddenSymbols)
		{
			_editor.Options.ShowSpaces = diffShowHiddenSymbols;
			_editor.Options.ShowTabs = diffShowHiddenSymbols;
		}

		public void ScrollToPreviousCustomHunk()
		{
			_editor.ScrollToPreviousCustomHunk();
		}

		public void ScrollToNextCustomHunk()
		{
			_editor.ScrollToNextCustomHunk();
		}
	}
}
