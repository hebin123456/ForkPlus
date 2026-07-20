using System;
using System.IO;
using System.Text;

namespace ForkPlus.Git.Commands
{
	public class AddGitignorePatternGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string pattern)
		{
			try
			{
				AppendLineToFile(gitModule.MakePath(".gitignore"), pattern.Trim());
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			return GitCommandResult.Success();
		}

		private void AppendLineToFile(string filePath, string text)
		{
			string text2 = "";
			string value = "";
			using (FileStream stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
			{
				using StreamReader streamReader = new StreamReader(stream);
				text2 = streamReader.ReadToEnd();
				value = (text2.EndsWith("\n") ? "" : "\n");
			}
			using FileStream stream2 = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
			using StreamWriter streamWriter = new StreamWriter(stream2, Encoding.UTF8, 1024, leaveOpen: true);
			streamWriter.BaseStream.Seek(0L, SeekOrigin.End);
			if (text2 != "")
			{
				streamWriter.Write(value);
			}
			streamWriter.Write(text);
			streamWriter.Write("\n");
		}
	}
}
