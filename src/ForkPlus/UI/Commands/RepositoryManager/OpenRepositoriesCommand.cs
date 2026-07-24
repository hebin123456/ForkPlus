using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class OpenRepositoriesCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open All";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(IReadOnlyList<string> repositories)
		{
			ServiceLocator.WindowManager.OpenRepositories(repositories.ToArray());
		}
	}
}
