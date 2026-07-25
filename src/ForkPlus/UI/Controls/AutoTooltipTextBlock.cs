using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace ForkPlus.UI.Controls
{
	public class AutoTooltipTextBlock : TextBlock
	{
		public static readonly StyledProperty<string> CustomToolTipProperty =
			AvaloniaProperty.Register<AutoTooltipTextBlock, string>(nameof(CustomToolTip));

		public string CustomToolTip
		{
			get => GetValue(CustomToolTipProperty);
			set => SetValue(CustomToolTipProperty, value);
		}

		public AutoTooltipTextBlock()
		{
			TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
			ToolTip.SetTip(this, "");
			// 阶段 4.5：WPF ToolTipOpening 事件 → Avalonia PointerEntered（Avalonia 无 ToolTipOpening 事件）。
			// 阶段 5：TextBlock 无 PointerEntered CLR 事件，改用 AddHandler(InputElement.PointerEnteredEvent, ...) 订阅路由事件。
			// 注意：Avalonia 11 用 PointerEnteredEvent（带 "ed"），不是 WPF 的 PointerEnterEvent。
			AddHandler(InputElement.PointerEnteredEvent, AutoTooltipTextBlock_PointerEnter);
		}

		private void AutoTooltipTextBlock_PointerEnter(object sender, PointerEventArgs e)
		{
			if (CustomToolTip != null)
			{
				ToolTip.SetTip(this, CustomToolTip);
			}
			else if (TextIsTrimmed())
			{
				ToolTip.SetTip(this, Text);
			}
			else
			{
				// 文本未截断且无自定义提示：清除提示以抑制显示（等效 WPF e.Handled = true）。
				ToolTip.SetTip(this, null);
			}
		}

		private bool TextIsTrimmed()
		{
			Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return Bounds.Width < DesiredSize.Width;
		}
	}
}
