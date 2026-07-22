using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/HighlightingTypeExtensions.cs（171 行）：
    //   - public static class HighlightingTypeExtensions
    //   - GetHighlightBrush(this HighlightingType, ThemeType) 返回画刷
    //   - 优先从 Application.Resources 读 Color 资源（key 形如 Diff.AddColor / Syntax.CommentColor）
    //   - 取不到资源回退到本文件硬编码默认值（按 light/dark 基底分组）
    //   - 资源画刷不 Freeze（随资源变化更新）；硬编码回退值 Freeze 复用
    //
    // Avalonia 版差异：
    //   1. System.Windows.Media.Brush / SolidColorBrush / Color / Brushes.Transparent →
    //      Avalonia.Media.IBrush / SolidColorBrush / Color / Brushes.Transparent
    //   2. Application.Current.TryFindResource(key) 返回 object（WPF）→
    //      Avalonia Application.Current.TryFindResource(key, out value) 返回 bool
    //   3. brush.Freeze() 删除（Avalonia Brush immutable，无需 Freeze）
    //   4. ThemeType / IsDarkBase 来自 ForkPlus.Settings（Core，可访问）
    //   5. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    public static class HighlightingTypeExtensions
    {
        // ===== Light 基底默认画刷（回退用） =====
        private static readonly IBrush ExactAddBrush = new SolidColorBrush(Color.FromRgb(189, 243, 189));
        private static readonly IBrush ExactRemoveBrush = new SolidColorBrush(Color.FromRgb(254, 196, 197));
        private static readonly IBrush RemoveBrush = new SolidColorBrush(Color.FromRgb(255, 229, 229));
        private static readonly IBrush AddBrush = new SolidColorBrush(Color.FromRgb(226, 253, 227));
        private static readonly IBrush ServiceBrush = new SolidColorBrush(Color.FromRgb(196, 196, 196));
        private static readonly IBrush MergeRemoveBrush = new SolidColorBrush(Color.FromRgb(253, 240, 239));
        private static readonly IBrush MergeAddBrush = new SolidColorBrush(Color.FromRgb(240, 253, 239));
        private static readonly IBrush MergeRemoteBrush = new SolidColorBrush(Color.FromRgb(230, 231, 245));
        private static readonly IBrush MergeLocalBrush = new SolidColorBrush(Color.FromRgb(226, 240, 245));
        private static readonly IBrush MergeUnresolvedBrush = new SolidColorBrush(Color.FromRgb(255, 196, 196));
        private static readonly IBrush AlignmentBrush = new SolidColorBrush(Color.FromRgb(249, 249, 249));
        private static readonly IBrush SyntaxCommentBrush = new SolidColorBrush(Color.FromRgb(120, 120, 120));
        private static readonly IBrush SyntaxStringBrush = new SolidColorBrush(Color.FromRgb(29, 93, 190));
        private static readonly IBrush SyntaxKeywordBrush = new SolidColorBrush(Color.FromRgb(193, 64, 71));
        private static readonly IBrush SyntaxTypeBrush = new SolidColorBrush(Color.FromRgb(29, 93, 190));
        private static readonly IBrush SyntaxCommandBrush = new SolidColorBrush(Color.FromRgb(104, 72, 186));
        private static readonly IBrush SyntaxAttributeBrush = new SolidColorBrush(Color.FromRgb(193, 64, 71));
        private static readonly IBrush SyntaxVariableBrush = new SolidColorBrush(Color.FromRgb(104, 72, 186));
        private static readonly IBrush SyntaxValueBrush = new SolidColorBrush(Color.FromRgb(7, 89, 212));
        private static readonly IBrush SyntaxNumberBrush = new SolidColorBrush(Color.FromRgb(7, 89, 212));

        // ===== Dark 基底默认画刷（回退用） =====
        private static readonly IBrush ExactAddBrushDark = new SolidColorBrush(Color.FromRgb(56, 132, 66));
        private static readonly IBrush ExactRemoveBrushDark = new SolidColorBrush(Color.FromRgb(159, 66, 71));
        private static readonly IBrush RemoveBrushDark = new SolidColorBrush(Color.FromRgb(99, 63, 62));
        private static readonly IBrush AddBrushDark = new SolidColorBrush(Color.FromRgb(58, 92, 63));
        private static readonly IBrush ServiceBrushDark = new SolidColorBrush(Color.FromRgb(149, 149, 149));
        private static readonly IBrush MergeRemoveBrushDark = new SolidColorBrush(Color.FromRgb(94, 64, 63));
        private static readonly IBrush MergeAddBrushDark = new SolidColorBrush(Color.FromRgb(62, 85, 62));
        private static readonly IBrush MergeRemoteBrushDark = new SolidColorBrush(Color.FromRgb(92, 92, 118));
        private static readonly IBrush MergeLocalBrushDark = new SolidColorBrush(Color.FromRgb(53, 78, 89));
        private static readonly IBrush MergeUnresolvedBrushDark = new SolidColorBrush(Color.FromRgb(167, 73, 70));
        private static readonly IBrush AlignmentBrushDark = new SolidColorBrush(Color.FromRgb(66, 66, 66));
        private static readonly IBrush SyntaxCommentBrushDark = new SolidColorBrush(Color.FromRgb(145, 152, 161));
        private static readonly IBrush SyntaxStringBrushDark = new SolidColorBrush(Color.FromRgb(207, 159, 137));
        private static readonly IBrush SyntaxKeywordBrushDark = new SolidColorBrush(Color.FromRgb(100, 155, 209));
        private static readonly IBrush SyntaxTypeBrushDark = new SolidColorBrush(Color.FromRgb(100, 155, 209));
        private static readonly IBrush SyntaxCommandBrushDark = new SolidColorBrush(Color.FromRgb(100, 155, 209));
        private static readonly IBrush SyntaxAttributeBrushDark = new SolidColorBrush(Color.FromRgb(230, 76, 128));
        private static readonly IBrush SyntaxVariableBrushDark = new SolidColorBrush(Color.FromRgb(100, 155, 209));
        private static readonly IBrush SyntaxValueBrushDark = new SolidColorBrush(Color.FromRgb(190, 213, 168));
        private static readonly IBrush SyntaxNumberBrushDark = new SolidColorBrush(Color.FromRgb(190, 213, 168));

        // 每类高亮对应的 Color 资源 key（用户可在 CustomColorsDialog 修改这些 key）。
        // 同一个 HighlightingType 在 light/dark 基底下共用一个 key，由各 Colors.{Skin}.axaml 提供不同的值。
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

        public static IBrush GetHighlightBrush(this HighlightingType highlightingType, ThemeType theme)
        {
            // 优先读资源：自定义颜色覆盖或主题字典里定义了对应 key 就用它的 Color 构建新画刷。
            string key = ResourceKeyFor(highlightingType);
            if (key != null && Application.Current != null
                && Application.Current.TryGetResource(key, null, out var res))
            {
                if (res is Color c)
                    return new SolidColorBrush(c);
                if (res is ISolidColorBrush b)
                    return b;
            }
            // 回退到硬编码默认值（按基底明暗分组）。
            return theme.IsDarkBase()
                ? GetDarkHighlightBrush(highlightingType)
                : GetLightHighlightBrush(highlightingType);
        }

        private static IBrush GetLightHighlightBrush(HighlightingType highlightingType)
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

        private static IBrush GetDarkHighlightBrush(HighlightingType highlightingType)
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
    }
}
