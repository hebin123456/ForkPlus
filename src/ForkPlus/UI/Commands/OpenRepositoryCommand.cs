using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class OpenRepositoryCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Open Repository...", new Argument[0], delegate
			{
				MainWindow.Commands.OpenRepository.Execute();
			})
		};

		public string Title => "Open Repository...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			string initialDirectory = ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			if (!ServiceLocator.FileSystemDialog.SelectDirectory(Title, initialDirectory, out var directoryPath) || ServiceLocator.WindowManager.OpenRepository(directoryPath))
			{
				return;
			}
			GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(directoryPath);
			if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			else if (new OpenRepositoryAlertWindow(directoryPath).ShowDialog().GetValueOrDefault())
			{
				GitCommandResult gitCommandResult2 = new InitRepositoryGitCommand().Execute(directoryPath);
				if (!gitCommandResult2.Succeeded)
				{
					new ErrorWindow(null, gitCommandResult2.Error).ShowDialog();
				}
				else
				{
					ServiceLocator.WindowManager.OpenRepository(directoryPath);
				}
			}
		}
	}
}
