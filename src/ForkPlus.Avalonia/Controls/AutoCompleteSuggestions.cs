namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AutoCompleteSuggestions（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AutoCompleteSuggestions.cs（14 行）：
    //   - WPF AutoCompleteSuggestions : class（POCO）
    //   - int DropdownPosition { get; }
    //   - AutoCompleteSuggestion[] Suggestions { get; }
    //   - 构造函数 AutoCompleteSuggestions(int dropdownPosition, AutoCompleteSuggestion[] suggestions)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. POCO 类无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. AutoCompleteSuggestion 类型来自本 spike 命名空间
    //
    // spike 简化：
    //   - 与 WPF 完全一致的 POCO 类
    public class AutoCompleteSuggestions
    {
        // 对照 WPF: public int DropdownPosition { get; }
        public int DropdownPosition { get; }

        // 对照 WPF: public AutoCompleteSuggestion[] Suggestions { get; }
        public AutoCompleteSuggestion[] Suggestions { get; }

        // 对照 WPF: public AutoCompleteSuggestions(int dropdownPosition, AutoCompleteSuggestion[] suggestions)
        public AutoCompleteSuggestions(int dropdownPosition, AutoCompleteSuggestion[] suggestions)
        {
            DropdownPosition = dropdownPosition;
            Suggestions = suggestions;
        }
    }
}
