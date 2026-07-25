// ⚠ 临时桥接类型 ─ 阶段 4.5 编译过渡用。
// Avalonia 11.3.18 移除了独立的 PasswordBox 控件，推荐用 TextBox + PasswordChar 替代。
// 本桥接类继承 TextBox，暴露 WPF 兼容的 .Password 属性和 .PasswordChanged 事件，
// 让原 WPF 代码无需修改即可通过编译。
//
// XAML 中 <PasswordBox x:Name="..."> 会解析到本类型（AvaloniaXaml 命名空间解析
// PasswordBox 标签到 Avalonia.Controls.PasswordBox，由于本类在该命名空间，会被命中）。
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
	/// <summary>
	/// WPF PasswordBox 兼容桥接类。继承自 Avalonia TextBox，
	/// 在构造时设置 PasswordChar 为 '●'，并暴露 .Password 属性和 .PasswordChanged 事件。
	/// </summary>
	public class PasswordBox : TextBox
	{
		/// <summary>WPF PasswordBox.PasswordChanged 兼容事件。</summary>
		public static readonly RoutedEvent<RoutedEventArgs> PasswordChangedEvent =
			RoutedEvent.Register<PasswordBox, RoutedEventArgs>(nameof(PasswordChanged), RoutingStrategies.Bubble);

		public event EventHandler<RoutedEventArgs> PasswordChanged
		{
			add => AddHandler(PasswordChangedEvent, value);
			remove => RemoveHandler(PasswordChangedEvent, value);
		}

		public PasswordBox()
		{
			PasswordChar = '●';
			TextChanged += (s, e) => RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));
		}

		/// <summary>WPF PasswordBox.Password 兼容属性（等价于 TextBox.Text）。</summary>
		public string Password
		{
			get => Text;
			set => Text = value;
		}
	}
}
