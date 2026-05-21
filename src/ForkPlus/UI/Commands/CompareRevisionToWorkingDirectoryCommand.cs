using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class CompareRevisionToWorkingDirectoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Compare to Local Changes";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(Sha sha)
		{
			Application.Current.ActiveRepositoryUserControl()?.ShowRevisionDetails(new RevisionDiffTarget.WorkingDirectory(sha));
		}
	}
}
