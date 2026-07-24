using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRevisionInSeparateWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open in Separate Window...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Return);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, RevisionDiffTarget target, [Null] string fileToSelect = null)
		{
			if (!(target is RevisionDiffTarget.WorkingDirectory) && !(target is RevisionDiffTarget.MultipleRevisions))
			{
				new RevisionDetailsWindow(Application.Current.ActiveRepositoryUserControl(), gitModule, target, fileToSelect).Show();
			}
		}

		public void Execute(RepositoryUserControl repositoryUserControl, RevisionDiffTarget target, [Null] string fileToSelect = null)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null && !(target is RevisionDiffTarget.WorkingDirectory) && !(target is RevisionDiffTarget.MultipleRevisions))
			{
				new RevisionDetailsWindow(repositoryUserControl, gitModule, target, fileToSelect).Show();
			}
		}
	}
}
