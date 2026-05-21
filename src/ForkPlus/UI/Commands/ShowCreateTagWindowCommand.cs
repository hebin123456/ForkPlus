using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCreateTagWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Create Tag...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, null);
			})
		};

		public string Title => "New Tag...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, IGitPoint gitPoint)
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
			CreateTagWindow createTagWindow = new CreateTagWindow(gitModule, repositoryData.References, repositoryData.Remotes.Items, gitPoint2);
			if (createTagWindow.ShowDialog().GetValueOrDefault())
			{
				if (!createTagWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, createTagWindow.GitResult.Error).ShowDialog();
				}
				Application.Current.ActiveRepositoryUserControl().InvalidateAndRefresh(SubDomain.References, new RevisionSelector.Sha(valueOrDefault));
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
