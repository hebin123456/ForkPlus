using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferenceNameAutocompleteProvider（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferenceNameAutocompleteProvider.cs（86 行）：
    //   - WPF ReferenceNameAutocompleteProvider : IAutoCompleteProvider
    //   - 构造函数接受 Reference[] references，预处理出 folder name 集合 _autocompleteSource
    //     （folder name = reference.Name 中最后一个 '/' 之前的部分；无 '/' 的不参与补全）
    //   - GetSuggestions(string text, int caretIndex)：
    //     - 找 text.LastIndexOf('/')，作为 dropdown 锚点
    //     - 按 '/' 分段，对 _autocompleteSource 中前缀匹配的项取第 num2 段 + '/'
    //     - 去重后构造 AutoCompleteSuggestion
    //   - GetFolderName(Reference) 按 LocalBranch.Name / RemoteBranch.ShortName / Tag.Name
    //     取最后一个 '/' 之前的子串
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   2. Reference / LocalBranch / RemoteBranch / Tag 来自 ForkPlus.Core.Git
    //   3. IListExtensions.ContainsItem / IReadOnlyListExtensions.Filter 来自 ForkPlus.Core
    //   4. [Null] Attribute 在 ForkPlus.Core 是 internal，spike 工程不可访问
    //      → spike 跳过 [Null] 标记
    //
    // spike 简化：
    //   - 与 WPF 完全一致的算法逻辑（无 UI 依赖，零行为差异）
    public class ReferenceNameAutocompleteProvider : IAutoCompleteProvider
    {
        // 对照 WPF: private readonly Reference[] _references
        private readonly Reference[] _references;

        // 对照 WPF: private readonly string[] _autocompleteSource
        private readonly string[] _autocompleteSource;

        // 对照 WPF: public ReferenceNameAutocompleteProvider(Reference[] references)
        public ReferenceNameAutocompleteProvider(Reference[] references)
        {
            _references = references;
            List<string> list = new List<string>(8);
            for (int i = 0; i < _references.Length; i++)
            {
                string folderName = GetFolderName(_references[i]);
                if (folderName != null && !list.Contains(folderName))
                {
                    list.Add(folderName);
                }
            }
            _autocompleteSource = list.ToArray();
        }

        // 对照 WPF: [Null] public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
        {
            if (text.Length == 0)
            {
                return null;
            }
            int num = text.LastIndexOf('/');
            if (num == -1)
            {
                num = 0;
            }
            List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(8);
            string[] array = text.Split('/');
            int num2 = array.Length - 1;
            string searchText = text.ToLower();
            foreach (string item in _autocompleteSource.Filter((string x) => x.ToLower().StartsWith(searchText)))
            {
                string[] array2 = item.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string suggestion = array2[num2] + "/";
                if (!list.ContainsItem((AutoCompleteSuggestion x) => x.Suggestion == suggestion))
                {
                    int start = ((array.Length > 1) ? (num + 1) : num);
                    list.Add(new AutoCompleteSuggestion(new Range(start, text.Length), suggestion));
                }
            }
            return new AutoCompleteSuggestions(num, list.ToArray());
        }

        // 对照 WPF: [Null] private static string GetFolderName(Reference reference)
        private static string GetFolderName(Reference reference)
        {
            string text;
            if (reference is LocalBranch localBranch)
            {
                text = localBranch.Name;
            }
            else if (reference is RemoteBranch remoteBranch)
            {
                text = remoteBranch.ShortName;
            }
            else
            {
                if (!(reference is Tag tag))
                {
                    return null;
                }
                text = tag.Name;
            }
            for (int num = text.Length - 1; num >= 0; num--)
            {
                if (text[num] == '/')
                {
                    return text.Substring(0, num);
                }
            }
            return null;
        }
    }
}
