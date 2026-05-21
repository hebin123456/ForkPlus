using System.Windows.Media;

namespace ForkPlus.UI.Controls.Editor
{
	public static class HighlightingTypeExtensions
	{
		private static readonly Brush ExactAddBrush = Freeze(new SolidColorBrush(Color.FromRgb(189, 243, 189)));

		private static readonly Brush ExactRemoveBrush = Freeze(new SolidColorBrush(Color.FromRgb(254, 196, 197)));

		private static readonly Brush RemoveBrush = Freeze(new SolidColorBrush(Color.FromRgb(byte.MaxValue, 229, 229)));

		private static readonly Brush AddBrush = Freeze(new SolidColorBrush(Color.FromRgb(226, 253, 227)));

		private static readonly Brush ServiceBrush = Freeze(new SolidColorBrush(Color.FromRgb(196, 196, 196)));

		private static readonly Brush MergeRemoveBrush = Freeze(new SolidColorBrush(Color.FromRgb(253, 240, 239)));

		private static readonly Brush MergeAddBrush = Freeze(new SolidColorBrush(Color.FromRgb(240, 253, 239)));

		private static readonly Brush MergeRemoteBrush = Freeze(new SolidColorBrush(Color.FromRgb(230, 231, 245)));

		private static readonly Brush MergeLocalBrush = Freeze(new SolidColorBrush(Color.FromRgb(226, 240, 245)));

		private static readonly Brush MergeUnresolvedBrush = Freeze(new SolidColorBrush(Color.FromRgb(byte.MaxValue, 196, 196)));

		private static readonly Brush AlignmentBrush = Freeze(new SolidColorBrush(Color.FromRgb(249, 249, 249)));

		private static readonly Brush SyntaxCommentBrush = Freeze(new SolidColorBrush(Color.FromRgb(120, 120, 120)));

		private static readonly Brush SyntaxStringBrush = Freeze(new SolidColorBrush(Color.FromRgb(29, 93, 190)));

		private static readonly Brush SyntaxKeywordBrush = Freeze(new SolidColorBrush(Color.FromRgb(193, 64, 71)));

		private static readonly Brush SyntaxTypeBrush = Freeze(new SolidColorBrush(Color.FromRgb(29, 93, 190)));

		private static readonly Brush SyntaxCommandBrush = Freeze(new SolidColorBrush(Color.FromRgb(104, 72, 186)));

		private static readonly Brush SyntaxAttributeBrush = Freeze(new SolidColorBrush(Color.FromRgb(193, 64, 71)));

		private static readonly Brush SyntaxVariableBrush = Freeze(new SolidColorBrush(Color.FromRgb(104, 72, 186)));

		private static readonly Brush SyntaxValueBrush = Freeze(new SolidColorBrush(Color.FromRgb(7, 89, 212)));

		private static readonly Brush SyntaxNumberBrush = Freeze(new SolidColorBrush(Color.FromRgb(7, 89, 212)));

		private static readonly Brush ExactAddBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(56, 132, 66)));

		private static readonly Brush ExactRemoveBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(159, 66, 71)));

		private static readonly Brush RemoveBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(99, 63, 62)));

		private static readonly Brush AddBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(58, 92, 63)));

		private static readonly Brush ServiceBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(149, 149, 149)));

		private static readonly Brush MergeRemoveBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(94, 64, 63)));

		private static readonly Brush MergeAddBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(62, 85, 62)));

		private static readonly Brush MergeRemoteBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(92, 92, 118)));

		private static readonly Brush MergeLocalBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(53, 78, 89)));

		private static readonly Brush MergeUnresolvedBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(167, 73, 70)));

		private static readonly Brush AlignmentBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(66, 66, 66)));

		private static readonly Brush SyntaxCommentBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(145, 152, 161)));

		private static readonly Brush SyntaxStringBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(207, 159, 137)));

		private static readonly Brush SyntaxKeywordBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(100, 155, 209)));

		private static readonly Brush SyntaxTypeBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(100, 155, 209)));

		private static readonly Brush SyntaxCommandBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(100, 155, 209)));

		private static readonly Brush SyntaxAttributeBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(230, 76, 128)));

		private static readonly Brush SyntaxVariableBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(100, 155, 209)));

		private static readonly Brush SyntaxValueBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(190, 213, 168)));

		private static readonly Brush SyntaxNumberBrushDark = Freeze(new SolidColorBrush(Color.FromRgb(190, 213, 168)));

		public static Brush GetHighlightBrush(this HighlightingType highlightingType, ThemeType theme)
		{
			switch (theme)
			{
			case ThemeType.Light:
				switch (highlightingType)
				{
				case HighlightingType.Add:
					return AddBrush;
				case HighlightingType.Remove:
					return RemoveBrush;
				case HighlightingType.ExactAdd:
					return ExactAddBrush;
				case HighlightingType.ExactRemove:
					return ExactRemoveBrush;
				case HighlightingType.Service:
					return ServiceBrush;
				case HighlightingType.MergeAdd:
					return MergeAddBrush;
				case HighlightingType.MergeRemove:
					return MergeRemoveBrush;
				case HighlightingType.MergeRemote:
					return MergeRemoteBrush;
				case HighlightingType.MergeLocal:
					return MergeLocalBrush;
				case HighlightingType.MergeUnresolved:
					return MergeUnresolvedBrush;
				case HighlightingType.Alignment:
					return AlignmentBrush;
				case HighlightingType.SyntaxComment:
					return SyntaxCommentBrush;
				case HighlightingType.SyntaxString:
					return SyntaxStringBrush;
				case HighlightingType.SyntaxKeyword:
					return SyntaxKeywordBrush;
				case HighlightingType.SyntaxType:
					return SyntaxTypeBrush;
				case HighlightingType.SyntaxCommand:
					return SyntaxCommandBrush;
				case HighlightingType.SyntaxAttribute:
					return SyntaxAttributeBrush;
				case HighlightingType.SyntaxVariable:
					return SyntaxVariableBrush;
				case HighlightingType.SyntaxValue:
					return SyntaxValueBrush;
				case HighlightingType.SyntaxNumber:
					return SyntaxNumberBrush;
				}
				break;
			case ThemeType.Dark:
				switch (highlightingType)
				{
				case HighlightingType.Add:
					return AddBrushDark;
				case HighlightingType.Remove:
					return RemoveBrushDark;
				case HighlightingType.ExactAdd:
					return ExactAddBrushDark;
				case HighlightingType.ExactRemove:
					return ExactRemoveBrushDark;
				case HighlightingType.Service:
					return ServiceBrushDark;
				case HighlightingType.MergeAdd:
					return MergeAddBrushDark;
				case HighlightingType.MergeRemove:
					return MergeRemoveBrushDark;
				case HighlightingType.MergeRemote:
					return MergeRemoteBrushDark;
				case HighlightingType.MergeLocal:
					return MergeLocalBrushDark;
				case HighlightingType.MergeUnresolved:
					return MergeUnresolvedBrushDark;
				case HighlightingType.Alignment:
					return AlignmentBrushDark;
				case HighlightingType.SyntaxComment:
					return SyntaxCommentBrushDark;
				case HighlightingType.SyntaxString:
					return SyntaxStringBrushDark;
				case HighlightingType.SyntaxKeyword:
					return SyntaxKeywordBrushDark;
				case HighlightingType.SyntaxType:
					return SyntaxTypeBrushDark;
				case HighlightingType.SyntaxCommand:
					return SyntaxCommandBrushDark;
				case HighlightingType.SyntaxAttribute:
					return SyntaxAttributeBrushDark;
				case HighlightingType.SyntaxVariable:
					return SyntaxVariableBrushDark;
				case HighlightingType.SyntaxValue:
					return SyntaxValueBrushDark;
				case HighlightingType.SyntaxNumber:
					return SyntaxNumberBrushDark;
				}
				break;
			}
			return Brushes.Transparent;
		}

		private static Brush Freeze(Brush brush)
		{
			brush.Freeze();
			return brush;
		}
	}
}
