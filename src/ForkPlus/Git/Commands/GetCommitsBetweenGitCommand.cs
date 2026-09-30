using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// v3.13.0（WS5/WS2.2）：统计 <from>..<to> 范围内 to 侧独有的提交，一次调用返回
	// count（rev-list --count）与提交列表（log -n <limit>，%h+%s，新→旧，可截断）。
	// PullWindow 拉取预览（from=本地基线, to=远程分支）与 PushWindow 强推覆盖预览共用。
	public class GetCommitsBetweenGitCommand
	{
		public const int DefaultLimit = 50;

		public class CommitPreview
		{
			public string Sha { get; }

			/// <summary>完整 40 位 SHA（%H）。Sha 为 %h 短 SHA 仅供显示；
			/// 打开 RevisionDetailsWindow 需要 Sha.Parse 可解析的完整值。</summary>
			public string FullSha { get; }

			public string Subject { get; }

			public CommitPreview(string sha, string subject, string fullSha)
			{
				Sha = sha;
				Subject = subject;
				FullSha = fullSha;
			}
		}

		public class CommitsBetweenResult
		{
			public int Count { get; }

			public CommitPreview[] Commits { get; }

			public CommitsBetweenResult(int count, CommitPreview[] commits)
			{
				Count = count;
				Commits = commits;
			}
		}

		public GitCommandResult<CommitsBetweenResult> Execute(GitModule gitModule, string from, string to, int limit = DefaultLimit)
		{
			string range = from + ".." + to;
			GitRequestResult countRequestResult = new GitRequest(gitModule).Command("rev-list", "--count", range).Execute();
			if (!countRequestResult.Success)
			{
				return GitCommandResult<CommitsBetweenResult>.Failure(countRequestResult.ToGitCommandError());
			}
			string text = countRequestResult.Stdout.Trim();
			if (!int.TryParse(text, out var count))
			{
				return GitCommandResult<CommitsBetweenResult>.Failure(new GitCommandError.ParseError("Cannot parse '" + text + "'"));
			}
			if (count == 0)
			{
				return GitCommandResult<CommitsBetweenResult>.Success(new CommitsBetweenResult(0, Array.Empty<CommitPreview>()));
			}
			string text2 = "F|!-";
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--pretty=format:%h" + text2 + "%H" + text2 + "%s");
			gitCommand.Add("-n", Math.Max(1, limit).ToString());
			gitCommand.Add(range);
			GitRequestResult logRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!logRequestResult.Success)
			{
				return GitCommandResult<CommitsBetweenResult>.Failure(new GitCommandError.GitError(logRequestResult.Stdout, logRequestResult.Stderr));
			}
			List<CommitPreview> list = new List<CommitPreview>();
			string[] array = logRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new string[1] { text2 }, StringSplitOptions.None);
				if (array2.Length == 3)
				{
					list.Add(new CommitPreview(array2[0], array2[2], array2[1]));
				}
			}
			return GitCommandResult<CommitsBetweenResult>.Success(new CommitsBetweenResult(count, list.ToArray()));
		}
	}
}
