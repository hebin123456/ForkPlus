using System;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.1：IDispatcher 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfDispatcher.cs：
    //   WPF: 构造注入 System.Windows.Threading.Dispatcher 实例，
    //   Post = BeginInvoke(action)（异步排队到 UI 队列），
    //   Invoke = Invoke(action)（同步阻塞直到 UI 线程执行完）。
    //
    // Avalonia 11 实现：
    //   - 用 Avalonia.Threading.Dispatcher.UIThread 静态属性（单例，无需构造注入）
    //   - Post = Dispatcher.UIThread.Post(action)（异步排队，等价 WPF BeginInvoke）
    //   - Invoke = Dispatcher.UIThread.Invoke(action)（同步阻塞，等价 WPF Invoke）
    //
    // 注：Dispatcher.UIThread 是 Avalonia 全局 UI 线程调度器，与 WPF Application.Current.Dispatcher
    // 语义一致。无需在 App 启动时注入实例（WPF 版从主窗口取 Dispatcher 注入）。
    //
    // 注意：Avalonia.Threading 命名空间也定义了 IDispatcher 接口（与 Core 的
    // ForkPlus.Services.IDispatcher 同名冲突），故类声明用完全限定名消除歧义。
    public class AvaloniaDispatcher : ForkPlus.Services.IDispatcher
    {
        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Dispatcher.UIThread.Post(action);
        }

        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Dispatcher.UIThread.Invoke(action);
        }
    }
}
