using System;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class CopyFilePathsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Path";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.C, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(string[] filePaths)
		{
			ClipboardHelper.SetText(string.Join(Environment.NewLine, filePaths.Map((string x) => PathHelper.Normalize(x))));
		}
	}
}
