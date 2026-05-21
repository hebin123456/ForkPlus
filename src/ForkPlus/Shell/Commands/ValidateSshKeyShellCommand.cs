using System;
using System.IO;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Shell.Commands
{
	public class ValidateSshKeyShellCommand
	{
		public enum Result
		{
			Success,
			IncorrectPassphrase
		}

		public GitCommandResult<Result> Execute(string keypath, [Null] string passphrase = null)
		{
			GitRequestResult gitRequestResult;
			try
			{
				string text = passphrase ?? "";
				string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(App.GitPath)), "usr", "bin", "ssh-keygen.exe");
				gitRequestResult = default(GitRequest).Path(path).Command("-y", "-P", text, "-f", keypath).ExecuteBt();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to validate SSH key", ex);
				return GitCommandResult<Result>.Failure(ex);
			}
			if (gitRequestResult.Success)
			{
				return GitCommandResult<Result>.Success(Result.Success);
			}
			string stderr = gitRequestResult.Stderr;
			if (stderr != null && stderr.Contains("incorrect passphrase supplied"))
			{
				return GitCommandResult<Result>.Success(Result.IncorrectPassphrase);
			}
			return GitCommandResult<Result>.Failure(new GitCommandError.GitError(gitRequestResult));
		}
	}
}
