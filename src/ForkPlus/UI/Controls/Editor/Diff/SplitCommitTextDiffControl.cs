using System;
using Avalonia.Controls;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SplitCommitTextDiffControl : Grid, ICommitTextDiffControl, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly CommitCodeEditor _editor = new CommitCodeEditor(DiffViewMode.Split);

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

		public bool IsStaged
		{
			get
			{
				return _editor.IsStaged;
			}
			set
			{
				_editor.IsStaged = value;
			}
		}

		public bool IsNewOrUntracked
		{
			get
			{
				return _editor.IsNewOrUntracked;
			}
			set
			{
				_editor.IsNewOrUntracked = value;
			}
		}

		[Null]
		public ForkPlus.Git.Diff.Diff Diff { get; private set; }

		public int TabWidth { get; private set; }

		public bool EntireFile { get; private set; }

		public DiffLocation Location { get; private set; }

		public event EventHandler<CommitCodeEditor> ToggleStage
		{
			add
			{
				_editor.ToggleStage += value;
			}
			remove
			{
				_editor.ToggleStage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Stage
		{
			add
			{
				_editor.Stage += value;
			}
			remove
			{
				_editor.Stage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Unstage
		{
			add
			{
				_editor.UnStage += value;
			}
			remove
			{
				_editor.UnStage -= value;
			}
		}

		public event EventHandler<CommitCodeEditor> Discard
		{
			add
			{
				_editor.Discard += value;
			}
			remove
			{
				_editor.Discard -= value;
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

		public SplitCommitTextDiffControl()
		{
			base.Children.Add(_editor);
			_editor.ContextMenu = new ContextMenu();
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

		public void RefreshDiffFont(double codeEditorFontSize)
		{
			_editor.FontSize = codeEditorFontSize;
		}

		public void RefreshDiffWordWrap(bool diffWordWrap)
		{
			if (_editor.DiffViewMode == DiffViewMode.Split)
			{
				_editor.WordWrap = diffWordWrap;
			}
			else
			{
				_editor.WordWrap = false;
			}
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
