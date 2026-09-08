using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class AutoTooltipTextBlock : TextBlock
	{
		public static readonly global::Avalonia.StyledProperty<string> CustomToolTipProperty =
    global::Avalonia.AvaloniaProperty.Register<AutoTooltipTextBlock, string>("CustomToolTip");

		public string CustomToolTip
		{
			get
			{
				return (string)GetValue(CustomToolTipProperty);
			}
			set
			{
				SetValue(CustomToolTipProperty, value);
			}
		}

		public AutoTooltipTextBlock()
		{
			base.TextTrimming = TextTrimming.CharacterEllipsis;
			global::Avalonia.Controls.ToolTip.SetTip(this, ""); // Migration note：base 不能作为独立参数，改 this（同一对象引用）
			// Migration note：WPF FrameworkElement.ToolTipOpening 事件 → Avalonia ToolTip.ToolTipOpeningEvent 路由事件订阅。
			// （lambda 内不可用 base 关键字，改用 this。）
			global::Avalonia.Controls.ToolTip.AddToolTipOpeningHandler(this, (s, e) =>
			{
				if (CustomToolTip != null)
				{
					global::Avalonia.Controls.ToolTip.SetTip(this, CustomToolTip);
				}
				else if (TextIsTrimmed())
				{
					global::Avalonia.Controls.ToolTip.SetTip(this, this.Text);
				}
				else
				{
					// Migration note（2026-09-03，"暂存区文件鼠标覆盖有空的 tips"根因）：
					// WPF 里 ToolTipOpening 事件 e.Handled = true 即取消弹窗；Avalonia 12 的
					// ToolTip.IsOpenChanged 只检查 CancelRoutedEventArgs.Cancel，Handled 无效。
					// 只置 Handled 时事件不取消，构造函数预置的空字符串 Tip 照常弹出 → 空 tooltip 框。
					// 对齐 WPF 行为：无可显示内容时必须 Cancel 才能不显示。
					e.Cancel = true;
					e.Handled = true;
				}
			});
		}

		private bool TextIsTrimmed()
		{
			Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return base.Bounds.Width < base.DesiredSize.Width;
		}
	}
}
