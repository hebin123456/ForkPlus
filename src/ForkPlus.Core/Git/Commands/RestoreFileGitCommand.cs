using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RestoreFileGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, string sha, string filepath, string destination, JobMonitor monitor)
		{
			Log.Info("Restore " + filepath + " at " + sha.Abbreviated() + " to " + destination);
			try
			{
				Directory.CreateDirectory(destination);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create directory at '" + destination + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
			string text = Path.Combine(destination, Path.GetFileName(filepath));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("show", sha + ":" + filepath.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.StartsWith("fatal:") && (gitRequestResult.Stderr.Contains("does not exist in") || gitRequestResult.Stderr.Contains("exists on disk, but not in")))
				{
					if (!SaveFile(text, new MemoryStream()))
					{
						return GitCommandResult<string>.Failure(new GitCommandError.GenericError("Cannot save file '" + text + "'"));
					}
					return GitCommandResult<string>.Success(text);
				}
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			MemoryStream data;
			if (IsLfsContent(gitRequestResult.Stdout))
			{
				LfsPointer filePointer = LfsPointer.Parse(gitRequestResult?.Stdout);
				GitCommandResult<MemoryStream> gitCommandResult = new SmudgeLfsFileCommand().Execute(gitModule, filePointer, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<string>.Failure(gitCommandResult.Error);
				}
				data = gitCommandResult.Result;
			}
			else
			{
				ShellRequestBinaryResult shellRequestBinaryResult = new GitRequest(gitModule).Command("cat-file", "--filters", (sha + ":" + filepath).Quotify()).ExecuteBinary(null, silent: true);
				if (!shellRequestBinaryResult.Success)
				{
					return GitCommandResult<string>.Failure(shellRequestBinaryResult.ToGitCommandError());
				}
				data = shellRequestBinaryResult.Stdout;
			}
			if (!SaveFile(text, data))
			{
				return GitCommandResult<string>.Failure(new GitCommandError.GenericError("Cannot save file '" + text + "'"));
			}
			return GitCommandResult<string>.Success(text);
		}

		private static bool IsLfsContent([Null] string output)
		{
			if (output == null)
			{
				return false;
			}
			if (output.Length <= 120 || output.Length >= 1024)
			{
				return false;
			}
			if (!output.Contains("version https://git-lfs.github.com/spec/v1"))
			{
				return false;
			}
			if (!output.Contains("oid sha256:"))
			{
				return false;
			}
			return true;
		}

		private static bool SaveFile(string filePath, [Null] MemoryStream data)
		{
			byte[] array = data?.ToArray();
			if (array == null)
			{
				return false;
			}
			try
			{
				File.WriteAllBytes(filePath, array);
			}
			catch (Exception arg)
			{
				Log.Error($"Cannot save file: {arg}");
				return false;
			}
			return true;
		}
	}
}
