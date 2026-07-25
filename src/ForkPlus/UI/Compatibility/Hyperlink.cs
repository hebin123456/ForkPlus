// 阶段 4.5：Avalonia 11.3 核心包不含 Avalonia.Controls.Documents.Hyperlink
// （仅在 Avalonia Pro RichTextEditor 中存在）。此处提供桥接类，
// 继承 Span 并暴露 Click 路由事件，使 CommandHyperlink 等代码无需改动。
using System.Windows.Input;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;

namespace Avalonia.Controls.Documents
{
	public class Hyperlink : Span
	{
		public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
			RoutedEvent.Register<Hyperlink, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

		public event EventHandler<RoutedEventArgs> Click
		{
			add => AddHandler(ClickEvent, value);
			remove => RemoveHandler(ClickEvent, value);
		}

		[Null]
		public Uri NavigateUri { get; set; }

		[Null]
		public ICommand Command { get; set; }

		[Null]
		public object CommandParameter { get; set; }

		protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			if (!e.Handled)
			{
				RaiseEvent(new RoutedEventArgs(ClickEvent));
				if (Command?.CanExecute(CommandParameter) == true)
				{
					Command.Execute(CommandParameter);
				}
			}
		}
	}
}
