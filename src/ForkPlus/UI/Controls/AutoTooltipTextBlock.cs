using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	public class AutoTooltipTextBlock : TextBlock
	{
		public static readonly DependencyProperty CustomToolTipProperty = DependencyProperty.Register("CustomToolTip", typeof(string), typeof(AutoTooltipTextBlock), new PropertyMetadata((object)null));

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
			base.ToolTip = "";
			base.ToolTipOpening += delegate(object s, ToolTipEventArgs e)
			{
				if (CustomToolTip != null)
				{
					base.ToolTip = CustomToolTip;
				}
				else if (TextIsTrimmed())
				{
					base.ToolTip = base.Text;
				}
				else
				{
					e.Handled = true;
				}
			};
		}

		private bool TextIsTrimmed()
		{
			Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return base.ActualWidth < base.DesiredSize.Width;
		}
	}
}
