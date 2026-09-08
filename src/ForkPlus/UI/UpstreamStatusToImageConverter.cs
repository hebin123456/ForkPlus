using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using ForkPlus.Git;
using Avalonia.Data.Converters;

namespace ForkPlus.UI
{
	public class UpstreamStatusToImageConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = parameter as string == "true";
			if (value is UpstreamStatus upstreamStatus)
			{
				if (upstreamStatus.IsValid)
				{
					if (!flag)
					{
						return global::ForkPlus.UI.Theme.BranchIcon;
					}
					return global::ForkPlus.UI.Theme.BranchSelectedIcon;
				}
				if (!flag)
				{
					return global::ForkPlus.UI.Theme.BranchWarningIcon;
				}
				return global::ForkPlus.UI.Theme.BranchWarningSelectedIcon;
			}
			if (!flag)
			{
				return global::ForkPlus.UI.Theme.BranchPaleIcon;
			}
			return global::ForkPlus.UI.Theme.BranchPaleSelectedIcon;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
