using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public class WindowLocationState
	{
		public double Left { get; }

		public double Top { get; }

		public double Width { get; }

		public double Height { get; }

		public global::Avalonia.Controls.WindowState WindowState { get; }

		public WindowLocationState(double left, double top, double width, double height, global::Avalonia.Controls.WindowState windowState)
		{
			Left = left;
			Top = top;
			Width = width;
			Height = height;
			WindowState = windowState; // Migration note：自动转换误将属性名写成全限定类型名，恢复属性赋值。
		}
	}
}
