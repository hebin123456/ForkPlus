using Avalonia.Input;
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
			ShowDialog(repositoryUserControl, applyPatchDialog); // Migration note：静态方法不能 this. 调用
		}

		public void Execute(RepositoryUserControl repositoryUserControl, byte[] patchData)
		{
			ApplyPatchWindow applyPatchDialog = new ApplyPatchWindow(repositoryUserControl, patchData);
			ShowDialog(repositoryUserControl, applyPatchDialog); // Migration note：静态方法不能 this. 调用
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
