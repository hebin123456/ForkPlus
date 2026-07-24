using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class PlaceholderTextBox : TextBox
	{
		public static readonly StyledProperty<string> PlaceholderProperty =
			AvaloniaProperty.Register<PlaceholderTextBox, string>(nameof(Placeholder));

		// 阶段 4.5：WPF ImageSource → Avalonia IImage。
		public static readonly StyledProperty<IImage> IconProperty =
			AvaloniaProperty.Register<PlaceholderTextBox, IImage>(nameof(Icon));

		public string Placeholder
		{
			get => GetValue(PlaceholderProperty);
			set => SetValue(PlaceholderProperty, value);
		}

		public IImage Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
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
