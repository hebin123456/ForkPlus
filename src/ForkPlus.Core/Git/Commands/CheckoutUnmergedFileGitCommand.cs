using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class CheckoutUnmergedFileGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, TempFileManager tempFileManager, string path, UnmergedFileVersionType version)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("checkout-index", "--temp", $"--stage={(int)version}", "--", path.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			int num = gitRequestResult.Stdout.IndexOf(Consts.Chars.TabChar);
			if (num == -1)
			{
				return GitCommandResult<string>.Failure(new GitCommandError.GitError(gitRequestResult.Stdout));
			}
			string relativePath = gitRequestResult.Stdout.Substring(0, num);
			string path2 = Path.GetFileNameWithoutExtension(path) + "~" + version.ToString() + Path.GetExtension(path);
			string tempFilePath = tempFileManager.GetTempFilePath(path2);
			try
			{
				File.Delete(tempFilePath);
				File.Move(gitModule.MakePath(relativePath), tempFilePath);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to move '" + gitModule.MakePath(relativePath) + "' to '" + tempFilePath + "'", ex);
				return GitCommandResult<string>.Failure(new GitCommandError.FileIsBusy(tempFilePath));
			}
			return GitCommandResult<string>.Success(tempFilePath);
		}
	}
}
