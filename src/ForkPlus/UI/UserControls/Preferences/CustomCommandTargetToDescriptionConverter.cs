using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using ForkPlus.UI.CustomCommands;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandTargetToDescriptionConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
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
