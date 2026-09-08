using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
