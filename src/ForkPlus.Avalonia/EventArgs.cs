using System;

// Avalonia spike 版 EventArgs<T>（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/EventArgs.cs（13 行）：
//   - WPF: public class EventArgs<T> : EventArgs
//   - 单只读字段 Value + 构造函数
//   - 无 WPF 依赖，纯 BCL 类型
//
// Avalonia 版差异：
//   1. 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//
// spike 简化：与 WPF 完全一致的泛型 EventArgs。
namespace ForkPlus.Avalonia
{
	public class EventArgs<T> : EventArgs
	{
		public T Value { get; }

		public EventArgs(T value)
		{
			Value = value;
		}
	}
}
