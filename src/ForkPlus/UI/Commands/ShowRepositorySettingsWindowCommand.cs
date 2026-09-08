using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using ForkPlus.UI.WpfCompat;

namespace ForkPlus.UI.Commands
{
	public class ShowRepositorySettingsWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Settings...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						RepositoryUserControl.Commands.ShowRepositorySettingsWindow.Execute(gitModule, repositoryData);
					}
				}
			})
		};

		public string Title { get; } = "Settings for This Repository...";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, RepositoryData repositoryData)
		{
			// 居中到主窗口所在屏幕（原版 WPF：Owner + CenterOwner）。
			Window owner = MainWindow.Instance;
			new RepositorySettingsWindow(gitModule, repositoryData)
				.SetOwnerAndCenter(owner)
				.ShowDialog(owner);
			Application.Current.ActiveRepositoryUserControl()?.InvalidateAndRefresh(SubDomain.All);
		}
	}
}
