using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class PlaceholderTextBox : TextBox
	{
		public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(PlaceholderTextBox));

		public static readonly DependencyProperty IconProperty = DependencyProperty.Register("Icon", typeof(ImageSource), typeof(PlaceholderTextBox));

		public string Placeholder
		{
			get
			{
				return (string)GetValue(PlaceholderProperty);
			}
			set
			{
				SetValue(PlaceholderProperty, value);
			}
		}

		public ImageSource Icon
		{
			get
			{
				return (ImageSource)GetValue(IconProperty);
			}
			set
			{
				SetValue(IconProperty, value);
			}
		}

		public PlaceholderTextBox()
		{
			base.Loaded += delegate
			{
				base.ContextMenu = GetContextMenu();
			};
		}

		protected virtual ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			return contextMenu;
		}
	}
}
