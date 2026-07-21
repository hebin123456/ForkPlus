using System;
using System.Windows.Input;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FileDiffControlCommands（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileDiffControlCommands.cs（24 行）：
    //   - WPF FileDiffControlCommands : CommandContainer（ForkPlus.UI.Commands）
    //   - 4 个 Lazy 字段 + 4 个公共属性：
    //     - OpenFileInExternalEditor（OpenFileInExternalEditorCommand）
    //     - Copy（CopyCommand）
    //     - CopyAsPatch（CopyAsPatchCommand）
    //     - HunkHistory（HunkHistoryCommand）
    //   - CommandContainer.Lazy(ref field) 延迟初始化模式
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF CommandContainer 基类 + Lazy 模式 → spike 静态类 + RoutedCommand 字段
    //   2. WPF System.Windows.Input.RoutedCommand → Avalonia 11 无内置 RoutedCommand
    //      spike 自定义 RoutedCommand 类实现 ICommand（BCL System.Windows.Input.ICommand）
    //   3. WPF 4 个具体 Command 类（OpenFileInExternalEditorCommand 等）→ spike 4 个 RoutedCommand
    //      （spike 不迁移具体 Command 实现逻辑，仅保留 RoutedCommand 标识符）
    //   4. spike 保持 4 个公共属性名与 WPF 一致
    //
    // spike 简化（task spec 关键 API）：
    //   - 静态类 + 4 个 RoutedCommand 公共字段
    //   - OpenFileInExternalEditor / Copy / CopyAsPatch / HunkHistory
    //   - RoutedCommand 类：spike 自实现 ICommand（Avalonia 11 无内置 RoutedCommand）
    public static class FileDiffControlCommands
    {
        // 对照 WPF: OpenFileInExternalEditorCommand（在外部编辑器打开文件）
        public static readonly RoutedCommand OpenFileInExternalEditor = new RoutedCommand();

        // 对照 WPF: CopyCommand（复制选中内容）
        public static readonly RoutedCommand Copy = new RoutedCommand();

        // 对照 WPF: CopyAsPatchCommand（复制为 patch 格式）
        public static readonly RoutedCommand CopyAsPatch = new RoutedCommand();

        // 对照 WPF: HunkHistoryCommand（查看 hunk 历史）
        public static readonly RoutedCommand HunkHistory = new RoutedCommand();
    }

    // spike 版 RoutedCommand（Avalonia 11 无内置 RoutedCommand，spike 自实现 ICommand）
    // 对照 WPF System.Windows.Input.RoutedCommand：
    //   - WPF RoutedCommand 有 CommandManager + InputGestures 复杂逻辑
    //   - spike 简化为 ICommand 空实现（CanExecute 永远返回 true，Execute 空操作）
    public class RoutedCommand : ICommand
    {
        // 对照 WPF: public event EventHandler CanExecuteChanged
        public event EventHandler CanExecuteChanged;

        // 对照 WPF: public bool CanExecute(object parameter)
        public bool CanExecute(object parameter) => true;

        // 对照 WPF: public void Execute(object parameter)
        public void Execute(object parameter) { }
    }
}
