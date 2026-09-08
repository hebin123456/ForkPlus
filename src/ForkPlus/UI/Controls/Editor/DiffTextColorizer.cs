using ForkPlus.Settings;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor
{
	public class DiffTextColorizer : DocumentColorizingTransformer
	{
		public int[] HunkHeaderLines { get; set; }

		protected override void ColorizeLine(DocumentLine line)
		{
			if (HunkHeaderLines != null && !line.IsDeleted && HunkHeaderLines.ContainsItem(line.LineNumber))
			{
				ChangeLinePart(line.Offset, line.EndOffset, HighlightHunkHeader);
			}
		}

		private static void HighlightHunkHeader(VisualLineElement e)
		{
			ThemeType theme = ForkPlusSettings.Default.Theme;
			e.TextRunProperties.SetForegroundBrush(HighlightingType.Service.GetHighlightBrush(theme));
		}
	}
}
