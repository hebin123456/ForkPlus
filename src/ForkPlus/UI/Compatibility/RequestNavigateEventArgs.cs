// ⚠ 临时桥接类型 ─ 阶段 4.5 编译过渡用。
// WPF System.Windows.Navigation.RequestNavigateEventArgs（Hyperlink.RequestNavigate 事件参数）
// 在 Avalonia 中无对应：Avalonia Hyperlink 不触发 RequestNavigate，需用 Click/Command。
//
// 此类仅让 code-behind 的 Hyperlink_RequestNavigate(object, RequestNavigateEventArgs) 签名
// 通过编译。真正的迁移（阶段 4 XAML 收尾）会把 XAML 的 RequestNavigate="H" 改为 Click="H"
// 并通过 CommandParameter 传递 Uri，届时删除本文件。
using System;
using Avalonia.Interactivity;

namespace ForkPlus.UI
{
	/// <summary>WPF RequestNavigateEventArgs 的 Avalonia 兼容占位。</summary>
	public class RequestNavigateEventArgs : RoutedEventArgs
	{
		/// <summary>导航目标 URI（运行时需由真实事件填充，当前桥接默认 null）。</summary>
		public Uri Uri { get; set; }
	}
}
