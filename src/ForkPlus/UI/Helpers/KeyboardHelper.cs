using ForkPlus.UI.Helpers;
using System.Windows.Input;

namespace ForkPlus.UI.Helpers
{
	public static class KeyboardHelper
	{
		public static bool IsShiftDown
		{
			get
			{
				if (!Keyboard.IsKeyDown(Key.LeftShift))
				{
					return Keyboard.IsKeyDown(Key.RightShift);
				}
				return true;
			}
		}

		public static bool IsCtrlDown
		{
			get
			{
				if (!Keyboard.IsKeyDown(Key.LeftCtrl))
				{
					return Keyboard.IsKeyDown(Key.RightCtrl);
				}
				return true;
			}
		}

		public static bool IsAltDown
		{
			get
			{
				if (!Keyboard.IsKeyDown(Key.LeftAlt))
				{
					return Keyboard.IsKeyDown(Key.RightAlt);
				}
				return true;
			}
		}
	}
}
