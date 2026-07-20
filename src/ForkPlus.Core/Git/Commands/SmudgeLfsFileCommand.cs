using ForkPlus.Services;
using System.IO;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class SmudgeLfsFileCommand
	{
		public GitCommandResult<MemoryStream> Execute(GitModule gitModule, LfsPointer filePointer, JobMonitor monitor)
		{
			byte[] bytes = Encoding.Default.GetBytes(filePointer.StringPointer);
			using GitLfsProgressHandler gitLfsProgressHandler = new GitLfsProgressHandler(monitor);
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "lfs", "smudge");
			ShellRequestBinaryResult shellRequestBinaryResult = new GitRequest(gitModule).Command(command).Env(gitLfsProgressHandler.EnvironmentVariables).Stdin(bytes)
				.ExecuteBinary(monitor);
			if (!shellRequestBinaryResult.Success)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult<MemoryStream>.Failure(new GitCommandError.Cancelled());
				}
				return GitCommandResult<MemoryStream>.Failure(shellRequestBinaryResult.ToGitCommandError());
			}
			return GitCommandResult<MemoryStream>.Success(shellRequestBinaryResult.Stdout);
		}
	}
}
