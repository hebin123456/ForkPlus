// spike: 对照 WPF 工程 src/ForkPlus/UI/Commands/IUICommandExtension.cs
//   - WPF: internal static class IUICommandExtension
//     - InputGestureText(IUICommand) → command.Shortcut?.ToFriendlyString() ?? string.Empty
//     - CreateShortcutCommandBinding / CreateShortcutCommand / CreateMenuItem / CreateMenuItemFormat
//     （WPF-only：依赖 System.Windows.Input.KeyGesture / System.Windows.Controls.MenuItem /
//      System.Windows.Input.CommandBinding / RoutedCommand，用于菜单 + 快捷键注册）
//   - spike: 极简版，只保留 InputGestureText（用 spike 的 ShortcutText 字符串），
//     菜单 / 快捷键绑定由 Avalonia 视图层用 KeyBinding + NativeMenuItem 处理。
namespace ForkPlus.Avalonia.Commands
{
    internal static class IUICommandExtension
    {
        /// <summary>取命令快捷键显示文本（spike 直接返回 ShortcutText，WPF 用 KeyGesture.ToFriendlyString）。</summary>
        public static string InputGestureText(this IUICommand command)
        {
            return command.ShortcutText ?? string.Empty;
        }
    }
}
