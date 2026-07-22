using System;
using System.Windows.Input;

namespace ForkPlus.Avalonia.Views
{
    // Phase 4.0：Avalonia 版 MainWindowCommands（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWindowCommands.cs（273 行）：
    //   - WPF: public class MainWindowCommands : CommandContainer
    //   - 60+ 个命令字段，每个通过 CommandContainer.Lazy(ref _field) 懒加载
    //   - 命令类型来自 ForkPlus.UI.Commands.*（每个独立 .cs 文件）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF CommandContainer + 60+ 个 IForkPlusCommand 实现 → spike 用 RoutedCommand 空壳替代
    //   2. spike 不引入 ForkPlus.UI.Commands.* 完整命令树（每个 200-300 行，依赖大量 WPF-only 类型）
    //   3. 保留 60+ 个命令属性名，调用方代码（MainWindow.InitializeKeyBindings /
    //      MainWindowMenuManager.CreateXxxMenuItems）的代码模式不变
    //   4. spike 版每个命令是 RoutedCommand 实例，Execute/CanExecute 由外部 CommandBinding 注册
    //
    // spike 简化（task spec 关键 API）：
    //   - 静态类 + RoutedCommand 替代（避免引入 CommandContainer + 60+ 命令实现）
    //   - 60+ 个 public RoutedCommand 属性，按 WPF 命名一致
    public class MainWindowCommands
    {
        // 对照 WPF: 60+ 个 IForkPlusCommand 字段 → spike 版统一用 RoutedCommand 空壳
        // 注意：spike 版用 System.Windows.Input.ICommand 接口（Avalonia 也用此接口）
        // 替代 WPF 的 RoutedCommand（Avalonia 不需要 CommandBinding/CommandManager 全局表）
        public ICommand ActivateCommitView { get; } = new RelayCommand();
        public ICommand ActivateRevisionList { get; } = new RelayCommand();
        public ICommand ActivateRepositoryTab { get; } = new RelayCommand();
        public ICommand ActivateSearchTab { get; } = new RelayCommand();
        public ICommand ShowHead { get; } = new RelayCommand();
        public ICommand ShowDebugUpdateWindow { get; } = new RelayCommand();
        public ICommand UpdateApplication { get; } = new RelayCommand();
        public ICommand CloseActiveTab { get; } = new RelayCommand();
        public ICommand ExitApplication { get; } = new RelayCommand();
        public ICommand NewTab { get; } = new RelayCommand();
        public ICommand OpenRepository { get; } = new RelayCommand();
        public ICommand RefreshRepositoryData { get; } = new RelayCommand();
        public ICommand ShowAskPassWindow { get; } = new RelayCommand();
        public ICommand ShowCloneWindow { get; } = new RelayCommand();
        public ICommand ShowInitGitMmRepositoryWindow { get; } = new RelayCommand();
        public ICommand ShowCreateWorktreeWindow { get; } = new RelayCommand();
        public ICommand ShowCheckoutBranchAsWorktreeWindow { get; } = new RelayCommand();
        public ICommand ShowBenchmarkWindow { get; } = new RelayCommand();
        public ICommand ShowCreateBranchWindow { get; } = new RelayCommand();
        public ICommand ShowCreateRepositoryWindow { get; } = new RelayCommand();
        public ICommand ShowCreateTagWindow { get; } = new RelayCommand();
        public ICommand CopyRevisionSha { get; } = new RelayCommand();
        public ICommand CopyRevisionInfo { get; } = new RelayCommand();
        public ICommand ShowFetchWindow { get; } = new RelayCommand();
        public ICommand ShowConfigureSSHKeysWindow { get; } = new RelayCommand();
        public ICommand ShowConfigureWorkspacesWindow { get; } = new RelayCommand();
        public ICommand SendCrashReport { get; } = new RelayCommand();
        public ICommand ShowQuickLaunchWindow { get; } = new RelayCommand();
        public ICommand ShowQuickLaunchCheckoutWindow { get; } = new RelayCommand();
        public ICommand ShowPreferencesWindow { get; } = new RelayCommand();
        public ICommand ShowAccountsWindow { get; } = new RelayCommand();
        public ICommand ShowPullWindow { get; } = new RelayCommand();
        public ICommand ShowPushWindow { get; } = new RelayCommand();
        public ICommand QuickPush { get; } = new RelayCommand();
        public ICommand QuickFetch { get; } = new RelayCommand();
        public ICommand QuickPull { get; } = new RelayCommand();
        public ICommand SelectPreviousTab { get; } = new RelayCommand();
        public ICommand SelectNextTab { get; } = new RelayCommand();
        public ICommand SwitchApplicationTheme { get; } = new RelayCommand();
        public ICommand SwitchWorkspace { get; } = new RelayCommand();
        public ICommand SwitchRevisionListOrientation { get; } = new RelayCommand();
        public ICommand OpenRepositoryInShellTool { get; } = new RelayCommand();
        public ICommand OpenRepositoryInDefaultShellTool { get; } = new RelayCommand();
        public ICommand OpenRepositoryInFileExplorer { get; } = new RelayCommand();
        public ICommand OpenRepositoryInExternalEditor { get; } = new RelayCommand();
        public ICommand OpenUrl { get; } = new RelayCommand();
        public ICommand ToggleShowReflogInRevisionList { get; } = new RelayCommand();
        public ICommand ToggleCollapseAllMergeRevisions { get; } = new RelayCommand();
        public ICommand ToggleHideTags { get; } = new RelayCommand();
        public ICommand ToggleHideStashesInRevisionList { get; } = new RelayCommand();
        public ICommand ToggleReferenceFilter { get; } = new RelayCommand();
        public ICommand IncreaseLayoutScale { get; } = new RelayCommand();
        public ICommand DecreaseLayoutScale { get; } = new RelayCommand();
        public ICommand ToggleRefreshOnActivate { get; } = new RelayCommand();
        public ICommand ToggleTraceElapsedTime { get; } = new RelayCommand();
        public ICommand OpenApplicationDataDirectory { get; } = new RelayCommand();
        public ICommand ShowAboutWindow { get; } = new RelayCommand();
        public ICommand OpenForkTwitter { get; } = new RelayCommand();
        public ICommand OpenForkWebsite { get; } = new RelayCommand();
        public ICommand OpenIssueTracker { get; } = new RelayCommand();
        public ICommand OpenKeyboardShortcuts { get; } = new RelayCommand();
        public ICommand OpenReleaseNotes { get; } = new RelayCommand();
        public ICommand ShowPerformanceDiagnosticsWindow { get; } = new RelayCommand();
        public ICommand ShowSaveStashWindow { get; } = new RelayCommand();
        public ICommand Undo { get; } = new RelayCommand();
        public ICommand Redo { get; } = new RelayCommand();
    }

    // spike 版：简单的 ICommand 空壳实现（CanExecute 永远 true，Execute 不做任何事）
    // 对照 WPF CommandContainer.Lazy(ref _field) → IForkPlusCommand 实现
    // spike 版用 RelayCommand 空壳替代（避免引入完整命令树）
    public class RelayCommand : ICommand
    {
        public event EventHandler CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            // spike 版空实现：实际命令逻辑由调用方在外部 CommandBinding 中注册
            Console.WriteLine("[RelayCommand] Execute (spike no-op)");
        }
    }
}
