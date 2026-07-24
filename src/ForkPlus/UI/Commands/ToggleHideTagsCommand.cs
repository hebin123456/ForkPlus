using System.Windows;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ToggleHideTagsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Hide Tags";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			GitModule gitModule = ServiceLocator.WindowManager.GetActiveRepositoryGitModule();
			if (gitModule != null)
			{
				gitModule.Settings.HideTags = !gitModule.Settings.HideTags;
				gitModule.Settings.Save();
				ServiceLocator.WindowManager.InvalidateAndRefreshActiveRepositoryView(SubDomain.References);
			}
		}
	}
}
