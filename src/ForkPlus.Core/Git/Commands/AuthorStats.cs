using System;

namespace ForkPlus.Git.Commands
{
	public struct AuthorStats
	{
		public string Name { get; }

		public int TotalCommits { get; }

		public Tuple<DateTime, int>[] CommitsByDate { get; }

		public AuthorStats(string name, int totalCommits, Tuple<DateTime, int>[] commitsByDate)
		{
			Name = name;
			TotalCommits = totalCommits;
			CommitsByDate = commitsByDate;
		}
	}
}
