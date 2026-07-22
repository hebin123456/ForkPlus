using System;
// Avalonia spike 版 RepositoryStatusManager（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/RepositoryStatusManager.cs（98 行）：
//   - WPF: internal class RepositoryStatusManager
//   - 用 DelayedAction<object> 延迟 3 秒触发 RefreshInternal
//   - UpdateRequired 依据 ForkPlusSettings.AutomaticStatusUpdateInterval
//   - RefreshInternal：遍历 TabManager.RepositoryUserControls，
//     对非活动 tab 的 GitModule 调 IsRepositoryDirtyGitCommand，
//     通过 dispatcher.Async 更新 IsDirtyState
//   - 依赖：MainWindow.ActiveRepositoryUserControl / Application.Current.TabManager() /
//     MainWindow.Instance.JobQueue / GitModule / IsRepositoryDirtyGitCommand
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF DelayedAction<object> → spike 用 System.Threading.Timer（最简延迟）
//   2. WPF DispatcherTimer / dispatcher.Async → spike 跳过（无真实 GitModule 调用）
//   3. WPF MainWindow.ActiveRepositoryUserControl → spike 永远 null（spike 单 tab）
//      → RefreshInternal 直接 return（spike 不实际刷新状态）
//   4. WPF MainWindow.Instance.JobQueue.Add → spike 跳过（JobQueue 为 null 占位）
//   5. WPF IsRepositoryDirtyGitCommand.Execute(gitModule) → spike 跳过（无 Git 依赖）
//
// spike 简化（task spec 关键 API）：
//   - public void Refresh()（spike 占位，不实际执行状态刷新）
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    internal class RepositoryStatusManager
    {
        private DateTime _lastUpdateTime = DateTime.Today.AddDays(-1.0);
        private bool _isRefreshing;

        // spike 版：无 DelayedAction 依赖，Refresh 直接同步占位
        public RepositoryStatusManager()
        {
        }

        // 对照 WPF: public void Refresh() — if (UpdateRequired) _refreshAction.InvokeWithDelay(null);
        // spike 版：占位（不实际触发延迟刷新）
        public void Refresh()
        {
            // spike 版跳过：UpdateRequired 永远 false（spike 不读 ForkPlusSettings）
            // 真实刷新留待 Phase 4.x 后期接入 GitModule + JobQueue
            Console.WriteLine("[RepositoryStatusManager] Refresh (spike skipped)");
        }
    }
}
