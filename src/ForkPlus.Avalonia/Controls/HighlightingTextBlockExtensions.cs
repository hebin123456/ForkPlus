// Avalonia 版 HighlightingTextBlockExtensions（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/HighlightingTextBlockExtensions.cs（301 行）：
//   - ApplySearchAndButrackerHighlighting：搜索高亮 + bugtracker 超链接
//   - ApplyFuzzyHighlighting：模糊搜索高亮
//   - ApplySearchHighlighting：普通搜索高亮
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF TextBlock.Inlines + Run + Hyperlink → Avalonia TextBlock.Inlines + Run
//      （Avalonia 无 Hyperlink inline，spike 版省略 bugtracker 超链接，仅保留搜索高亮）
//   2. WPF Theme.FindBrush → Avalonia Application.Current.TryGetResource
//   3. WPF Range 类 → spike 用 (int Start, int End) 元组
//   4. bugtracker 超链接功能 → spike 省略（保留方法签名但空实现）
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    internal static class HighlightingTextBlockExtensions
    {
        // spike 简化：仅高亮搜索匹配，省略 bugtracker 超链接
        public static void ApplySearchAndButrackerHighlighting(this TextBlock textBlock, string? highlightString, object? bugtrackers)
        {
            // spike: bugtracker 超链接省略，直接调用搜索高亮
            ApplySearchHighlighting(textBlock, highlightString);
        }

        public static void ApplyFuzzyHighlighting(this FuzzyHighlightableTextBlock textBlock, string? fuzzySearchString)
        {
            string? text = textBlock.Text;
            if (string.IsNullOrEmpty(fuzzySearchString) || text == null || !text.HasFuzzyMatch(fuzzySearchString))
            {
                RestoreText(textBlock);
                return;
            }
            int[] array = new int[fuzzySearchString.Length];
            text.MatchPositions(fuzzySearchString, array);
            textBlock.Inlines?.Clear();
            int num = 0;
            for (int i = 0; i < array.Length; i++)
            {
                int num2 = array[i];
                int length = num2 - num;
                if (length > 0)
                {
                    textBlock.Inlines?.Add(new Run(text.Substring(num, length)));
                }
                int num3 = 1;
                for (; i + 1 < array.Length && array[i + 1] == num2 + 1; i++)
                {
                    num3++;
                }
                textBlock.Inlines?.Add(new Run(text.Substring(num2, num3)) { FontWeight = FontWeight.Bold });
                num = num2 + num3;
            }
            if (num < text.Length)
            {
                textBlock.Inlines?.Add(new Run(text.Substring(num)));
            }
        }

        public static void ApplySearchHighlighting(this TextBlock textBlock, string? highlightString)
        {
            string? text = textBlock.Text;
            if (string.IsNullOrEmpty(highlightString) || text == null)
            {
                RestoreText(textBlock);
                return;
            }
            int idx = text.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
            {
                RestoreText(textBlock);
                return;
            }
            int length = highlightString.Length;
            int pos = 0;
            var matchBrush = GetMatchBrush();
            textBlock.Inlines?.Clear();
            while (idx != -1)
            {
                int beforeLen = idx - pos;
                if (beforeLen > 0)
                {
                    textBlock.Inlines?.Add(new Run(text.Substring(pos, beforeLen)));
                }
                textBlock.Inlines?.Add(new Run(text.Substring(idx, length)) { Background = matchBrush });
                pos = idx + length;
                idx = text.IndexOf(highlightString, pos, StringComparison.OrdinalIgnoreCase);
            }
            if (pos < text.Length)
            {
                textBlock.Inlines?.Add(new Run(text.Substring(pos)));
            }
        }

        private static IBrush? GetMatchBrush()
        {
            // spike: 用固定黄色高亮，替代 WPF Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush")
            if (Application.Current?.TryGetResource("RevisionList.SearchMatch.ForegroundBrush", null, out var brush) == true)
            {
                return brush as IBrush;
            }
            return Brushes.Yellow;
        }

        private static void RestoreText(TextBlock textBlock)
        {
            var inlines = textBlock.Inlines;
            if (inlines != null && inlines.Count > 1)
            {
                string? text = textBlock.Text;
                inlines.Clear();
                if (!string.IsNullOrEmpty(text))
                {
                    inlines.Add(new Run(text));
                }
            }
        }
    }
}
