namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 IAutoCompleteProvider（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/IAutoCompleteProvider.cs（8 行）：
    //   - WPF IAutoCompleteProvider : interface
    //   - [Null] AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
    //     （[Null] 是 ForkPlus.NullAttribute，标记返回值可能为 null）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 接口无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. [Null] Attribute 在 ForkPlus.Core 是 internal，spike 工程不可访问
    //      → spike 跳过 [Null] 标记（用 XML 注释说明返回值可能为 null）
    //
    // spike 简化：
    //   - 与 WPF 一致的接口契约（GetSuggestions 返回值可能为 null）
    public interface IAutoCompleteProvider
    {
        // 返回值可能为 null（对照 WPF [Null] 标记）
        AutoCompleteSuggestions GetSuggestions(string text, int caretIndex);
    }
}
