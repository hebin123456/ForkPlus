using System;
using Avalonia.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;

namespace ForkPlus.UI.Controls
{
	public class ToggleMenuItem : MenuItem
	{
		protected override Type StyleKeyOverride => typeof(MenuItem);

		public ToggleMenuItem(string title, Action clickHandler, bool isChecked, Image icon = null)
		{
			base.Header = PreferencesLocalization.Current(title);
			base.IsChecked = isChecked;
			base.ToggleType = MenuItemToggleType.CheckBox;
			base.Icon = CloneIcon(icon);
			base.Click += delegate
			{
				clickHandler?.Invoke();
			};
		}

		private static Image CloneIcon(Image icon)
		{
			if (icon == null)
			{
				return null;
			}
			return new Image
			{
				Source = icon.Source,
				Width = icon.Width,
				Height = icon.Height,
				Margin = icon.Margin,
				Stretch = icon.Stretch,
				HorizontalAlignment = icon.HorizontalAlignment,
				VerticalAlignment = icon.VerticalAlignment
				// Migration note：WPF Image.SnapsToDevicePixels 在 Avalonia 无对应（位图渲染由布局取整保证），移除。
			};
		}
	}
}
