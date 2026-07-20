using System.IO;
using System.Text;

namespace ForkPlus.Git.Commands
{
	public class GetFileContentGitCommand : GetFileChangesGitCommand
	{
		/// <summary>
		/// v3.1.0：超过此阈值（10 MB）的二进制文件不自动加载 hex 内容，只返回 BinaryContent（仅大小）。
		/// 与 TextContent 的 1MB 阈值同量级但放宽，因为 hex 是固定宽度更省内存。
		/// 用户可在 FileContentControl 中通过 "Load Hex" 按钮强制加载。
		/// </summary>
		private const long MaxAutoLoadHexSize = 10 * 1024 * 1024;

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
			// v3.1.0：非图片二进制 → HexContent（携带字节，<= 10MB），否则只返回 BinaryContent（仅大小）
			if (result.Length <= MaxAutoLoadHexSize)
			{
				return GitCommandResult<Content>.Success(new HexContent(filePath, isTracked: true, result));
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
