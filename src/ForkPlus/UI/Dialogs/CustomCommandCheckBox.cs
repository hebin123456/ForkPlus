using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.CustomCommands;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class CustomCommandCheckBox : CheckBox
	{
		private CustomCommandUI.Control.CheckBox _checkBox;

		public string CheckedValue => _checkBox.CheckedValue;

		public string UncheckedValue => _checkBox.UncheckedValue;

		public CustomCommandCheckBox(CustomCommandUI.Control.CheckBox checkBox)
		{
			// Migration note：WPF SetResourceReference(StyleProperty, type) 隐式样式已由 Avalonia ControlTheme 接管，移除调用。;
			_checkBox = checkBox;
			base.Content = checkBox.Title;
			base.IsChecked = checkBox.DefaultValue;
		}
	}
}
