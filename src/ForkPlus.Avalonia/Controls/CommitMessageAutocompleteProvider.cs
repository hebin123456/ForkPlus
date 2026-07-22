using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CommitMessageAutocompleteProvider（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CommitMessageAutocompleteProvider.cs（56 行）：
    //   - WPF CommitMessageAutocompleteProvider : IAutoCompleteProvider
    //   - UpdateUserIdentities(Dictionary<string, UserIdentity>) 更新用户身份表
    //   - GetSuggestions(string text, int caretIndex) → AutoCompleteSuggestions（可能 null）
    //     触发条件：text.Length > 0 && caretIndex > 0
    //     匹配规则：当前 token（'\n' 分隔）以 "Co-authored-by:" 前缀开头时：
    //       - "Co-authored-by:".StartsWith(token) → 提示 "Co-authored-by: "
    //       - token.StartsWith("Co-authored-by: ") → 提示用户名匹配
    //     排序：按 Suggestion 字符串比较
    //   - GetCurrentTokenRange(string text, int cursor, char terminator)
    //     返回 text.LastIndexOf(terminator, cursor)+1 .. cursor+1
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   2. Range / StringExtensions.Substring(Range) / UserIdentity 来自 ForkPlus.Core
    //   3. [Null] Attribute 在 ForkPlus.Core 是 internal，spike 工程不可访问
    //      → spike 跳过 [Null] 标记
    //   4. UserIdentityAutoCompleteSuggestion 类型来自本 spike 命名空间（UserIdentityAutoCompleteSuggestion.cs）
    //
    // spike 简化：
    //   - 与 WPF 完全一致的算法逻辑（无 UI 依赖，零行为差异）
    public class CommitMessageAutocompleteProvider : IAutoCompleteProvider
    {
        // 对照 WPF: private Dictionary<string, UserIdentity> _userIdentities
        private Dictionary<string, UserIdentity> _userIdentities = new Dictionary<string, UserIdentity>();

        // 对照 WPF: public void UpdateUserIdentities([Null] Dictionary<string, UserIdentity> userIdentities)
        public void UpdateUserIdentities(Dictionary<string, UserIdentity> userIdentities)
        {
            _userIdentities = userIdentities ?? new Dictionary<string, UserIdentity>();
        }

        // 对照 WPF: [Null] public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        {
            if (text.Length == 0 || caretIndex == 0)
            {
                return null;
            }
            List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(8);
            Range currentTokenRange = GetCurrentTokenRange(text, caretIndex - 1, '\n');
            int start = currentTokenRange.Start;
            if (currentTokenRange.Length > 1)
            {
                string text2 = text.Substring(currentTokenRange);
                if ("Co-authored-by:".StartsWith(text2, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new AutoCompleteSuggestion(currentTokenRange, "Co-authored-by: "));
                }
                else if (text2.StartsWith("Co-authored-by: "))
                {
                    Range range = new Range(currentTokenRange.Start + "Co-authored-by: ".Length, caretIndex);
                    start = range.Start;
                    string text3 = text.Substring(range);
                    foreach (UserIdentity value in _userIdentities.Values)
                    {
                        if (text3.Length == 0 || value.Name.IndexOf(text3, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            list.Add(new UserIdentityAutoCompleteSuggestion(range, value));
                        }
                    }
                }
                list.Sort((AutoCompleteSuggestion x, AutoCompleteSuggestion y) => x.Suggestion.CompareTo(y.Suggestion));
            }
            return new AutoCompleteSuggestions(start, list.ToArray());
        }

        // 对照 WPF: private static Range GetCurrentTokenRange(string text, int cursor, char terminator)
        private static Range GetCurrentTokenRange(string text, int cursor, char terminator)
        {
            int num = 1;
            return new Range(text.LastIndexOf(terminator, cursor) + num, cursor + 1);
        }
    }
}
