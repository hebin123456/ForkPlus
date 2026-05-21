using System.Windows.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class ShowLeanBranchingFinishWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Finish Branch...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			RepositoryStatus repositoryStatus = repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			Branch branch = repositoryData.References.MainBranch(gitModule, commitGraphCache);
			if (branch == null)
			{
				return;
			}
			if (repositoryStatus.WorkingDirectoryIsDirty())
			{
				new ErrorWindow(PreferencesLocalization.Translate("Cannot sync: You have unstaged changes. Please commit or stash them.", ForkPlusSettings.Default.UiLanguage)).ShowDialog();
				return;
			}
			LeanBranchingFinishWindow leanBranchingFinishWindow = new LeanBranchingFinishWindow(repositoryUserControl);
			if (leanBranchingFinishWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(branch.Sha));
				if (!leanBranchingFinishWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, leanBranchingFinishWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
