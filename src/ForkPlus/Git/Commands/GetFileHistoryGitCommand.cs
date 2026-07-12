using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetFileHistoryGitCommand
	{
		public GitCommandResult<RevisionWithFiles[]> Execute(GitModule gitModule, Submodule[] submodules, string filePath, Sha? parentSha)
		{
			GitCommand gitCommand = new GitCommand("-c", "core.quotePath=false", "log", "--no-show-signature", "--oneline", "--name-status", "--follow", "--pretty=format:" + RevisionParser.Format);
			if (parentSha.HasValue)
			{
				gitCommand.Add(parentSha.Value.ToString());
			}
			gitCommand.Add("--");
			gitCommand.Add(filePath.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<RevisionWithFiles[]>.Failure(new GitCommandError.GitError(gitRequestResult.Stderr));
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine);
			List<RevisionWithFiles> list = new List<RevisionWithFiles>(array.Length / 6);
			for (int i = 0; i < array.Length; i++)
			{
				RevisionWithFiles revisionWithFiles = RevisionParser.ParseRevisionWithFiles(array, submodules, ref i);
				if (revisionWithFiles == null)
				{
					return GitCommandResult<RevisionWithFiles[]>.Failure(new GitCommandError.ParseError("Cannot parse revision"));
				}
				list.Add(revisionWithFiles);
			}
			return GitCommandResult<RevisionWithFiles[]>.Success(list.ToArray());
		}

		public GitCommandResult<(RevisionWithFiles[], string[])> Execute(GitModule gitModule, string filePath, Range lineRange, Sha? sha)
		{
			// -L 语法为 start,end:file；filePath 含空格或特殊字符时需转义并加引号，避免被 git 当作多个参数或语法错误。
			string escapedFilePath = "\"" + (filePath ?? "").Replace("\"", "\\\"") + "\"";
			GitCommand gitCommand = new GitCommand("-c", "core.quotepath=false", "log", "--no-show-signature", "--pretty=format:--ForkRevisionHeaderStart--%n" + RevisionParser.Format + "%n--ForkRevisionHeaderEnd--", "-L", $"{lineRange.Start},{lineRange.End}:{escapedFilePath}");
			if (sha.HasValue)
			{
				gitCommand.Add(sha.Value.ToString());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<(RevisionWithFiles[], string[])>.Failure(gitRequestResult.ToGitCommandError());
			}
			string stdout = gitRequestResult.Stdout;
			List<RevisionWithFiles> list = new List<RevisionWithFiles>(4);
			List<string> list2 = new List<string>(4);
			int num = stdout.IndexOf("--ForkRevisionHeaderStart--\n", StringComparison.Ordinal);
			num = ((num != -1) ? (num + "--ForkRevisionHeaderStart--\n".Length) : stdout.Length);
			while (num < stdout.Length)
			{
				int num2 = stdout.IndexOf("--ForkRevisionHeaderEnd--\n", num, StringComparison.Ordinal);
				if (num2 == -1)
				{
					break;
				}
				string text = stdout.Substring(num, num2 - num);
				string[] lines = text.Split('\n');
				int i = 0;
				Revision revision = RevisionParser.ParseRevision(lines, ref i);
				if (revision == null)
				{
					return GitCommandResult<(RevisionWithFiles[], string[])>.Failure(new ParseException("Failed to parse revision in '" + text + "'"));
				}
				RevisionWithFiles item = new RevisionWithFiles(revision, new ChangedFile[0]);
				int num3 = num2 + "--ForkRevisionHeaderEnd--\n".Length;
				int num4 = stdout.IndexOf("\n--ForkRevisionHeaderStart--\n", num3, StringComparison.Ordinal);
				if (num4 != -1)
				{
					list.Add(item);
					list2.Add(stdout.Substring(num3, num4 - num3));
					num = num4 + "\n--ForkRevisionHeaderStart--\n".Length;
				}
				else
				{
					list.Add(item);
					list2.Add(stdout.Substring(num3));
					num = stdout.Length;
				}
			}
			return GitCommandResult<(RevisionWithFiles[], string[])>.Success((list.ToArray(), list2.ToArray()));
		}
	}
}
