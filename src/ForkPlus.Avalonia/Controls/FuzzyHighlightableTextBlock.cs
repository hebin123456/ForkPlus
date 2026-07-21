using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FuzzyHighlightableTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FuzzyHighlightableTextBlock.cs（26 行）+
    //         src/ForkPlus/UI/Controls/HighlightingTextBlockExtensions.cs:91-122（ApplyFuzzyHighlighting）：
    //   - WPF FuzzyHighlightableTextBlock : TextBlock（internal）
    //   - FuzzySearchString DependencyProperty（RegisterAttached）
    //   - ApplyFuzzyHighlighting 扩展方法（在 HighlightingTextBlockExtensions.cs）：
    //     - HasFuzzyMatch 检查是否存在模糊匹配（字符按顺序出现即可）
    //     - MatchPositions 计算最优匹配位置数组（int[]）
    //     - 遍历位置数组，连续匹配段用 Bold FontWeight，
    //       非匹配段用普通 Run
    //   - RestoreText 扩展方法：清空 Inlines + 单 Run 显示原文本
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 TextBlock → Avalonia.Controls.TextBlock（API 一致）
    //   2. DependencyProperty.RegisterAttached → StyledProperty<T>.Register
    //   3. WPF Run.FontWeight = FontWeights.Bold →
    //      Avalonia Run.FontWeight = FontWeight.Bold（API 一致）
    //      注意 Avalonia 11 用 Avalonia.Media.FontWeight 枚举（不是 WPF System.Windows.FontWeights 静态类）
    //   4. FuzzySearch.HasFuzzyMatch / MatchPositions 零成本复用（来自 ForkPlus.Core）
    //   5. spike 跳过 RestoreText 扩展方法（直接在类内实现，逻辑一致）
    //
    // spike 简化：
    //   - Query StyledProperty（task spec 关键 API：Query）
    //   - Text StyledProperty（task spec 关键 API：Text）
    //   - SetQuery(string) 公共方法（task spec 关键 API）
    //   - 内置 ApplyFuzzyHighlighting 逻辑（直接在类内实现）
    public class FuzzyHighlightableTextBlock : TextBlock
    {
        // 对照 WPF: FuzzySearchStringProperty（RegisterAttached，变化时 ApplyFuzzyHighlighting）
        // spike 版按 task spec 改名为 Query（task spec 关键 API）
        // Avalonia 11：AvaloniaProperty.Register 无 notifying 参数，改用 OnPropertyChanged 触发 ApplyHighlighting。
        public static readonly StyledProperty<string> QueryProperty =
            AvaloniaProperty.Register<FuzzyHighlightableTextBlock, string>(nameof(Query));

        // task spec 关键 API：Query 属性
        public string Query
        {
            get => GetValue(QueryProperty);
            set => SetValue(QueryProperty, value);
        }

        // task spec 关键 API：SetQuery(string) 公共方法
        public void SetQuery(string query)
        {
            Query = query;
        }

        // Avalonia 11：替代 WPF notifying 回调，Query 变化时触发 ApplyHighlighting。
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == QueryProperty)
            {
                ApplyHighlighting();
            }
        }

        // 对照 WPF: ApplyFuzzyHighlighting(this FuzzyHighlightableTextBlock, string fuzzySearchString)
        //   FuzzySearch.HasFuzzyMatch + MatchPositions → 高亮匹配字符（Bold）
        private void ApplyHighlighting()
        {
            string fuzzySearchString = Query;
            string text = Text;

            if (string.IsNullOrEmpty(fuzzySearchString) || !text.HasFuzzyMatch(fuzzySearchString))
            {
                RestoreText(text);
                return;
            }

            int[] positions = new int[fuzzySearchString.Length];
            text.MatchPositions(fuzzySearchString, positions);
            Inlines.Clear();

            int pos = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                int matchPos = positions[i];
                // 添加匹配前的普通文本
                int beforeLen = matchPos - pos;
                if (beforeLen > 0)
                {
                    Inlines.Add(new Run(text.Substring(pos, beforeLen)));
                }
                // 计算连续匹配段长度（连续位置 pos[i], pos[i]+1, pos[i]+2...）
                int matchLen = 1;
                while (i + 1 < positions.Length && positions[i + 1] == matchPos + 1)
                {
                    matchLen++;
                    i++;
                }
                // 添加高亮匹配段（对照 WPF: Run { FontWeight = FontWeights.Bold }）
                Inlines.Add(new Run(text.Substring(matchPos, matchLen))
                {
                    FontWeight = FontWeight.Bold
                });
                pos = matchPos + matchLen;
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
