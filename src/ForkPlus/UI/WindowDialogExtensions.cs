// 阶段 5：WPF → Avalonia 11 迁移辅助。
//
// WPF 的 Window.ShowDialog() 是同步方法，返回 bool?（DialogResult）。
// 大量调用站点形如：if (new FooWindow().ShowDialog().GetValueOrDefault()) { ... }
// 或：new ErrorWindow(...).ShowDialog();
//
// Avalonia 11 的 Window.ShowDialog(Window owner) 是异步方法，返回 Task / Task<TResult>，
// 且必须传入 owner 窗口（无 0 参重载）。直接迁移需把 100+ 文件、~200 处调用全部改造为 async/await，
// 工作量过大且会破坏同步控制流（很多在构造函数/命令 Execute 中调用）。
//
// 本扩展通过 DispatcherFrame 嵌套消息泵实现 WPF 风格的同步阻塞语义：
//   1. 自动选取活动窗口作为 owner（无需调用方传入）
//   2. 启动 Avalonia 异步 ShowDialog<bool?>(owner)
//   3. 推入嵌套 DispatcherFrame 阻塞当前调用，同时持续泵送平台消息
//      （Win32 PeekMessage/GetMessage、X11 XNextEvent、macOS CFRunLoop），
//      UI 事件正常处理，对话框可点击交互，与 WPF.ShowDialog 行为一致。
//   4. 对话框关闭后 frame.Continue=false，控制流回到调用方，返回 bool? 结果。
//
// 对话框结果映射（ForkPlusDialogWindow 基类已统一）：
//   - Close(true)        → OnSubmit / CloseWithOk / Close(GitCommandResult) → true
//   - Close(false)       → OnCancel / Escape 键 → false
//   - Close() / X 按钮   → null（未明确提交）
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	public static class WindowDialogExtensions
	{
		/// <summary>
		/// WPF 兼容的同步模态对话框显示。阻塞调用线程直到对话框关闭，
		/// 期间通过 DispatcherFrame 持续泵送 UI 消息，保证对话框可交互。
		/// </summary>
		/// <returns>
		/// true = 用户提交（Submit 按钮 / OnSubmit）；false = 用户取消（Cancel 按钮 / Escape）；
		/// null = 无 owner 退化为非模态，或对话框异常关闭。
		/// </returns>
		public static bool? ShowDialog(this Window window)
		{
			Window owner = GetActiveOwner(window);
			if (owner == null)
			{
				// 启动早期或无可见宿主窗口：退化为非模态显示，立即返回 null。
				// 调用方若依赖 bool? 结果会得到 null，GetValueOrDefault() 转 false，符合"未提交"语义。
				window.Show();
				return null;
			}

			bool? result = null;
			var frame = new DispatcherFrame();

			// 异步启动 ShowDialog；完成时把结果写回 result 并退出嵌套帧。
			// async void 用于 fire-and-forget：异常通过 try/catch 兜底，避免未观察异常崩溃。
			async void RunDialog()
			{
				try
				{
					result = await window.ShowDialog<bool?>(owner);
				}
				catch
				{
					result = null;
				}
				finally
				{
					frame.Continue = false;
				}
			}

			RunDialog();

			// 推入嵌套 Dispatcher 帧：阻塞当前调用直到 frame.Continue=false，
			// 同时持续泵送平台消息队列，UI 事件正常派发（与 WPF Dispatcher.PushFrame 等价）。
			Dispatcher.UIThread.PushFrame(frame);
			return result;
		}

		/// <summary>
		/// 选取当前活动窗口作为 owner；若无可用的，回退到 MainWindow 或任意可见窗口。
		/// </summary>
		private static Window GetActiveOwner(Window exclude)
		{
			if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				// 1. 当前活动窗口（且非自身）
				Window active = desktop.Windows.FirstOrDefault(w => w.IsActive && w != exclude);
				if (active != null)
				{
					return active;
				}
				// 2. MainWindow（若可见）
				if (desktop.MainWindow != null && desktop.MainWindow != exclude && desktop.MainWindow.IsVisible)
				{
					return desktop.MainWindow;
				}
				// 3. 任意可见窗口
				return desktop.Windows.FirstOrDefault(w => w != exclude && w.IsVisible);
			}
			return null;
		}
	}
}
