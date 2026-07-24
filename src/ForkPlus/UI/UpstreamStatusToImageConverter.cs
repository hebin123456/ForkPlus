using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class UpstreamStatusToImageConverter : MarkupExtension, IValueConverter
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
						return Theme.BranchIcon;
					}
					return Theme.BranchSelectedIcon;
				}
				if (!flag)
				{
					return Theme.BranchWarningIcon;
				}
				return Theme.BranchWarningSelectedIcon;
			}
			if (!flag)
			{
				return Theme.BranchPaleIcon;
			}
			return Theme.BranchPaleSelectedIcon;
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
