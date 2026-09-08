using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Migration note：WPF 内置 System.Windows.Controls.BooleanToVisibilityConverter 的等价实现。
	/// WPF 里该转换器全局可用（bool → Visibility.Visible/Collapsed），Avalonia 无内置；
	/// ExternalToolsUserControl / MergeConflictUserControl / CustomCommandsUserControl 的
	/// StaticResource 引用曾因缺失此类在构造时抛 KeyNotFoundException（2026-08-30 偏好设置
	/// 冒烟实证：PreferencesWindow → IntegrationUserControl → ExternalToolsUserControl 链路崩溃）。
	/// Avalonia 的 IsVisible 直接接受 bool，故此处返回 bool 而非枚举（null 视为 false）。
	/// </summary>
	public class BooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool b)
			{
				return b;
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool b)
			{
				return b;
			}
			return false;
		}
	}
}
