using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushTagWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Push Tag...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Tag tag, [Null] Remote remote)
		{
			PushTagWindow pushTagWindow = new PushTagWindow(repositoryUserControl, tag, remote);
			if (pushTagWindow.ShowDialog().GetValueOrDefault())
			{
				if (!pushTagWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, pushTagWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
			}
		}
	}
}
