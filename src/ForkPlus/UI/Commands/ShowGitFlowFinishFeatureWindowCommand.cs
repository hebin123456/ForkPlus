using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowFinishFeatureWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Finish Feature...", new Argument[1]
			{
				new Argument(ArgumentType.FeatureBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null && arguments[0] is LocalBranch featureBranch)
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.Execute(repositoryUserControl, gitModule, repositoryData, featureBranch);
					}
				}
			})
		};

		public string Title => "Finish Feature...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, LocalBranch featureBranch)
		{
			if (gitModule == null || repositoryData == null)
			{
				return;
			}
			GitFlowFinishFeatureWindow gitFlowFinishFeatureWindow = new GitFlowFinishFeatureWindow(gitModule, repositoryData, featureBranch);
			if (gitFlowFinishFeatureWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
				if (!gitFlowFinishFeatureWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowFinishFeatureWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
