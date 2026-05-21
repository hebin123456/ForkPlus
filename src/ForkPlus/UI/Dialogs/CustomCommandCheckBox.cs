using System.Windows;
using System.Windows.Controls;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.Dialogs
{
	public class CustomCommandCheckBox : CheckBox
	{
		private CustomCommandUI.Control.CheckBox _checkBox;

		public string CheckedValue => _checkBox.CheckedValue;

		public string UncheckedValue => _checkBox.UncheckedValue;

		public CustomCommandCheckBox(CustomCommandUI.Control.CheckBox checkBox)
		{
			SetResourceReference(FrameworkElement.StyleProperty, typeof(CheckBox));
			_checkBox = checkBox;
			base.Content = checkBox.Title;
			base.IsChecked = checkBox.DefaultValue;
		}
	}
}
