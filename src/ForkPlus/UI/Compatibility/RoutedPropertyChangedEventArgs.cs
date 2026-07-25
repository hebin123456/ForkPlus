// 阶段 4.5：WPF RoutedPropertyChangedEventArgs<T> 桥接。
// Avalonia 无此类型，此处提供兼容占位使事件处理方法签名通过编译。
using System;
using Avalonia.Interactivity;

namespace Avalonia.Interactivity
{
	public class RoutedPropertyChangedEventArgs<T> : RoutedEventArgs
	{
		public T OldValue { get; set; }

		public T NewValue { get; set; }
	}
}
