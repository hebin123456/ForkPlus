using System;
using System.Windows.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class ToggleMenuItem : MenuItem
	{
		public ToggleMenuItem(string title, Action clickHandler, bool isChecked, Image icon = null)
		{
			base.Header = PreferencesLocalization.Current(title);
			base.IsChecked = isChecked;
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
				VerticalAlignment = icon.VerticalAlignment,
				SnapsToDevicePixels = icon.SnapsToDevicePixels
			};
		}
	}
}
