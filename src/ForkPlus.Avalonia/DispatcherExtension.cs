using System;
using Avalonia.Threading;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/DispatcherExtension.cs（17 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - Async(this Dispatcher dispatcher, Action action) → dispatcher.BeginInvoke(action) 返回 DispatcherOperation
    //   - Sync(this Dispatcher dispatcher, Action action) → dispatcher.Invoke(action) 同步执行
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. Dispatcher → Avalonia.Threading.Dispatcher（spike 规范）
    //   2. Dispatcher.BeginInvoke → Dispatcher.Post（Avalonia 11 等价的 fire-and-forget 异步派发）
    //      WPF 返回 DispatcherOperation，Avalonia Post 返回 void（spike 版 Async 返回 void）
    //   3. Dispatcher.Invoke → Dispatcher.Invoke（Avalonia 11 Dispatcher 实例同样有同步 Invoke）
    //      spike 规范提示 Dispatcher.Invoke → Dispatcher.UIThread.Post，
    //      但 Sync 语义为同步执行，spike 版保留 Dispatcher.Invoke 保证语义一致
    public static class DispatcherExtension
    {
        public static void Async(this Dispatcher dispatcher, Action action)
        {
            dispatcher.Post(action);
        }

        public static void Sync(this Dispatcher dispatcher, Action action)
        {
            dispatcher.Invoke(action);
        }
    }
}
