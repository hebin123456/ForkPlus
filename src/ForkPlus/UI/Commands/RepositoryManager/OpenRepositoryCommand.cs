using System;
using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class OpenRepositoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Return);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(ForkPlus.RepositoryManager.Repository? repository)
		{
			if (!repository.HasValue)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(repository.Value.Path))
				{
					DeleteRepository(repository.Value);
					ServiceLocator.WindowManager.RefreshActiveRepositoryManager();
					return;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove invalid repo entry", ex);
			}
			if (!ServiceLocator.WindowManager.OpenRepository(repository.Value.Path))
			{
				GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(repository.Value.Path);
				if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				}
			}
		}

		public void Execute([Null] ForkPlusSettings.RepositoryManagerSettings.Repository repository)
		{
			if (repository == null)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(repository.Path))
				{
					DeleteRepository(repository);
					ServiceLocator.WindowManager.RefreshActiveRepositoryManager();
					return;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove invalid repo entry", ex);
			}
			if (!ServiceLocator.WindowManager.OpenRepository(repository.Path))
			{
				GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(repository.Path);
				if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				}
			}
		}

		private void DeleteRepository(ForkPlusSettings.RepositoryManagerSettings.Repository repositoryToDelete)
		{
			if (new MessageBoxWindow("Repository Not Found", ServiceLocator.Localization.FormatCurrent("It looks like '{0}' was deleted or moved. Do you want to remove the reference from Fork?", repositoryToDelete.Name), "Remove").ShowDialog().GetValueOrDefault())
			{
				ForkPlus.RepositoryManager.Instance.DeleteRepositories(new string[1] { repositoryToDelete.Path });
			}
		}

		private void DeleteRepository(ForkPlus.RepositoryManager.Repository repositoryToDelete)
		{
			if (new MessageBoxWindow("Repository Not Found", ServiceLocator.Localization.FormatCurrent("It looks like '{0}' was deleted or moved. Do you want to remove the reference from Fork?", repositoryToDelete.Name()), "Remove").ShowDialog().GetValueOrDefault())
			{
				ForkPlus.RepositoryManager.Instance.DeleteRepositories(new string[1] { repositoryToDelete.Path });
			}
		}
	}
}
