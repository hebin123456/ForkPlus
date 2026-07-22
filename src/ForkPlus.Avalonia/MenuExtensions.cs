using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/MenuExtensions.cs（191 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - private class PasteCommand : ICommand（ApplicationCommands.Paste 代理）
    //   - SetItems(this ContextMenu, IEnumerable<Control>) → 清空 + 翻译 + 去重 + 父级分离 + Add
    //   - SetItems(this MenuItem, IEnumerable<Control>) → 同上
    //   - AddMenuItem(this MenuBase, string, RoutedEventHandler, Image, KeyGesture, bool) → 创建 MenuItem + 翻译 + 图标
    //   - AddMenuItemFormat(this MenuBase, string, object[], ...) → AddMenuItem + FormatMenuHeader
    //   - AddDefaultTextBoxMenuItems(this ContextMenu, IInputElement) → Cut/Copy/Paste 三个 MenuItem
    //   - AddSpellingMenuItems(this ContextMenu, SpellingError, IInputElement) → 拼写建议菜单项
    //   - private TranslateMenuControl / CloneIcon / SetItems(ItemCollection,...) / PrepareMenuControl
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. ContextMenu / MenuItem → Avalonia.Controls.ContextMenu / Avalonia.Controls.MenuItem
    //   2. MenuBase（WPF 中 Menu 和 MenuItem 的共同基类）在 Avalonia 11 无等价
    //      （Avalonia Menu 继承 SelectingItemsControl，MenuItem 继承 HeaderedSelectingItemsControl，无共同 Menu 基类）
    //      spike 版 AddMenuItem / AddMenuItemFormat 暂注释，Phase 4 菜单系统迁移时实现
    //   3. PreferencesLocalization（WPF 工程专有）spike 版跳过翻译
    //   4. VisualTreeAttachmentHelper.PrepareForNewParent / Describe（spike 版简化，跳过去重/父级分离）
    //   5. ApplicationCommands.Cut/Copy/Paste → Avalonia 11 命令系统不同，spike 版 AddDefaultTextBoxMenuItems 暂注释
    //   6. SpellingError（WPF 拼写检查）Avalonia 11 无内置等价，AddSpellingMenuItems 暂注释
    //   7. RoutedEventHandler → Avalonia.EventHandler / routed event，spike 版注释
    //   8. KeyGesture / Image / Separator → Avalonia 有等价类型但 API 不同，spike 版注释
    //
    // spike 版保留的简化 API：
    //   - SetItems(this ContextMenu, IEnumerable<Control>) → 直接 Clear + Add（跳过翻译/去重/分离）
    //   - SetItems(this MenuItem, IEnumerable<Control>) → 同上
    public static class MenuExtensions
    {
        // spike: 简化版 SetItems，跳过 WPF 的 PreferencesLocalization 翻译 / VisualTreeAttachmentHelper 去重分离
        public static void SetItems(this ContextMenu menu, IEnumerable<Control> items)
        {
            menu.Items.Clear();
            foreach (var item in items ?? Array.Empty<Control>())
            {
                menu.Items.Add(item);
            }
        }

        public static void SetItems(this MenuItem menu, IEnumerable<Control> items)
        {
            menu.Items.Clear();
            foreach (var item in items ?? Array.Empty<Control>())
            {
                menu.Items.Add(item);
            }
        }

        // spike: AddMenuItem(this MenuBase, ...) 依赖 WPF MenuBase 基类（Avalonia 无等价），暂注释
        // public static MenuItem AddMenuItem(this MenuBase menu, string header, RoutedEventHandler clickHandler, Image icon, KeyGesture keyGesture, bool isEnabled)

        // spike: AddMenuItemFormat(this MenuBase, ...) 依赖 WPF MenuBase，暂注释
        // public static MenuItem AddMenuItemFormat(this MenuBase menu, string header, object[] args, ...)

        // spike: AddDefaultTextBoxMenuItems 依赖 WPF ApplicationCommands，Avalonia 命令系统不同，暂注释
        // public static void AddDefaultTextBoxMenuItems(this ContextMenu contextMenu, IInputElement commandTarget)

        // spike: AddSpellingMenuItems 依赖 WPF SpellingError，Avalonia 无内置拼写检查，暂注释
        // public static void AddSpellingMenuItems(this ContextMenu contextMenu, SpellingError spellingError, IInputElement commandTarget)
    }
}
