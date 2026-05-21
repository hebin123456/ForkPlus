using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class OpenRepositoriesCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open All";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(IReadOnlyList<string> repositories)
		{
			Application.Current.TabManager()?.OpenRepositories(repositories.ToArray());
		}
	}
}
