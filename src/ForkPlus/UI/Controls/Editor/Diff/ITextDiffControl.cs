using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public interface ITextDiffControl : DiffControlContainer.IFileDiffControlSubControl
	{
		CodeEditorScrollPositionCache PositionCache { get; set; }

		[Null]
		ForkPlus.Git.Diff.Diff Diff { get; }

		int TabWidth { get; }

		bool EntireFile { get; }

		DiffLocation Location { get; }

		ScrollBarVisibility VerticalScrollBarVisibility { get; set; }

		event EventHandler<ContextRequestedEventArgs> EditorContextMenuOpening;

		void SetDiff([Null] ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location);

		void RefreshDiffFont(double codeEditorFontSize);

		void RefreshDiffWordWrap(bool diffWordWrap);

		void RefreshDiffShowHiddenSymbols(bool diffShowHiddenSymbols);

		void ScrollToNextCustomHunk();

		void ScrollToPreviousCustomHunk();
	}
}
