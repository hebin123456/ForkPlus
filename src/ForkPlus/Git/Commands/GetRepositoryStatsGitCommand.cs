using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetRepositoryStatsGitCommand
	{
		internal class AuthorStatsComparer : IComparer<AuthorStats>
		{
			public int Compare(AuthorStats x, AuthorStats y)
			{
				return -1 * x.TotalCommits.CompareTo(y.TotalCommits);
			}
		}

		public GitCommandResult<RepositoryStats> Execute(GitModule gitModule, [Null] CalendarDateRange dateRange)
		{
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--date-order", "--pretty=format:" + RevisionParser.Format);
			if (dateRange != null)
			{
				gitCommand.Add($"--since={ConvertToUnixTime(dateRange.Start)}");
				gitCommand.Add($"--until={ConvertToUnixTime(dateRange.End)}");
			}
			gitCommand.Add("--");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<RepositoryStats>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine);
			List<Revision> list = new List<Revision>(array.Length / 6);
			int i = 0;
			while (i < array.Length)
			{
				Revision revision = RevisionParser.ParseRevision(array, ref i);
				if (revision != null)
				{
					list.Add(revision);
				}
			}
			if (list.Count <= 2)
			{
				return GitCommandResult<RepositoryStats>.Failure(new GitCommandError.GenericError("no changes"));
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			Dictionary<DayOfWeek, int> dictionary2 = new Dictionary<DayOfWeek, int>(7);
			for (int j = 0; j < 7; j++)
			{
				dictionary2[(DayOfWeek)j] = 0;
			}
			Dictionary<int, int> dictionary3 = new Dictionary<int, int>(24);
			for (int k = 0; k < 24; k++)
			{
				dictionary3[k] = 0;
			}
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Revision item in list)
			{
				DateTime authorDate = item.AuthorDate;
				hashSet.Add(item.Author.Name);
				string key = Key(item.Author.Name, authorDate);
				dictionary.TryGetValue(key, out var value);
				dictionary[key] = value + 1;
				dictionary2[authorDate.ToUniversalTime().DayOfWeek]++;
				dictionary3[authorDate.Hour]++;
			}
			DateTime dateTime = TrimTime(list[list.Count - 1].AuthorDate);
			DateTime dateTime2 = TrimTime(list[0].AuthorDate);
			List<AuthorStats> list2 = new List<AuthorStats>(1024);
			foreach (string item2 in hashSet)
			{
				List<Tuple<DateTime, int>> list3 = new List<Tuple<DateTime, int>>(128);
				int num = 0;
				DateTime dateTime3 = dateTime;
				while (dateTime3 <= dateTime2)
				{
					string key2 = Key(item2, dateTime3);
					if (!dictionary.TryGetValue(key2, out var value2))
					{
						value2 = 0;
					}
					num += value2;
					list3.Add(new Tuple<DateTime, int>(dateTime3, value2));
					dateTime3 = dateTime3.AddMonths(1);
				}
				list2.Add(new AuthorStats(item2, num, list3.ToArray()));
			}
			list2.Sort(new AuthorStatsComparer());
			return GitCommandResult<RepositoryStats>.Success(new RepositoryStats(dateTime, dateTime2, list2.ToArray(), dictionary2, dictionary3));
		}

		private static DateTime TrimTime(DateTime dateTime)
		{
			return new DateTime(dateTime.Year, dateTime.Month, 1);
		}

		private static string Key(string author, DateTime date)
		{
			return author + "_" + date.Year + "-" + date.Month;
		}

		private static long ConvertToUnixTime(DateTime datetime)
		{
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return (long)(datetime - dateTime).TotalSeconds;
		}
	}
}
