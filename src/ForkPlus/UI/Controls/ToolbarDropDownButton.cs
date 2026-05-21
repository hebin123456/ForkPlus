using System.Windows;

namespace ForkPlus.UI.Controls
{
	public class ToolbarDropDownButton : DropDownButton
	{
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(ToolbarDropDownButton), new PropertyMetadata(null));

		public static readonly DependencyProperty IsArrowVisibleProperty = DependencyProperty.Register("IsArrowVisible", typeof(bool), typeof(ToolbarDropDownButton), new PropertyMetadata(true));

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

		public bool IsArrowVisible
		{
			get
			{
				return (bool)GetValue(IsArrowVisibleProperty);
			}
			set
			{
				SetValue(IsArrowVisibleProperty, value);
			}
		}
	}
}
