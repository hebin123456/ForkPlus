using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	public struct RepositoryOverviewData
	{
		public Dictionary<string, List<int>> Files { get; }

		public Sha[] Shas { get; }

		public DateTime[] AuthorDates { get; }

		public int[] Authors { get; }

		public UserIdentity[] UserIdentities { get; }

		public RepositoryOverviewData(Dictionary<string, List<int>> files, Sha[] shas, DateTime[] authorDates, int[] authors, UserIdentity[] userIdentities)
		{
			Files = files;
			Shas = shas;
			AuthorDates = authorDates;
			Authors = authors;
			UserIdentities = userIdentities;
		}
	}
}
