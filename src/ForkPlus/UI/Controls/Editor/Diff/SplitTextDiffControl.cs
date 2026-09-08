using System;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using Avalonia.Threading;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SplitTextDiffControl : Grid, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly DiffCodeEditor _editor = new DiffCodeEditor();

		[Null]
		public CodeEditorScrollPositionCache PositionCache { get; set; }

		public global::Avalonia.Controls.Primitives.ScrollBarVisibility VerticalScrollBarVisibility
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

		public global::Avalonia.Controls.Primitives.ScrollBarVisibility HorizontalScrollBarVisibility
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

		public double VerticalOffset => _editor.TextArea.TextView.ScrollOffset.Y;

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

		public event ContextMenuEventHandler EditorContextMenuOpening
		{
			add
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_editor,(s, e) => value?.Invoke(s, e));
			}
			remove
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_editor,(s, e) => value?.Invoke(s, e));
			}
		}

		public SplitTextDiffControl()
		{
			_editor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
			_editor.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
			base.Children.Add(_editor);
			_editor.ContextMenu = new ContextMenu();
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuClosingHandler(_editor,delegate
			{
				_editor.ContextMenu.Items.Clear();
			});
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
			base.Dispatcher.Post(delegate
			{
				PositionCache?.RestoreScrollPosition(_editor);
			});
		}

		public void ScrollToVerticalOffset(double verticalOffset)
		{
			_editor.ScrollToVerticalOffsetCompat(verticalOffset);
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
