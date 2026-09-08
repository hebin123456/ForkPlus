using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Migration note：WPF MenuItem.InputGestureText 是字符串属性（原代码可自行格式化）；
	/// Avalonia MenuItem.InputGesture 是 KeyGesture 对象，模板 TextBlock.Text 直接绑定会
	/// ToString() 出 "Ctrl+OemComma" 这类原始枚举名。本转换器走 ToFriendlyString()
	/// （OemComma→","、OemPeriod→"."、Return→Enter 等映射），还原 WPF 显示。
	/// </summary>
	public class KeyGestureTextConverter : IValueConverter
	{
		public static readonly KeyGestureTextConverter Instance = new KeyGestureTextConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is KeyGesture gesture)
			{
				return gesture.ToFriendlyString();
			}
			return string.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
