using ForkPlus.Services;
using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RebaseInteractiveGitCommand
	{
		private static readonly string TokenSeparator = "#!_";

		public GitCommandResult Execute(GitModule gitModule, [Null] IGitPoint destination)
		{
			string input = PathHelper.NormalizeUnix(Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.RIHelperFilename));
			GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "rebase.instructionFormat=" + TokenSeparator + "%H", "-c", "rebase.abbreviateCommands=true", "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "-i", "--autosquash", "--update-refs");
			if (destination == null)
			{
				gitCommand.Add("--root");
			}
			else
			{
				gitCommand.Add(destination.ObjectName);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.IndexOf("nothing to do", StringComparison.OrdinalIgnoreCase) != -1 || gitRequestResult.Stderr.Contains("error: Failed to merge in the changes.") || gitRequestResult.Stderr.Contains("Resolve all conflicts manually, mark them as resolved with"))
				{
					return GitCommandResult.Success();
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
