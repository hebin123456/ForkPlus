// 阶段 4.5：WPF → Avalonia 迁移。
// WPF SetResourceReference(FrameworkElement.StyleProperty, typeof(CheckBox))
// → Avalonia StyledElement.StyleKeyOverride（让派生控件复用基类 CheckBox 的默认样式）。
using System;
using Avalonia.Controls;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.Dialogs
{
	public class CustomCommandCheckBox : CheckBox
	{
		private CustomCommandUI.Control.CheckBox _checkBox;

		public string CheckedValue => _checkBox.CheckedValue;

		public string UncheckedValue => _checkBox.UncheckedValue;

		protected override Type StyleKeyOverride => typeof(CheckBox);

		public CustomCommandCheckBox(CustomCommandUI.Control.CheckBox checkBox)
		{
			_checkBox = checkBox;
			base.Content = checkBox.Title;
			base.IsChecked = checkBox.DefaultValue;
		}
	}
}
