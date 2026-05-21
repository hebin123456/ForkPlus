using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

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
