using System;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class CopyAbsoluteFilePathsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Full Path";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.C, KeyModifiers.Control | KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, string[] filePaths)
		{
			string normalizedGitModulePath = PathHelper.Normalize(gitModule.Path);
			ServiceLocator.Clipboard.SetText(string.Join(Environment.NewLine, filePaths.Map((string x) => Path.Combine(normalizedGitModulePath, PathHelper.Normalize(x)))));
		}
	}
}
