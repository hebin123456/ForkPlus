using System.Diagnostics;

// Avalonia spike 版 BindingErrorTraceListener（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/BindingErrorTraceListener.cs（33 行）：
//   - WPF: public class BindingErrorTraceListener : TraceListener
//   - 用 StringBuilder 累积 Write/WriteLine 消息，整条 trim 后 Debug.WriteLine + Log.Warn
//   - 由 WPF 启动时挂到 PresentationTraceSources.DataBindingSource.Listeners
//     （监听 System.Windows.Data 的绑定错误输出）
//
// Avalonia 版差异（task spec：Avalonia 无绑定错误追踪，创建空类 + 注释）：
//   1. WPF 绑定引擎走 PresentationTraceSources（Avalonia 无等价物），spike 不挂载
//   2. Write/WriteLine 保留为空实现（TraceListener 抽象方法必须 override），
//      不再累积/输出消息（spike 不消费绑定错误）
//   3. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：保留 TraceListener 子类骨架，方法体置空。
namespace ForkPlus.Avalonia
{
	public class BindingErrorTraceListener : TraceListener
	{
		public override void Write(string message)
		{
			// spike 版：Avalonia 无 WPF 绑定错误追踪机制，空实现占位
		}

		public override void WriteLine(string message)
		{
			// spike 版：Avalonia 无 WPF 绑定错误追踪机制，空实现占位
		}
	}
}
