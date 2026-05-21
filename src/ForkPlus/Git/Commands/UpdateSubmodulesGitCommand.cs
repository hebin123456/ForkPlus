using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class UpdateSubmodulesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, SubmodulesToUpdate oldSubmodules, JobMonitor monitor, string referenceGitDir = null)
		{
			GitCommandResult<Submodule[]> gitCommandResult = new GetSubmodulesGitCommand().Execute(gitModule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult.Error);
			}
			Submodule[] result = gitCommandResult.Result;
			List<Submodule> list = new List<Submodule>();
			Tuple<Submodule, bool>[] submodules = oldSubmodules.Submodules;
			for (int i = 0; i < submodules.Length; i++)
			{
				var (oldSubmodule, flag2) = submodules[i];
				if (oldSubmodule.IsActive && !flag2 && result.ContainsItem((Submodule existing) => oldSubmodule.Path == existing.Path))
				{
					list.Add(oldSubmodule);
				}
			}
			Submodule[] array = result;
			foreach (Submodule newSubmodule in array)
			{
				if (newSubmodule.IsActive && !oldSubmodules.Submodules.ContainsItem((Tuple<Submodule, bool> old) => newSubmodule.Path == old.Item1.Path))
				{
					list.Add(newSubmodule);
				}
			}
			if (list.Count == 0)
			{
				return GitCommandResult.Success();
			}
			if (referenceGitDir != null)
			{
				return ExecuteWithReference(gitModule, list, referenceGitDir, monitor);
			}
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "submodule", "update", "--init", "--recursive", "--progress", "--");
			foreach (Submodule item in list)
			{
				gitCommand.Add(item.Path.Quotify());
			}
			return ExecuteUpdate(gitModule, gitCommand, monitor);
		}

		public GitCommandResult Execute(GitModule gitModule, Submodule[] submodules, JobMonitor monitor)
		{
			if (submodules.Length == 0)
			{
				return GitCommandResult.Success();
			}
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "submodule", "update", "--init", "--recursive", "--progress", "--");
			foreach (Submodule submodule in submodules)
			{
				gitCommand.Add(submodule.Path.Quotify());
			}
			return ExecuteUpdate(gitModule, gitCommand, monitor);
		}

		public GitCommandResult ExecuteAll(GitModule gitModule, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(App.OverrideCredentialHelper, "submodule", "update", "--init", "--recursive", "--progress");
			return ExecuteUpdate(gitModule, command, monitor);
		}

		private GitCommandResult ExecuteWithReference(GitModule gitModule, List<Submodule> submodules, string referenceGitDir, JobMonitor monitor)
		{
			foreach (Submodule submodule in submodules)
			{
				GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "submodule", "update", "--init", "--recursive", "--progress");
				string text = Path.Combine(referenceGitDir, "modules", submodule.Path);
				if (Directory.Exists(text))
				{
					gitCommand.AddRange("--reference", text.Quotify());
				}
				gitCommand.Add("--", submodule.Path.Quotify());
				GitCommandResult gitCommandResult = ExecuteUpdate(gitModule, gitCommand, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult;
				}
			}
			return GitCommandResult.Success();
		}

		private GitCommandResult ExecuteUpdate(GitModule gitModule, GitCommand command, JobMonitor monitor)
		{
			ProcessOutputHandler processOutputHandler = new ProcessOutputHandler(monitor);
			ExecuteWithCallbackResponse executeWithCallbackResponse = new GitRequest(gitModule).Command(command).ExecuteWithCallback(processOutputHandler.StdoutHandler, processOutputHandler.StderrHandler, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			ISpawnError error = executeWithCallbackResponse.Error;
			if (error != null)
			{
				monitor.Fail("submodule update failed");
				return GitCommandResult.Failure(error.ToGitCommandError());
			}
			if (!executeWithCallbackResponse.Result.Success)
			{
				monitor.Fail(processOutputHandler.Stderr());
				GitCommandError.UnsafeRepository unsafeRepository = GitCommandError.UnsafeRepository.Test(processOutputHandler.FullOutput(), processOutputHandler.Stderr(), gitModule.Path);
				if (unsafeRepository != null)
				{
					return GitCommandResult.Failure(unsafeRepository);
				}
				return GitCommandResult.Failure(new GitCommandError.GitError(processOutputHandler.FullOutput(), processOutputHandler.Stderr()));
			}
			return GitCommandResult.Success();
		}
	}
}
