using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class HighlightableTextBlock : TextBlock
	{
		// Migration note：WPF DependencyProperty → Avalonia StyledProperty。
		// 原转换用 RegisterAttached<..., AvaloniaObject, ...>（附加属性形式），XAML 属性元素语法
		// <controls:HighlightableTextBlock.HighlightString><Binding/></...> 无法解析（AVLN3000），
		// 改为普通 Register 使其成为可绑定 StyledProperty。
		public static readonly global::Avalonia.StyledProperty<string> HighlightStringProperty =
    global::Avalonia.AvaloniaProperty.Register<HighlightableTextBlock, string>("HighlightString");

		public string HighlightString
		{
			get
			{
				return GetValue(HighlightStringProperty);
			}
			set
			{
				SetValue(HighlightStringProperty, value);
			}
		}
	}
}
