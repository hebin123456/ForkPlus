using System;
using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.Git.Commands
{
	public class RepositoryStats
	{
		public DateTime Start { get; }

		public DateTime End { get; }

		public Dictionary<DayOfWeek, int> CommitsByDayOfWeek { get; }

		public Dictionary<int, int> CommitsByHourOfDay { get; }

		public Dictionary<DateTime, DayContributionInfo> CommitsByDate { get; }

		public AuthorStats[] AuthorStat { get; }

		public RepositoryStats(DateTime start, DateTime end, AuthorStats[] authorStat, Dictionary<DayOfWeek, int> commitsByDayOfWeek, Dictionary<int, int> commitsByHourOfDay, Dictionary<DateTime, DayContributionInfo> commitsByDate)
		{
			Start = start;
			End = end;
			CommitsByDayOfWeek = commitsByDayOfWeek;
			CommitsByHourOfDay = commitsByHourOfDay;
			CommitsByDate = commitsByDate;
			AuthorStat = authorStat;
		}
	}

	// Per-day contribution breakdown for the heatmap tooltip. Authors are kept
	// unique and ordered by commit count (desc) so the tooltip can list the most
	// active contributors first.
	public class DayContributionInfo
	{
		public int Commits { get; }

		public Dictionary<string, int> CommitsByAuthor { get; }

		public DayContributionInfo()
		{
			Commits = 0;
			CommitsByAuthor = new Dictionary<string, int>();
		}

		public DayContributionInfo(int commits, Dictionary<string, int> commitsByAuthor)
		{
			Commits = commits;
			CommitsByAuthor = commitsByAuthor ?? new Dictionary<string, int>();
		}

		public DayContributionInfo AddCommit(string author)
		{
			Dictionary<string, int> dict = new Dictionary<string, int>(CommitsByAuthor)
			{
				[author ?? ""] = (CommitsByAuthor.TryGetValue(author ?? "", out var n) ? n : 0) + 1
			};
			return new DayContributionInfo(Commits + 1, dict);
		}

		public List<string> GetTopAuthors(int limit)
		{
			return CommitsByAuthor
				.OrderByDescending(kv => kv.Value)
				.ThenBy(kv => kv.Key)
				.Take(limit)
				.Select(kv => kv.Key)
				.Where(name => !string.IsNullOrEmpty(name))
				.ToList();
		}

		public int AuthorCount => CommitsByAuthor.Count;
	}
}
