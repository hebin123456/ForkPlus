using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class RunCustomCommandCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Custom Command";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, CustomCommand customCommand, CustomCommandEnvironment env)
		{
			if (AllowSharedCommandsExecution(customCommand.Shared, env.GitModule))
			{
				if (customCommand.UI != null)
				{
					ShowCustomCommandWindow(repositoryUserControl, customCommand.Name, customCommand.UI, env);
				}
				else if (customCommand.Action != null)
				{
					customCommand.Action.Execute(repositoryUserControl, customCommand.Name, env);
				}
				else
				{
					Log.Error("Cannot execute '" + customCommand.Name + "'. Either 'UI' or 'Action' field must be defined");
				}
			}
		}

		private void ShowCustomCommandWindow(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandUI ui, CustomCommandEnvironment env)
		{
			new CustomCommandUIWindow(repositoryUserControl, customCommandName, ui, env).ShowDialog();
		}

		private bool AllowSharedCommandsExecution(bool shared, GitModule gitModule)
		{
			if (!shared)
			{
				return true;
			}
			if (gitModule.Settings.TrustSharedCommands)
			{
				return true;
			}
			RunSharedCustomCommandConfirmationWindow runSharedCustomCommandConfirmationWindow = new RunSharedCustomCommandConfirmationWindow(gitModule.RepositoryName);
			runSharedCustomCommandConfirmationWindow.Owner = MainWindow.Instance;
			if (runSharedCustomCommandConfirmationWindow.ShowDialog().GetValueOrDefault())
			{
				if (runSharedCustomCommandConfirmationWindow.TrustThisRepository)
				{
					gitModule.Settings.TrustSharedCommands = true;
					gitModule.Settings.Save();
				}
				return true;
			}
			return false;
		}
	}
}
