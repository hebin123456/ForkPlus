using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowStartFeatureWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Start Feature...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Start Feature...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (gitModule == null)
			{
				return;
			}
			GitFlowStartFeatureWindow gitFlowStartFeatureWindow = new GitFlowStartFeatureWindow(gitModule);
			if (gitFlowStartFeatureWindow.ShowDialog().GetValueOrDefault())
			{
				if (!gitFlowStartFeatureWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowStartFeatureWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
			}
		}
	}
}
