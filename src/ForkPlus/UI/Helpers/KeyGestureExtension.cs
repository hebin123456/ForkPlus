using ForkPlus.UI.Helpers;
using System.Text;
using Avalonia.Input;

namespace ForkPlus.UI.Helpers
{
	public static class KeyGestureExtension
	{
		public static string ToFriendlyString(this KeyGesture gesture)
		{
			StringBuilder stringBuilder = new StringBuilder(16);
		// Migration note：Avalonia KeyGesture 的修饰键属性是 KeyModifiers（WPF 为 Modifiers），枚举成员名一致。
			if ((gesture.KeyModifiers & global::Avalonia.Input.KeyModifiers.Control) != 0)
			{
				stringBuilder.Append("Ctrl+");
			}
			if ((gesture.KeyModifiers & global::Avalonia.Input.KeyModifiers.Shift) != 0)
			{
				stringBuilder.Append("Shift+");
			}
			if ((gesture.KeyModifiers & global::Avalonia.Input.KeyModifiers.Alt) != 0)
			{
				stringBuilder.Append("Alt+");
			}
			switch (gesture.Key)
			{
			case Key.Return:
				stringBuilder.Append("Enter");
				break;
			case Key.D0:
				stringBuilder.Append("0");
				break;
			case Key.D1:
				stringBuilder.Append("1");
				break;
			case Key.D2:
				stringBuilder.Append("2");
				break;
			case Key.OemPlus:
				stringBuilder.Append("=");
				break;
			case Key.OemMinus:
				stringBuilder.Append("-");
				break;
			case Key.OemComma:
				stringBuilder.Append(",");
				break;
			case Key.OemPeriod:
				stringBuilder.Append(".");
				break;
			default:
				stringBuilder.Append(gesture.Key.ToString());
				break;
			}
			return stringBuilder.ToString();
		}
	}
}
