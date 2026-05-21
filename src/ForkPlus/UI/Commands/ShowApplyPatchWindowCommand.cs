using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowApplyPatchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, string patchPath)
		{
			ApplyPatchWindow applyPatchDialog = new ApplyPatchWindow(repositoryUserControl, patchPath);
			ShowDialog(repositoryUserControl, applyPatchDialog);
		}

		public void Execute(RepositoryUserControl repositoryUserControl, byte[] patchData)
		{
			ApplyPatchWindow applyPatchDialog = new ApplyPatchWindow(repositoryUserControl, patchData);
			ShowDialog(repositoryUserControl, applyPatchDialog);
		}

		private static void ShowDialog(RepositoryUserControl repositoryUserControl, ApplyPatchWindow applyPatchDialog)
		{
			if (applyPatchDialog.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.All, null, RepositoryViewMode.CommitViewMode);
				if (!applyPatchDialog.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, applyPatchDialog.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
