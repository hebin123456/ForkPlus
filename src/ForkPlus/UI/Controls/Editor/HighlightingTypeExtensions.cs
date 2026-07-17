using System;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls.Editor
{
	/// <summary>
	/// 为 diff/语法高亮类型提供画刷。
	/// 优先从 Application.Resources 读取 Color 资源（key 形如 Diff.Add.Color / Syntax.Comment.Color），
	/// 这样用户在 CustomColorsDialog 修改对应颜色或切换主题后，HighlightingTypeExtensions 能即时返回新画刷。
	/// 取不到资源时回退到本文件内的硬编码默认值（按 light/dark 基底分组），保证旧皮肤不缺失颜色。
	/// 注意：从资源读到的画刷不 Freeze，使其能随资源变化更新；硬编码回退值仍 Freeze 以复用。
	/// </summary>
	public static class HighlightingTypeExtensions
	{
		// ===== Light 基底默认画刷（回退用） =====
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

		// ===== Dark 基底默认画刷（回退用） =====
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

		/// <summary>每类高亮对应的 Color 资源 key（用户可在 CustomColorsDialog 修改这些 key）。
		/// 同一个 HighlightingType 在 light/dark 基底下共用一个 key——由各 Colors.{Skin}.xaml 提供不同的值。</summary>
		private static string ResourceKeyFor(HighlightingType type)
		{
			switch (type)
			{
				case HighlightingType.Add: return "Diff.AddColor";
				case HighlightingType.Remove: return "Diff.RemoveColor";
				case HighlightingType.ExactAdd: return "Diff.ExactAddColor";
				case HighlightingType.ExactRemove: return "Diff.ExactRemoveColor";
				case HighlightingType.Service: return "Diff.ServiceColor";
				case HighlightingType.MergeAdd: return "Merge.AddColor";
				case HighlightingType.MergeRemove: return "Merge.RemoveColor";
				case HighlightingType.MergeRemote: return "Merge.RemoteColor";
				case HighlightingType.MergeLocal: return "Merge.LocalColor";
				case HighlightingType.MergeUnresolved: return "Merge.UnresolvedColor";
				case HighlightingType.Alignment: return "Diff.AlignmentColor";
				case HighlightingType.SyntaxComment: return "Syntax.CommentColor";
				case HighlightingType.SyntaxString: return "Syntax.StringColor";
				case HighlightingType.SyntaxKeyword: return "Syntax.KeywordColor";
				case HighlightingType.SyntaxType: return "Syntax.TypeColor";
				case HighlightingType.SyntaxCommand: return "Syntax.CommandColor";
				case HighlightingType.SyntaxAttribute: return "Syntax.AttributeColor";
				case HighlightingType.SyntaxVariable: return "Syntax.VariableColor";
				case HighlightingType.SyntaxValue: return "Syntax.ValueColor";
				case HighlightingType.SyntaxNumber: return "Syntax.NumberColor";
			}
			return null;
		}

		public static Brush GetHighlightBrush(this HighlightingType highlightingType, ThemeType theme)
		{
			// 优先读资源：自定义颜色覆盖或主题字典里定义了对应 key 就用它的 Color 构建新画刷。
			// 不 Freeze——这样下次资源变化时订阅者重绘会再调本方法拿到最新画刷。
			string key = ResourceKeyFor(highlightingType);
			if (key != null)
			{
				object res = Application.Current?.TryFindResource(key);
				if (res is Color c)
					return new SolidColorBrush(c);
				if (res is SolidColorBrush b)
					return b;
			}
			// 回退到硬编码默认值（按基底明暗分组）。
			return theme.IsDarkBase()
				? GetDarkHighlightBrush(highlightingType)
				: GetLightHighlightBrush(highlightingType);
		}

		private static Brush GetLightHighlightBrush(HighlightingType highlightingType)
		{
			switch (highlightingType)
			{
			case HighlightingType.Add: return AddBrush;
			case HighlightingType.Remove: return RemoveBrush;
			case HighlightingType.ExactAdd: return ExactAddBrush;
			case HighlightingType.ExactRemove: return ExactRemoveBrush;
			case HighlightingType.Service: return ServiceBrush;
			case HighlightingType.MergeAdd: return MergeAddBrush;
			case HighlightingType.MergeRemove: return MergeRemoveBrush;
			case HighlightingType.MergeRemote: return MergeRemoteBrush;
			case HighlightingType.MergeLocal: return MergeLocalBrush;
			case HighlightingType.MergeUnresolved: return MergeUnresolvedBrush;
			case HighlightingType.Alignment: return AlignmentBrush;
			case HighlightingType.SyntaxComment: return SyntaxCommentBrush;
			case HighlightingType.SyntaxString: return SyntaxStringBrush;
			case HighlightingType.SyntaxKeyword: return SyntaxKeywordBrush;
			case HighlightingType.SyntaxType: return SyntaxTypeBrush;
			case HighlightingType.SyntaxCommand: return SyntaxCommandBrush;
			case HighlightingType.SyntaxAttribute: return SyntaxAttributeBrush;
			case HighlightingType.SyntaxVariable: return SyntaxVariableBrush;
			case HighlightingType.SyntaxValue: return SyntaxValueBrush;
			case HighlightingType.SyntaxNumber: return SyntaxNumberBrush;
			}
			return Brushes.Transparent;
		}

		private static Brush GetDarkHighlightBrush(HighlightingType highlightingType)
		{
			switch (highlightingType)
			{
			case HighlightingType.Add: return AddBrushDark;
			case HighlightingType.Remove: return RemoveBrushDark;
			case HighlightingType.ExactAdd: return ExactAddBrushDark;
			case HighlightingType.ExactRemove: return ExactRemoveBrushDark;
			case HighlightingType.Service: return ServiceBrushDark;
			case HighlightingType.MergeAdd: return MergeAddBrushDark;
			case HighlightingType.MergeRemove: return MergeRemoveBrushDark;
			case HighlightingType.MergeRemote: return MergeRemoteBrushDark;
			case HighlightingType.MergeLocal: return MergeLocalBrushDark;
			case HighlightingType.MergeUnresolved: return MergeUnresolvedBrushDark;
			case HighlightingType.Alignment: return AlignmentBrushDark;
			case HighlightingType.SyntaxComment: return SyntaxCommentBrushDark;
			case HighlightingType.SyntaxString: return SyntaxStringBrushDark;
			case HighlightingType.SyntaxKeyword: return SyntaxKeywordBrushDark;
			case HighlightingType.SyntaxType: return SyntaxTypeBrushDark;
			case HighlightingType.SyntaxCommand: return SyntaxCommandBrushDark;
			case HighlightingType.SyntaxAttribute: return SyntaxAttributeBrushDark;
			case HighlightingType.SyntaxVariable: return SyntaxVariableBrushDark;
			case HighlightingType.SyntaxValue: return SyntaxValueBrushDark;
			case HighlightingType.SyntaxNumber: return SyntaxNumberBrushDark;
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
