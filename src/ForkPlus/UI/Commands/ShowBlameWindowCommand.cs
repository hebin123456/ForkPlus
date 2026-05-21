using System.Collections.Generic;
using System.Windows.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowBlameWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Blame...", new Argument[1]
			{
				new Argument(ArgumentType.RepositoryFile)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				string filePath = arguments[0] as string;
				RepositoryUserControl.Commands.ShowBlameWindow.Execute(repositoryUserControl, filePath, null);
			})
		};

		public string Title => "Blame/Timeline...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, string filePath, Sha? sha)
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
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			Sha? headSha = repositoryData.References.HeadSha;
			if (!headSha.HasValue)
			{
				return;
			}
			Sha valueOrDefault = headSha.GetValueOrDefault();
			GitCommandResult<Sha?> gitCommandResult = CommitGraphHelper.FindBranchTip(gitModule, commitGraphCache, valueOrDefault, repositoryData.References.ReferenceStorage, sha);
			Sha? fartherestTipSha = null;
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
				return;
			}
			fartherestTipSha = gitCommandResult.Result;
			List<Reference> source = repositoryData.References.Items.Filter(delegate(Reference x)
			{
				Sha sha2 = x.Sha;
				Sha? sha3 = fartherestTipSha;
				return sha2 == sha3;
			});
			Reference targetReference = IReadOnlyListExtensions.FirstItem(source, (Reference x) => (x as LocalBranch)?.IsActive ?? false) ?? source.FirstItem();
			new BlameWindow(repositoryUserControl, filePath, sha, targetReference).Show();
		}
	}
}
