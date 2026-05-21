using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionsInRangeGitCommand
	{
		public class Result
		{
			public Revision[] Revisions { get; }

			public Sha Src { get; }

			public Sha? Dst { get; }

			public Result(List<Revision> revisions, Sha src, Sha? dst)
			{
				Revisions = revisions.ToArray();
				Src = src;
				Dst = dst;
			}
		}

		public GitCommandResult<Result> Execute(GitModule gitModule, Sha one, Sha? other)
		{
			GitCommandResult<Result> gitCommandResult = ExecuteInternal(gitModule, one, other);
			if (gitCommandResult.Error is GitCommandError.NotFound && other.HasValue)
			{
				return ExecuteInternal(gitModule, other.Value, one);
			}
			return gitCommandResult;
		}

		private static GitCommandResult<Result> ExecuteInternal(GitModule gitModule, Sha dst, Sha? src)
		{
			string text = "F|!-";
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--pretty=format:%H" + text + "%s");
			if (src.HasValue)
			{
				gitCommand.Add(src.Value.ToString() + "~.." + dst);
			}
			else
			{
				gitCommand.Add("--max-count=1");
				gitCommand.Add(dst.ToString() ?? "");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (gitRequestResult.Success && gitRequestResult.Stdout.Length == 0)
			{
				return GitCommandResult<Result>.Failure(new GitCommandError.NotFound());
			}
			List<Revision> list = new List<Revision>();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Result>.Success(new Result(list, dst, src));
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new string[1] { text }, StringSplitOptions.None);
				if (array2.Length == 2)
				{
					if (!Sha.TryParse(array2[0], out var result))
					{
						return GitCommandResult<Result>.Failure(new GitCommandError.ParseError("Failed to parse SHA in '" + array2[0] + "'"));
					}
					string message = array2[1];
					list.Add(new Revision(result, new RevisionHeader(UserIdentity.Dummy, DateTime.Now, message, hasBody: false)));
				}
			}
			return GitCommandResult<Result>.Success(new Result(list, dst, src));
		}
	}
}
