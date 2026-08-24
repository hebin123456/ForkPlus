using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// v3.12.0：查询 head 相对 upstream 未推送的提交（upstream..head），
	// 供 PushWindow 的"推送前 Squash"使用。返回顺序：新 → 旧。
	public class GetUnpushedCommitsGitCommand
	{
		public class UnpushedCommit
		{
			public Sha Sha { get; }

			public string Subject { get; }

			public UnpushedCommit(Sha sha, string subject)
			{
				Sha = sha;
				Subject = subject;
			}
		}

		public GitCommandResult<UnpushedCommit[]> Execute(GitModule gitModule, Sha head, Sha upstream)
		{
			string text = "F|!-";
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--pretty=format:%H" + text + "%s");
			gitCommand.Add(upstream.ToString() + ".." + head.ToString());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<UnpushedCommit[]>.Failure(new GitCommandError.GitError(gitRequestResult.Stdout, gitRequestResult.Stderr));
			}
			List<UnpushedCommit> list = new List<UnpushedCommit>();
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new string[1] { text }, StringSplitOptions.None);
				if (array2.Length == 2 && Sha.TryParse(array2[0], out var sha))
				{
					list.Add(new UnpushedCommit(sha, array2[1]));
				}
			}
			return GitCommandResult<UnpushedCommit[]>.Success(list.ToArray());
		}
	}
}
