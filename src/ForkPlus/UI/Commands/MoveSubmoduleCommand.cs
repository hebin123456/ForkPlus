using System.IO;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class MoveSubmoduleCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Move...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Submodule submodule)
		{
			if (gitModule == null)
			{
				return;
			}
			string directoryName = Path.GetDirectoryName(gitModule.MakePath(submodule.Path));
			if (!ServiceLocator.FileSystemDialog.SelectDirectory(Translate("Select empty folder for new submodule location"), directoryName, out var directoryPath))
			{
				return;
			}
			if (!directoryPath.StartsWith(gitModule.Path))
			{
				new ErrorWindow(string.Format(Translate("Can not move submodule to {0}"), PathHelper.NormalizeUnix(directoryPath))).ShowDialog();
				return;
			}
			string newSubmodulePath = PathHelper.NormalizeUnix(directoryPath.Substring(gitModule.Path.Length));
			newSubmodulePath = newSubmodulePath.TrimStart('/');
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Moving '{0}'"), submodule.FriendlyName), delegate(JobMonitor monitor)
			{
				GitCommandResult moveSubmoduleResult = new MoveSubmoduleGitCommand().Execute(gitModule, submodule.Path, newSubmodulePath, monitor);
				if (!moveSubmoduleResult.Succeeded)
				{
					repositoryUserControl.Dispatcher.Async(delegate
					{
						new ErrorWindow(repositoryUserControl, moveSubmoduleResult.Error).ShowDialog();
						repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Submodules);
					});
				}
				else
				{
					GitCommandResult renameGitmodulesSectionResult = new RenameGitmodulesSectionGitCommand().Execute(gitModule, submodule.Path, newSubmodulePath, monitor);
					if (!renameGitmodulesSectionResult.Succeeded)
					{
						repositoryUserControl.Dispatcher.Async(delegate
						{
							new ErrorWindow(repositoryUserControl, renameGitmodulesSectionResult.Error).ShowDialog();
							repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Submodules);
						});
					}
					else
					{
						repositoryUserControl.Dispatcher.Async(delegate
						{
							repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Submodules);
						});
					}
				}
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
