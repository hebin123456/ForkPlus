using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSaveRevisionsAsPatchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save as Patch...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Revision[] revisions)
		{
			Revision revision = revisions.FirstItem();
			if (revision != null)
			{
				new SaveAsPatchWindow(repositoryUserControl, gitModule, revision, ((revisions.Length == 2) ? revisions[1] : null)?.Sha).ShowDialog();
			}
		}
	}
}
