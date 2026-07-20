using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRecentRevisionsGitCommand
	{
		public class RecentRevision
		{
			public string Sha { get; }

			public UserIdentity Author { get; }

			public DateTime AuthorDate { get; }

			public string Subject { get; }

			public RecentRevision(string sha, UserIdentity author, DateTime authorDate, string subject)
			{
				Sha = sha;
				Author = author;
				AuthorDate = authorDate;
				Subject = subject;
			}
		}

		public GitCommandResult<RecentRevision[]> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--no-show-signature", "--all", "-n 5", "--date-order", "--pretty=format:%H%n%an%n%ae%n%at%n%s", "--").Execute();
			if (!gitRequestResult.Success)
			{
				// 空仓库（无 commit）时 git log 会失败，stderr 含 "does not have any commits" 或 "unknown revision"；
				// 这类情况返回空数组，其他真实错误（unsafe repository、git 缺失等）记录日志便于排查。
				string stderr = gitRequestResult.Stderr ?? "";
				if (stderr.Contains("does not have any commits") || stderr.Contains("unknown revision") || stderr.Contains("bad revision"))
				{
					return GitCommandResult<RecentRevision[]>.Success(new RecentRevision[0]);
				}
				Log.Error("GetRecentRevisions failed: " + stderr);
				return GitCommandResult<RecentRevision[]>.Success(new RecentRevision[0]);
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine);
			List<RecentRevision> list = new List<RecentRevision>(array.Length / 4);
			int num = 0;
			string sha = "";
			string name = "";
			string email = "";
			DateTime authorDate = default(DateTime);
			string text = "";
			foreach (string text2 in array)
			{
				switch (num)
				{
				case 0:
					sha = text2;
					break;
				case 1:
					name = text2;
					break;
				case 2:
					email = text2;
					break;
				case 3:
				{
					if (DateTimeHelper.TryParseUnixDate(text2, out var result))
					{
						authorDate = result;
						break;
					}
					Log.Error("Cannot parse author date in '" + text2 + "'");
					return GitCommandResult<RecentRevision[]>.Success(new RecentRevision[0]);
				}
				case 4:
					text = text2;
					list.Add(new RecentRevision(sha, new UserIdentity(name, email), authorDate, text));
					num = -1;
					break;
				default:
					Log.Error($"Cannot reach here. Invalid part number: '{num}'.");
					return GitCommandResult<RecentRevision[]>.Success(new RecentRevision[0]);
				}
				num++;
			}
			return GitCommandResult<RecentRevision[]>.Success(list.ToArray());
		}
	}
}
