using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
