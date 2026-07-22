namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 GitmojiAutoCompleteSuggestion（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/GitmojiAutoCompleteSuggestion.cs（19 行）：
    //   - WPF GitmojiAutoCompleteSuggestion : AutoCompleteSuggestion
    //   - GitmojiEntry Entry { get; }（原始 Gitmoji 条目，供 DataTemplate 显示用）
    //   - 构造函数传 entry.Emoji + " "（空格分隔）作为 base.Suggestion
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. POCO 类无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. GitmojiEntry 类型来自本 spike 命名空间（GitmojiData.cs 中定义）
    //   4. AutoCompleteSuggestion 基类来自本 spike 命名空间
    //
    // spike 简化：
    //   - 与 WPF 完全一致的 POCO 类
    public class GitmojiAutoCompleteSuggestion : AutoCompleteSuggestion
    {
        // 对照 WPF: public GitmojiEntry Entry { get; }
        public GitmojiEntry Entry { get; }

        // 对照 WPF: public GitmojiAutoCompleteSuggestion(Range range, GitmojiEntry entry)
        //   : base(range, entry.Emoji + " ")
        public GitmojiAutoCompleteSuggestion(Range range, GitmojiEntry entry)
            : base(range, entry.Emoji + " ")
        {
            Entry = entry;
        }
    }
}
