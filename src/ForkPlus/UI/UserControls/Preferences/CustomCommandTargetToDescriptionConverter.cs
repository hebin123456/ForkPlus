using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandTargetToDescriptionConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CustomCommandTarget)
			{
				switch ((CustomCommandTarget)value)
				{
				case CustomCommandTarget.Revision:
					return "Commit";
				case CustomCommandTarget.Repository:
					return "Repository";
				case CustomCommandTarget.RepositoryFile:
					return "File";
				case CustomCommandTarget.Reference:
					return "Branch";
				case CustomCommandTarget.Submodule:
					return "Submodule";
				}
			}
			return string.Empty;
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
