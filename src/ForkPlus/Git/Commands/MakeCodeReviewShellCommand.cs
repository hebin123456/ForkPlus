using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class MakeCodeReviewShellCommand
	{
		public GitCommandResult<string> Execute(AiAgent aiAgent, AiCodeReviewTarget target, string currentDir, JobMonitor monitor)
		{
			string text;
			if (target is AiCodeReviewTarget.Branch branch)
			{
				text = "Review `" + branch.Name + "` branch by checking the following range: `" + branch.Src.ToString() + ".." + branch.Dst.ToString() + "`. Do not fetch.";
			}
			else
			{
				if (!(target is AiCodeReviewTarget.ShaRange shaRange))
				{
					return GitCommandResult<string>.Failure(new GitCommandError.GenericError("Unsupported target type"));
				}
				text = "Review commits in the following range: `" + shaRange.Src.ToString() + ".." + shaRange.Dst.ToString() + "`. Do not fetch.";
			}
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			monitor.Update(monitor.TotalProgress, PreferencesLocalization.FormatCurrent("Reviewing with {0}...", aiAgent.Name));
			ExecuteWithCallbackResponse executeWithCallbackResponse = default(GitRequest).CurrentDir(currentDir).Path(aiAgent.Path).Command(text)
				.ExecuteWithCallbackBt(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<string>.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult<string>.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(processOutputHandler.Stderr());
				return GitCommandResult<string>.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
			}
			monitor.Success(PreferencesLocalization.Current("Finished"));
			return GitCommandResult<string>.Success(processOutputHandler.FullOutput());
		}
	}
}
