// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/DragAndDropListBox.cs（24 行）：
//   - public class DragAndDropListBox : ListBox
//   - GetContainerForItemOverride → new DragAndDropListBoxItem()
//   - IsItemItsOwnContainerOverride → item is DragAndDropListBoxItem
//   - PrepareContainerForItemOverride → base + set ParentListBox
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ListBox → Avalonia.Controls.ListBox（同名，API 略不同）
//   2. WPF GetContainerForItemOverride / IsItemItsOwnContainerOverride /
//      PrepareContainerForItemOverride → spike 不重写
//      （Avalonia 11 用 ControlTheme 配置 Container，spike 阶段默认用 ListBoxItem，
//       真实容器配置由 ControlTheme 在 Themes/Styles 中定义，Phase 3.9b 接入）
//   3. spike 版仅保留类定义（XAML 中引用 DragAndDropListBox 类型）
//   4. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class DragAndDropListBox : ListBox
    {
        // spike: 容器创建（对照 WPF GetContainerForItemOverride → new DragAndDropListBoxItem()）
        // Avalonia 11 用 ControlTheme 配置 Container，spike 简化为不重写（默认 ListBoxItem）
        // 真实容器配置由 ControlTheme 在 Themes/Styles 中定义（Phase 3.9b）
        // Phase 3.9b 在此补：
        //   - ControlTheme 中设置 ItemContainerTheme 为 DragAndDropListBoxItem
        //   - 或重写容器创建逻辑（若 Avalonia API 支持）
    }
}
