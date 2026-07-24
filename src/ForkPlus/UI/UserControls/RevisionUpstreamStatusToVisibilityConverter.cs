using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 4.5：从 WPF Visibility 迁移到 Avalonia bool（绑定 IsVisible）。
	/// 原 WPF: ActiveBranchCommitStatus.None -> Visibility.Collapsed, 其他 -> Visibility.Visible。
	/// </summary>
	public class RevisionUpstreamStatusToVisibilityConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (ActiveBranchCommitStatus)value != ActiveBranchCommitStatus.None;
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
