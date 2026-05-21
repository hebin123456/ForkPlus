using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowMergeBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Merge...", new Argument[1]
			{
				new Argument(ArgumentType.Reference)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryData repositoryData = repositoryUserControl.RepositoryData;
				if (repositoryData != null && arguments[0] is Reference source)
				{
					LocalBranch activeBranch = repositoryData.References.ActiveBranch;
					RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, source, activeBranch);
				}
			})
		};

		public string Title => "Merge Branch";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Reference source, [Null] LocalBranch destination)
		{
			if (destination == null || source == destination)
			{
				return;
			}
			MergeBranchWindow mergeBranchWindow = new MergeBranchWindow(repositoryUserControl, source, destination);
			if (!mergeBranchWindow.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Head());
			if (!mergeBranchWindow.GitResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, mergeBranchWindow.GitResult.Error).ShowDialog();
				if (!(mergeBranchWindow.GitResult.Error is GitCommandError.MergeUnrelatedHistory))
				{
					repositoryUserControl.ActivateCommitView();
				}
			}
			else if (mergeBranchWindow.SelectedMergeType == MergeType.Squash || mergeBranchWindow.SelectedMergeType == MergeType.NoCommit)
			{
				repositoryUserControl.ActivateCommitView(focusCommitSubject: true);
			}
		}
	}
}
