using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 HighlightableTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/HighlightableTextBlock.cs（25 行）+
    //         src/ForkPlus/UI/Controls/HighlightingTextBlockExtensions.cs（302 行）：
    //   - WPF HighlightableTextBlock : TextBlock（internal）
    //   - HighlightString DependencyProperty（RegisterAttached）
    //   - ApplySearchHighlighting 扩展方法（在 HighlightingTextBlockExtensions.cs）：
    //     - 大小写不敏感搜索匹配位置
    //     - 匹配部分用 Background=matchBrush + Foreground=foregroundBrush 高亮
    //     - 非匹配部分用普通 Run 显示
    //     - 画刷来自 Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush")
    //   - RestoreText 扩展方法：清空 Inlines + 单 Run 显示原文本
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 TextBlock → Avalonia.Controls.TextBlock（API 一致）
    //   2. DependencyProperty.RegisterAttached → StyledProperty<T>.Register
    //   3. WPF Run.Foreground = Brush → Avalonia Run.Foreground = IBrush（API 一致）
    //   4. WPF Run.Background = Brush → Avalonia Run.Background = IBrush（API 一致）
    //   5. spike 跳过 Theme.FindBrush 引用（Theme 类未迁移）
    //      用硬编码颜色 spike 兜底：
    //      - foregroundBrush = #333333（深灰，主文本色）
    //      - matchBrush = #FFFF00（黄色，搜索匹配高亮背景）
    //   6. spike 跳过 ApplySearchAndButrackerHighlighting（bugtracker 链接识别，
    //      需要 BugtrackerLinkDefinition + Hyperlink + ContextMenu，
    //      spike 不实现 issue tracker 链接识别）
    //
    // spike 简化：
    //   - Highlight StyledProperty（task spec 关键 API：Highlight）
    //   - Text StyledProperty（task spec 关键 API：Text）
    //   - SetHighlight(string) 公共方法（task spec 关键 API）
    //   - 内置 ApplySearchHighlighting 逻辑（直接在类内实现，不拆扩展方法）
    public class HighlightableTextBlock : TextBlock
    {
        // 对照 WPF: HighlightStringProperty（RegisterAttached，变化时 ApplySearchHighlighting）
        // Avalonia 11：AvaloniaProperty.Register 无 notifying 参数，改用 OnPropertyChanged 触发 ApplyHighlighting。
        public static readonly StyledProperty<string> HighlightProperty =
            AvaloniaProperty.Register<HighlightableTextBlock, string>(nameof(Highlight));

        // spike 版硬编码画刷（替代 WPF Theme.FindBrush）
        // 对照 WPF: foreground = Theme.FindBrush("ForegroundBrush")
        // 对照 WPF: background = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush")
        private static readonly IBrush ForegroundBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        private static readonly IBrush MatchBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));

        // task spec 关键 API：Highlight 属性
        public string Highlight
        {
            get => GetValue(HighlightProperty);
            set => SetValue(HighlightProperty, value);
        }

        // task spec 关键 API：SetHighlight(string) 公共方法
        public void SetHighlight(string highlight)
        {
            Highlight = highlight;
        }

        // Avalonia 11：替代 WPF notifying 回调，Highlight 变化时触发 ApplyHighlighting。
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HighlightProperty)
            {
                ApplyHighlighting();
            }
        }

        // 对照 WPF: ApplySearchHighlighting(this TextBlock, string highlightString)
        //   大小写不敏感搜索匹配位置 + 高亮匹配部分
        private void ApplyHighlighting()
        {
            string highlightString = Highlight;
            string text = Text;

            if (string.IsNullOrEmpty(highlightString))
            {
                RestoreText(text);
                return;
            }

            int index = text.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
            {
                RestoreText(text);
                return;
            }

            int length = highlightString.Length;
            int pos = 0;
            Inlines.Clear();

            while (index != -1)
            {
                // 添加匹配前的普通文本
                if (index > pos)
                {
                    Inlines.Add(new Run(text.Substring(pos, index - pos)));
                }
                // 添加高亮匹配部分（Background=matchBrush + Foreground=foregroundBrush）
                Inlines.Add(new Run(text.Substring(index, length))
                {
                    Background = MatchBrush,
                    Foreground = ForegroundBrush
                });
                pos = index + length;
                index = text.IndexOf(highlightString, pos, StringComparison.OrdinalIgnoreCase);
            }

            // 添加最后一段普通文本
            if (pos < text.Length)
            {
                Inlines.Add(new Run(text.Substring(pos)));
            }
        }

        // 对照 WPF: RestoreText(this TextBlock)
        //   清空 Inlines + 单 Run 显示原文本
        private void RestoreText(string text)
        {
            if (Inlines.Count > 1)
            {
                Inlines.Clear();
                Inlines.Add(new Run(text));
            }
        }
    }
}
