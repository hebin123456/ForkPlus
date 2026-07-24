using AvaloniaEdit.Document;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class HighlightingSource
	{
		public TextSegment Segment { get; }

		public HighlightingType HighlightingType { get; }

		public HighlightingSource(TextSegment segment, HighlightingType highlightingType)
		{
			Segment = segment;
			HighlightingType = highlightingType;
		}
	}
}
