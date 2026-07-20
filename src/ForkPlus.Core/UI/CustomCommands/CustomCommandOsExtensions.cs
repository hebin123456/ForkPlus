namespace ForkPlus.UI.CustomCommands
{
	public static class CustomCommandOsExtensions
	{
		public static bool IsSupported(this CustomCommandOS os)
		{
			if (os != 0)
			{
				return os == CustomCommandOS.Windows;
			}
			return true;
		}
	}
}
