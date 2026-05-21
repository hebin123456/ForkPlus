using System.IO;
using System.Text;

namespace ForkPlus.Git.Commands
{
	public class GetFileContentGitCommand : GetFileChangesGitCommand
	{
		public GitCommandResult<Content> Execute(GitModule gitModule, Sha sha, string filePath)
		{
			GitCommandResult<MemoryStream> gitCommandResult = new GetBlobGitCommand().Execute(gitModule, new BlobTarget.Revision(sha.ToString(), filePath));
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<Content>.Failure(gitCommandResult.Error);
			}
			MemoryStream result = gitCommandResult.Result;
			if (result == null)
			{
				return GitCommandResult<Content>.Failure(new GitCommandError.Bug($"Blob '{sha}:{filePath}' not found"));
			}
			string text = DecodeString(result);
			if (text != null && GetFileChangesGitCommand.IsLfsContent(text))
			{
				LfsPointer lfsPointer = LfsPointer.Parse(text);
				if (lfsPointer != null)
				{
					BinaryFileType binaryFileType = ((!PathHelper.IsImagePath(filePath)) ? BinaryFileType.LfsBinaryFile : BinaryFileType.LfsImage);
					return GitCommandResult<Content>.Success(new LfsContent(filePath, isTracked: true, lfsPointer, binaryFileType));
				}
			}
			if (PathHelper.IsImagePath(filePath))
			{
				return GitCommandResult<Content>.Success(new ImageContent(filePath, isTracked: true, result));
			}
			if (text != null)
			{
				return GitCommandResult<Content>.Success(new TextContent(filePath, isTracked: true, text));
			}
			return GitCommandResult<Content>.Success(new BinaryContent(filePath, isTracked: true, result.Length));
		}

		[Null]
		private static string DecodeString(MemoryStream memoryStream)
		{
			int num = 0;
			byte[] array = memoryStream.ToArray();
			for (int i = 0; i < array.Length && i < 2000; i++)
			{
				if (array[i] == 0)
				{
					num++;
				}
			}
			if (num > 5)
			{
				return null;
			}
			try
			{
				string @string = Encoding.UTF8.GetString(array);
				if (@string != null)
				{
					return @string;
				}
				Log.Warn($"Cannot decode string in {memoryStream}");
				return null;
			}
			catch
			{
				Log.Warn($"Cannot decode string in {memoryStream}");
				return null;
			}
		}
	}
}
