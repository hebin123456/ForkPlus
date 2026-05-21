using System.Windows.Input;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class SwitchWorkspaceCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Switch Workspace", new Argument[1]
			{
				new Argument(ArgumentType.Workspace)
			}, delegate(object[] arguments, RepositoryUserControl _)
			{
				MainWindow.Commands.SwitchWorkspace.Execute(arguments[0] as Workspace);
			})
		};

		public string Title => "Switch Workspace";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			Workspace[] all = ForkPlusSettings.Default.Workspaces.All;
			if (all.Length < 2)
			{
				return;
			}
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] == ForkPlusSettings.Default.Workspaces.ActiveWorkspace)
				{
					int num = ((i != all.Length - 1) ? (i + 1) : 0);
					Execute(all[num]);
					break;
				}
			}
		}

		public void Execute(Workspace newWorkspace)
		{
			MainWindow instance = MainWindow.Instance;
			instance.TabManager.SaveSession();
			ForkPlusSettings.Default.Workspaces.ActiveWorkspace = newWorkspace;
			instance.TabManager.RestoreSession();
			instance.Toolbar.RefreshWorkspacesButton();
			instance.RefreshTitle();
			instance.RefreshRepositoriesStatus();
		}
	}
}
