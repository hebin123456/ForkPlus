using System;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;
namespace ForkPlus.UI.Commands
{
	public class CopyWorktreePathsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Path";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute(Worktree[] worktrees)
		{
			ServiceLocator.Clipboard.SetText(string.Join(Environment.NewLine, worktrees.Map((Worktree x) => PathHelper.Normalize(x.Path))));
		}
	}
}
