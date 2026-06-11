using ForkPlus.UI.Helpers;
using System.Text;
using System.Windows.Input;

namespace ForkPlus.UI.Helpers
{
	public static class KeyGestureExtension
	{
		public static string ToFriendlyString(this KeyGesture gesture)
		{
			StringBuilder stringBuilder = new StringBuilder(16);
			if ((gesture.Modifiers & ModifierKeys.Control) != 0)
			{
				stringBuilder.Append("Ctrl+");
			}
			if ((gesture.Modifiers & ModifierKeys.Shift) != 0)
			{
				stringBuilder.Append("Shift+");
			}
			if ((gesture.Modifiers & ModifierKeys.Alt) != 0)
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
