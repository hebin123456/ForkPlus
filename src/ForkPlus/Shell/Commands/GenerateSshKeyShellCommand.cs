using System;
using System.IO;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Shell.Commands
{
	public class GenerateSshKeyShellCommand
	{
		public GitCommandResult Execute(string email, string keypath)
		{
			GitRequestResult gitRequestResult;
			try
			{
				string localSSHDirectory = SystemEnvironment.LocalSSHDirectory;
				Directory.CreateDirectory(localSSHDirectory);
				// Migration note：ssh-keygen 路径跨平台探测（Unix 内置 git 布局无 usr/bin/ssh-keygen.exe）。
				string path = SystemEnvironment.GetSshKeygenPath();
				gitRequestResult = default(GitRequest).Path(path).CurrentDir(localSSHDirectory).Command("-q", "-t", "ed25519", "-N", string.Empty, "-C", email, "-f", keypath)
					.ExecuteBt();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to generate ssh key", ex);
				return GitCommandResult.Failure(ex);
			}
			if (gitRequestResult.Success)
			{
				return GitCommandResult.Success();
			}
			return GitCommandResult.Failure(new GitCommandError.GitError(gitRequestResult));
		}
	}
}
