using ForkPlus.UI;

// Avalonia spike 版 IRoundedSelectionListBoxViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/IRoundedSelectionListBoxViewModel.cs（8 行）：
//   - WPF: public interface IRoundedSelectionListBoxViewModel
//   - int Row { get; }
//   - ListBoxSelectionType SelectionType { get; set; }
//   - 依赖：ForkPlus.UI.ListBoxSelectionType（Core 可用）
//
// Avalonia 版差异：
//   1. 接口无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//   3. ListBoxSelectionType 来自 ForkPlus.Core（ForkPlus.UI 命名空间）
//
// spike 简化：
//   - 与 WPF 完全一致的接口
namespace ForkPlus.Avalonia
{
    public interface IRoundedSelectionListBoxViewModel
    {
        int Row { get; }
        ListBoxSelectionType SelectionType { get; set; }
    }
}
