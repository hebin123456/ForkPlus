using System;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class UpdateSubmoduleCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Update";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Submodule[] submodules, Action<GitCommandResult> callback = null)
		{
			Submodule submodule = submodules.SingleItem();
			string name;
			string progressMessage;
			if (submodule != null)
			{
				name = Translate("Updating submodule");
				progressMessage = submodule.FriendlyName;
			}
			else
			{
				name = string.Format(Translate("Updating {0} submodules"), submodules.Length);
				progressMessage = string.Format(Translate("{0} submodules"), submodules.Length);
			}
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				monitor.Update(0.0, progressMessage);
				GitCommandResult updateSubmoduleResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodules, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (callback != null)
					{
						callback(updateSubmoduleResult);
					}
					else
					{
						if (!updateSubmoduleResult.Succeeded)
						{
							new ErrorWindow(repositoryUserControl, updateSubmoduleResult.Error).ShowDialog();
						}
						repositoryUserControl.InvalidateAndRefresh(SubDomain.Status);
					}
				});
			});
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
