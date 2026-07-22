namespace ForkPlus.Avalonia.Commands
{
    // spike 命令基础设施：带图标与快捷键文本的 UI 命令接口。
    // 对照 WPF src/ForkPlus/UI/Commands/IUICommand.cs（WPF 中暴露 Title / Shortcut / SecondaryShortcut）。
    // spike 阶段以 Icon emoji + ShortcutText 替代 KeyGesture，去除 WPF System.Windows.Input 依赖。
    // 实现 IUICommand 的命令会出现在菜单 / 工具栏 / 命令面板中。
    public interface IUICommand : IForkPlusCommand
    {
        string Icon { get; }
        string ShortcutText { get; }
    }
}
