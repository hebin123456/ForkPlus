using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCreateBranchWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Create Branch...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, null);
			})
		};

		public string Title => "New Branch...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, [Null] IGitPoint gitPoint)
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
			object obj = gitPoint;
			if (obj == null)
			{
				IGitPoint activeBranch = repositoryData.References.ActiveBranch;
				obj = activeBranch ?? GetRevision(gitModule, repositoryData.References.HeadSha);
			}
			IGitPoint gitPoint2 = (IGitPoint)obj;
			if (gitPoint2 == null)
			{
				return;
			}
			Sha? sha = GetSha(gitPoint2);
			if (!sha.HasValue)
			{
				return;
			}
			Sha valueOrDefault = sha.GetValueOrDefault();
			CreateBranchWindow createBranchWindow = new CreateBranchWindow(repositoryUserControl, repositoryData.References, gitPoint2);
			if (createBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (createBranchWindow.Checkout)
				{
					repositoryUserControl.Invalidate(SubDomain.Status | SubDomain.Stashes | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands);
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Head | SubDomain.References, new RevisionSelector.Sha(valueOrDefault));
				if (!createBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, createBranchWindow.GitResult.Error).ShowDialog();
				}
			}
		}

		private Sha? GetSha(IGitPoint gitPoint)
		{
			if (gitPoint is Revision revision)
			{
				return revision.Sha;
			}
			if (gitPoint is Reference reference)
			{
				return reference.Sha;
			}
			return null;
		}

		[Null]
		private static Revision GetRevision(GitModule gitModule, Sha? sha)
		{
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, new Sha[1] { valueOrDefault });
				if (!gitCommandResult.Succeeded)
				{
					Log.Error(gitCommandResult.Error.FriendlyDescription);
					return null;
				}
				return gitCommandResult.Result.FirstItem();
			}
			return null;
		}
	}
}
