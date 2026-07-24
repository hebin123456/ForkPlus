using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
				// 阶段 4.5：WPF SnapsToDevicePixels → Avalonia UseLayoutRounding（最接近的像素对齐语义）。
				UseLayoutRounding = icon.UseLayoutRounding
			};
		}
	}
}
