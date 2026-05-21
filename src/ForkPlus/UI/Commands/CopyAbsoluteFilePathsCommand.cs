using System;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class CopyAbsoluteFilePathsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Full Path";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, string[] filePaths)
		{
			string normalizedGitModulePath = PathHelper.Normalize(gitModule.Path);
			ClipboardHelper.SetText(string.Join(Environment.NewLine, filePaths.Map((string x) => Path.Combine(normalizedGitModulePath, PathHelper.Normalize(x)))));
		}
	}
}
