using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 GitmojiAutocompleteProvider（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/GitmojiAutocompleteProvider.cs（96 行）：
    //   - WPF GitmojiAutocompleteProvider : IAutoCompleteProvider
    //   - MaxSuggestions = 12（限制返回的最大建议数）
    //   - GetSuggestions(string text, int caretIndex) → AutoCompleteSuggestions（可能 null）
    //     触发条件：从光标向前找最近的 ':'，且 ':' 到光标之间无空白
    //     匹配规则：去掉 shortName 两端 ':' 后与用户输入 prefix 做前缀/包含匹配
    //     限制：list.Count >= MaxSuggestions 后中断
    //   - 复用 GitmojiData.Entries 静态数据
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   2. GitmojiEntry / GitmojiData / GitmojiAutoCompleteSuggestion 来自本 spike 命名空间
    //   3. [Null] Attribute 在 ForkPlus.Core 是 internal，spike 工程不可访问
    //      → spike 跳过 [Null] 标记
    //
    // spike 简化：
    //   - 与 WPF 完全一致的算法逻辑（无 UI 依赖，零行为差异）
    public class GitmojiAutocompleteProvider : IAutoCompleteProvider
    {
        // 对照 WPF: private const int MaxSuggestions = 12
        private const int MaxSuggestions = 12;

        // 对照 WPF: [Null] public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text) || caretIndex <= 0)
            {
                return null;
            }

            // 从光标位置向前查找最近的 ":"，作为触发字符
            // 但 ":" 到光标之间不能有空白（否则视为普通冒号，不触发）
            int colonIndex = -1;
            for (int i = caretIndex - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == ':')
                {
                    colonIndex = i;
                    break;
                }
                if (char.IsWhiteSpace(c))
                {
                    return null;
                }
            }
            if (colonIndex < 0)
            {
                return null;
            }

            // 用户在 ":" 后输入的过滤前缀（不含 ":" 本身）
            string prefix = text.Substring(colonIndex + 1, caretIndex - colonIndex - 1);

            // 范围覆盖 ":prefix"，选中后整体替换为 emoji
            Range replaceRange = new Range(colonIndex, caretIndex);

            List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(MaxSuggestions);
            foreach (GitmojiEntry entry in GitmojiData.Entries)
            {
                // 去掉 shortName 两端的 ":"
                string shortNameNoColons = entry.ShortName;
                if (shortNameNoColons.StartsWith(":", StringComparison.Ordinal))
                {
                    shortNameNoColons = shortNameNoColons.Substring(1);
                }
                if (shortNameNoColons.EndsWith(":", StringComparison.Ordinal))
                {
                    shortNameNoColons = shortNameNoColons.Substring(0, shortNameNoColons.Length - 1);
                }

                bool match;
                if (prefix.Length == 0)
                {
                    match = true;
                }
                else
                {
                    match = shortNameNoColons.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        || shortNameNoColons.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (match)
                {
                    list.Add(new GitmojiAutoCompleteSuggestion(replaceRange, entry));
                    if (list.Count >= MaxSuggestions)
                    {
                        break;
                    }
                }
            }

            if (list.Count == 0)
            {
                return null;
            }
            return new AutoCompleteSuggestions(colonIndex, list.ToArray());
        }
    }
}
