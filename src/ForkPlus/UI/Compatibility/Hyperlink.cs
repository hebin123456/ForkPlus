// 阶段 5：Avalonia 11.3 标准包无 Hyperlink 类（WPF System.Windows.Documents.Hyperlink）。
// Avalonia 的等价控件是 HyperlinkButton，但它继承 Button 而非 Span，不能用于 InlineCollection。
// 富文本内的可点击超链接需要 Avalonia Pro（付费）的 RichHyperlink。
//
// 本兼容类继承 Avalonia Span（Inline），提供 NavigateUri 属性 + Click 事件，
// 让 WPF 迁移代码中 `new Hyperlink()` / `<Hyperlink>` / `hyperlink.Click += ...`
// 等写法无需修改即可通过编译并运行。
//
// 放在命名空间 Avalonia.Controls.Documents：与 WPF System.Windows.Documents.Hyperlink
// 命名空间风格一致，现有 `using Avalonia.Controls.Documents;` 直接解析到本类型。
using System;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using ForkPlus.UI;

namespace Avalonia.Controls.Documents
{
	/// <summary>
	/// WPF Hyperlink 的 Avalonia 兼容占位。继承 Span 使其可作为 Inline 加入 InlineCollection。
	/// 点击时通过 Click 事件通知宿主，由宿主调用 OpenInBrowser 等完成导航。
	/// </summary>
	public class Hyperlink : Span
	{
		/// <summary>导航目标 URI。点击时由宿主读取并打开。</summary>
		public Uri NavigateUri { get; set; }

		/// <summary>WPF Hyperlink.RequestNavigate 的兼容事件。点击时触发。</summary>
		public event EventHandler<RequestNavigateEventArgs> RequestNavigate;

		/// <summary>WPF Hyperlink.Click 的兼容事件（Avalonia Span 无 Click，本类模拟）。</summary>
		public event EventHandler<RoutedEventArgs> Click;

		static Hyperlink()
		{
			// Avalonia 需要注册伪类才能在样式中用 :pointerover 等选择器。
		}

		public Hyperlink()
		{
		}

		public Hyperlink(Inline inline)
		{
			if (inline != null)
			{
				Inlines.Add(inline);
			}
		}

		/// <summary>模拟点击：触发 Click 与 RequestNavigate 事件。</summary>
		public void RaiseClick()
		{
			var clickArgs = new RoutedEventArgs();
			Click?.Invoke(this, clickArgs);
			if (!clickArgs.Handled && NavigateUri != null)
			{
				var navArgs = new RequestNavigateEventArgs { Uri = NavigateUri };
				RequestNavigate?.Invoke(this, navArgs);
			}
		}
	}
}
