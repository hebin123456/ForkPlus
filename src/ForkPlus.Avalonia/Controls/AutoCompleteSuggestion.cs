namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AutoCompleteSuggestion（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AutoCompleteSuggestion.cs（14 行）：
    //   - WPF AutoCompleteSuggestion : class（POCO）
    //   - Range Range { get; }（来自 ForkPlus.Range struct）
    //   - string Suggestion { get; }
    //   - 构造函数 AutoCompleteSuggestion(Range range, string suggestion)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. POCO 类无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. Range 类型来自 ForkPlus.Core（ForkPlus.Range struct），通过 ProjectReference 引用
    //
    // spike 简化：
    //   - 与 WPF 完全一致的 POCO 类
    public class AutoCompleteSuggestion
    {
        // 对照 WPF: public Range Range { get; }
        public Range Range { get; }

        // 对照 WPF: public string Suggestion { get; }
        public string Suggestion { get; }

        // 对照 WPF: public AutoCompleteSuggestion(Range range, string suggestion)
        public AutoCompleteSuggestion(Range range, string suggestion)
        {
            Range = range;
            Suggestion = suggestion;
        }
    }
}
