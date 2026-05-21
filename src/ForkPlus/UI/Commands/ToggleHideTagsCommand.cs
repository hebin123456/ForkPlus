using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ToggleHideTagsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Hide Tags";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			RepositoryUserControl repositoryUserControl = Application.Current.ActiveRepositoryUserControl();
			if (repositoryUserControl != null)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					gitModule.Settings.HideTags = !gitModule.Settings.HideTags;
					gitModule.Settings.Save();
					repositoryUserControl.InvalidateAndRefresh(SubDomain.References);
				}
			}
		}
	}
}
