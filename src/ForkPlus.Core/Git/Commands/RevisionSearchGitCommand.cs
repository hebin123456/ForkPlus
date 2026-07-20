using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RevisionSearchGitCommand
	{
		private static readonly string[] Separator = new string[1] { "±." };

		public GitCommandResult Execute(GitModule gitModule, Submodule[] submodules, RevisionSearchQuery query, Action<RevisionWithFiles> matchCallback, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("log");
			gitCommand.Add("--no-show-signature");
			gitCommand.Add("--pretty=format:%H±.%aN±.%aE±.%at±.%s");
			if (query.Scope == RevisionSearchScope.Repository)
			{
				gitCommand.Add("HEAD");
				gitCommand.Add("--branches");
				gitCommand.Add("--remotes");
				gitCommand.Add("--tags");
			}
			if (query.Type == RevisionSearchType.Author)
			{
				gitCommand.Add("--author=" + query.SearchString);
				gitCommand.Add("--regexp-ignore-case");
			}
			else if (query.Type == RevisionSearchType.Message)
			{
				gitCommand.Add("--grep=" + query.SearchString);
				gitCommand.Add("--regexp-ignore-case");
			}
			else if (query.Type == RevisionSearchType.DiffContent)
			{
				gitCommand.Add("--name-status");
				gitCommand.Add("-G");
				gitCommand.Add(Escape(query.SearchString));
			}
			else if (query.Type == RevisionSearchType.DiffPath)
			{
				gitCommand.Add("--name-status");
			}
			gitCommand.Add("--");
			if (query.Type == RevisionSearchType.DiffPath)
			{
				gitCommand.Add(":(icase)" + query.SearchString);
			}
			Revision revision = null;
			List<ChangedFile> changedFiles = new List<ChangedFile>(16);
			Action<string> stdoutPipeHandler = ((query.Type != RevisionSearchType.DiffPath && query.Type != RevisionSearchType.DiffContent) ? ((Action<string>)delegate(string line)
			{
				Parse(line, matchCallback);
			}) : ((Action<string>)delegate(string line)
			{
				Parse(line, submodules, ref revision, changedFiles, matchCallback);
			}));
			ProcessOutputHandler outputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(gitCommand).ExecuteWithCallbackBt(stdoutPipeHandler, delegate(string l)
			{
				outputHandler.StderrHandler(l);
				Log.Warn(l);
			}, monitor);
			if (revision != null)
			{
				matchCallback(new RevisionWithFiles(revision, changedFiles.ToArray()));
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				return GitCommandResult.Failure(new GitCommandError.GitError(outputHandler.FullOutput(), outputHandler.Stderr()));
			}
			return GitCommandResult.Success();
		}

		private static string Escape(string searchString)
		{
			return searchString.Replace("\"", "\\\"");
		}

		private static void Parse(string line, Action<RevisionWithFiles> matchCallback)
		{
			string[] lines = line.Split(Separator, StringSplitOptions.None);
			int i = 0;
			Revision revision = RevisionParser.ParseRevision(lines, ref i);
			if (revision != null)
			{
				matchCallback(new RevisionWithFiles(revision, new ChangedFile[0]));
			}
		}

		private static void Parse(string line, Submodule[] submodules, ref Revision revision, List<ChangedFile> changedFiles, Action<RevisionWithFiles> matchCallback)
		{
			if (line == "")
			{
				if (revision != null)
				{
					matchCallback(new RevisionWithFiles(revision, changedFiles.ToArray()));
					revision = null;
					changedFiles.Clear();
				}
			}
			else if (line.Length > 41 && line[40] == '±')
			{
				if (revision != null)
				{
					matchCallback(new RevisionWithFiles(revision, changedFiles.ToArray()));
					revision = null;
					changedFiles.Clear();
				}
				string[] lines = line.Split(Separator, StringSplitOptions.None);
				int i = 0;
				revision = RevisionParser.ParseRevision(lines, ref i);
			}
			else
			{
				ChangedFile changedFile = RevisionParser.ReadChangedFile(line, submodules);
				if (changedFile != null)
				{
					changedFiles.Add(changedFile);
				}
			}
		}
	}
}
