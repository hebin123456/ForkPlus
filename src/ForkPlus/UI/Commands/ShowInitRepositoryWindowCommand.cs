using System;
using Avalonia.Input;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowInitRepositoryWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Init New Repository...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.N, KeyModifiers.Control | KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			string text = ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			if (!ServiceLocator.FileSystemDialog.SelectDirectory("Select repository", text, out var directoryPath))
			{
				return;
			}
			string text2 = Environment.ExpandEnvironmentVariables("%userprofile%");
			if (directoryPath == text2 || directoryPath == text)
			{
				new MessageBoxWindow("Invalid directory", "It's not a good idea to make your source folder a repository. Please create a subfolder instead.", "Close", "Cancel", showCancelButton: false).ShowDialog();
				return;
			}
			if (new ValidateRepositoryPathGitCommand().Execute(directoryPath) != 0)
			{
				new MessageBoxWindow("Already a git repository", "'" + directoryPath + "' is already a git repository. Please select another folder.", "Close", "Cancel", showCancelButton: false).ShowDialog();
				return;
			}
			GitCommandResult gitCommandResult = new InitRepositoryGitCommand().Execute(directoryPath);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			else
			{
				ServiceLocator.WindowManager.OpenRepository(directoryPath);
			}
		}
	}
}
