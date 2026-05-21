using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class FetchGitCommand
	{
		private static readonly Regex UpdatedRegEx = new Regex("^ [ =\\*\\+-] (\\[.*\\]|.*?)\\s+.+ -> .+", RegexOptions.Multiline);

		private static readonly Regex NoWiFiRegEx = new Regex(" Could not resolve host: (.+)", RegexOptions.Multiline);

		public GitCommandResult Execute(GitModule gitModule, Remote remote, bool fetchAllRemotes, JobMonitor monitor, bool noPrompt, bool allTags = false, bool force = false)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper);
			gitCommand.Add("fetch");
			gitCommand.Add("--prune");
			if (force)
			{
				gitCommand.Add("--force");
			}
			if (fetchAllRemotes)
			{
				gitCommand.Add("--all");
			}
			else
			{
				gitCommand.Add(remote.Name);
			}
			if (allTags)
			{
				gitCommand.Add("--tags");
			}
			gitCommand.Add("--progress");
			monitor.Update(0.0, "Fetching...");
			(string, string)[] env = new(string, string)[1] { (Consts.Env.NoPrompt, noPrompt ? "1" : "0") };
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor, isBt: false);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(gitCommand).Env(env).ExecuteWithCallback(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			string text = processOutputHandler.Stderr();
			if (!executeWithCallbackResponse.Result.Success)
			{
				if (GitCommandError.TagMismatch.Match(text))
				{
					return GitCommandResult.Failure(new GitCommandError.TagMismatch(new GitRequestResult(executeWithCallbackResponse.Result.ExitCode, "", text), remote));
				}
				Match match = NoWiFiRegEx.FirstMatch(text);
				if (match != null)
				{
					string text2 = match.Groups[1].Value.TrimEnd();
					monitor.Fail("Could not resolve host '" + text2 + "'");
				}
				else
				{
					monitor.Fail(text);
				}
				return GitCommandResult.Failure(new GitCommandError.CallbackUnknownError(processOutputHandler.FullOutput()));
			}
			MatchCollection matchCollection = UpdatedRegEx.Matches(text);
			int num = 0;
			for (int i = 0; i < matchCollection.Count; i++)
			{
				if (!matchCollection[i].Groups[1].Value.StartsWith("[up to date]"))
				{
					num++;
				}
			}
			if (num > 0)
			{
				string resultMessage = ((num == 1) ? $"Fetched {num} branch" : $"Fetched {num} branches");
				monitor.Success(resultMessage);
			}
			else
			{
				monitor.AppendOutputLine("Already up to date");
				monitor.Success("Already up to date");
			}
			return GitCommandResult.Success();
		}
	}
}
