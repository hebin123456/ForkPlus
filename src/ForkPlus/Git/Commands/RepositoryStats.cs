using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	public class RepositoryStats
	{
		public DateTime Start { get; }

		public DateTime End { get; }

		public Dictionary<DayOfWeek, int> CommitsByDayOfWeek { get; }

		public Dictionary<int, int> CommitsByHourOfDay { get; }

		public Dictionary<DateTime, int> CommitsByDate { get; }

		public AuthorStats[] AuthorStat { get; }

		public RepositoryStats(DateTime start, DateTime end, AuthorStats[] authorStat, Dictionary<DayOfWeek, int> commitsByDayOfWeek, Dictionary<int, int> commitsByHourOfDay, Dictionary<DateTime, int> commitsByDate)
		{
			Start = start;
			End = end;
			CommitsByDayOfWeek = commitsByDayOfWeek;
			CommitsByHourOfDay = commitsByHourOfDay;
			CommitsByDate = commitsByDate;
			AuthorStat = authorStat;
		}
	}
}
