using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 阶段 4.5：从 WPF <c>Application.Current.TryFindResource</c> 迁移到
	/// <see cref="Theme.FindBrush"/> 门面（Avalonia 无 TryFindResource，使用 Resources.TryGetResource）。
	/// </summary>
	public class InteractiveRebaseActionToForegroundConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			InteractiveRebaseAction interactiveRebaseAction = (InteractiveRebaseAction)value;
			if (interactiveRebaseAction == InteractiveRebaseAction.Squash || interactiveRebaseAction == InteractiveRebaseAction.Fixup || interactiveRebaseAction == InteractiveRebaseAction.Drop)
			{
				return Theme.FindBrush("ForegroundBrush.Gray");
			}
			return Theme.FindBrush("ForegroundBrush");
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
