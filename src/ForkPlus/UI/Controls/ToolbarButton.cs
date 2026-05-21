using System.Windows;
using System.Windows.Controls;

namespace ForkPlus.UI.Controls
{
	public class ToolbarButton : Button
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ToolbarButton), new PropertyMetadata(null));

		public string Title
		{
			get
			{
				return (string)GetValue(TitleProperty);
			}
			set
			{
				SetValue(TitleProperty, value);
			}
		}
	}
}
