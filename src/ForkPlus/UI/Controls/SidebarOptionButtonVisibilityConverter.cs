using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class SidebarOptionButtonVisibilityConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			bool isPointerOver = values.Count > 0 && values[0] is bool pointerOver && pointerOver;
			if (isPointerOver)
			{
				return true;
			}

			string option = parameter as string;
			if (string.Equals(option, "Pin", StringComparison.OrdinalIgnoreCase))
			{
				return values.Count > 1 && values[1] is bool pinned && pinned;
			}

			if (values.Count <= 1 || values[1] is not ReferenceFilterState state)
			{
				return false;
			}

			if (string.Equals(option, "Filter", StringComparison.OrdinalIgnoreCase))
			{
				return state == ReferenceFilterState.Filter || state == ReferenceFilterState.InheritedFilter;
			}

			if (string.Equals(option, "Hide", StringComparison.OrdinalIgnoreCase))
			{
				return state == ReferenceFilterState.Hide || state == ReferenceFilterState.InheritedHide;
			}

			return false;
		}
	}

	public class SidebarOptionButtonIconConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			string option = parameter as string;
			bool isPointerOver = values.Count > 0 && values[0] is bool pointerOver && pointerOver;

			if (string.Equals(option, "Pin", StringComparison.OrdinalIgnoreCase))
			{
				bool pinned = values.Count > 1 && values[1] is bool p && p;
				return FindResource(pinned ? "PinOnIcon" : "PinOffIcon");
			}

			if (values.Count <= 1 || values[1] is not ReferenceFilterState state)
			{
				return AvaloniaProperty.UnsetValue;
			}

			if (string.Equals(option, "Filter", StringComparison.OrdinalIgnoreCase))
			{
				bool filtered = state == ReferenceFilterState.Filter || state == ReferenceFilterState.InheritedFilter;
				return FindResource(filtered ? "BranchFilterOnIcon" : "BranchFilterOffIcon");
			}

			if (string.Equals(option, "Hide", StringComparison.OrdinalIgnoreCase))
			{
				bool hidden = state == ReferenceFilterState.Hide || state == ReferenceFilterState.InheritedHide;
				return FindResource(hidden ? "HideBranchOnIcon" : "HideBranchOffIcon");
			}

			if (string.Equals(option, "Search", StringComparison.OrdinalIgnoreCase))
			{
				bool hidden = state == ReferenceFilterState.Hide || state == ReferenceFilterState.InheritedHide;
				return FindResource(hidden ? "SearchOnIcon" : "SearchOffIcon");
			}

			return isPointerOver ? AvaloniaProperty.UnsetValue : AvaloniaProperty.UnsetValue;
		}

		private static object FindResource(string key)
		{
			Avalonia.Application app = Avalonia.Application.Current;
			if (app != null && app.TryGetResource(key, app.ActualThemeVariant ?? ThemeVariant.Default, out object value))
			{
				return value;
			}
			return AvaloniaProperty.UnsetValue;
		}
	}
}
