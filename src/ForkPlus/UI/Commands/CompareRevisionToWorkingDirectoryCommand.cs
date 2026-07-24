using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class CompareRevisionToWorkingDirectoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Compare to Local Changes";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(Sha sha)
		{
			ServiceLocator.WindowManager.ShowRevisionDetailsOnActiveRepository(new RevisionDiffTarget.WorkingDirectory(sha));
		}
	}
}
