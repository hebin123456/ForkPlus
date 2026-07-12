using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class AddSubmoduleGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string submoduleUrl, string submodulePath, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "submodule", "add", "--force", "--progress", submoduleUrl, submodulePath.Quotify());
			monitor.Update(0.0, PreferencesLocalization.FormatCurrent("Adding '{0}'", PathHelper.GetReadableFileName(submodulePath)));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string stdErrLine)
			{
				if (!monitor.HandleGitProgress(stdErrLine))
				{
					monitor.AppendOutputLine(stdErrLine);
				}
			}, monitor);
			monitor.Success(PreferencesLocalization.FormatCurrent("Added '{0}'", PathHelper.GetReadableFileName(submodulePath)));
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
