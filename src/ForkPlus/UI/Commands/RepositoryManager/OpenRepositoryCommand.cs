using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

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
			TabManager tabManager = Application.Current.TabManager();
			if (tabManager == null)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(repository.Value.Path))
				{
					DeleteRepository(repository.Value);
					Application.Current.TabManager()?.ActiveRepositoryManager?.Refresh();
					return;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove invalid repo entry", ex);
			}
			if (!tabManager.OpenRepository(repository.Value.Path))
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
			TabManager tabManager = Application.Current.TabManager();
			if (tabManager == null)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(repository.Path))
				{
					DeleteRepository(repository);
					Application.Current.TabManager()?.ActiveRepositoryManager?.Refresh();
					return;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove invalid repo entry", ex);
			}
			if (!tabManager.OpenRepository(repository.Path))
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
			if (new MessageBoxWindow("Repository Not Found", PreferencesLocalization.FormatCurrent("It looks like '{0}' was deleted or moved. Do you want to remove the reference from Fork?", repositoryToDelete.Name), "Remove").ShowDialog().GetValueOrDefault())
			{
				ForkPlus.RepositoryManager.Instance.DeleteRepositories(new string[1] { repositoryToDelete.Path });
			}
		}

		private void DeleteRepository(ForkPlus.RepositoryManager.Repository repositoryToDelete)
		{
			if (new MessageBoxWindow("Repository Not Found", PreferencesLocalization.FormatCurrent("It looks like '{0}' was deleted or moved. Do you want to remove the reference from Fork?", repositoryToDelete.Name()), "Remove").ShowDialog().GetValueOrDefault())
			{
				ForkPlus.RepositoryManager.Instance.DeleteRepositories(new string[1] { repositoryToDelete.Path });
			}
		}
	}
}
