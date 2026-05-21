using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ForkPlus.Git.Commands
{
	public static class RepositoryOverviewDataExtensions
	{
		public static Sha[] GetShas(this RepositoryOverviewData data, CalendarDateRange dateRange, string filepath)
		{
			HashSet<int> hashSet = new HashSet<int>();
			foreach (KeyValuePair<string, List<int>> file in data.Files)
			{
				if (!file.Key.StartsWith(filepath))
				{
					continue;
				}
				foreach (int item in file.Value)
				{
					if (dateRange.Contains(data.AuthorDates[item]))
					{
						hashSet.Add(item);
					}
				}
			}
			return (from x in hashSet
				orderby x
				select data.Shas[x]).ToArray();
		}

		public static (int, int, DateTime)[] GetAuthorStats(this RepositoryOverviewData data, CalendarDateRange dateRange, string filepath)
		{
			Dictionary<int, HashSet<int>> dictionary = new Dictionary<int, HashSet<int>>();
			foreach (KeyValuePair<string, List<int>> file in data.Files)
			{
				if (!file.Key.StartsWith(filepath))
				{
					continue;
				}
				foreach (int item2 in file.Value)
				{
					if (dateRange.Contains(data.AuthorDates[item2]))
					{
						int key = data.Authors[item2];
						HashSet<int> value;
						HashSet<int> hashSet2 = (dictionary.TryGetValue(key, out value) ? value : (dictionary[key] = new HashSet<int>()));
						hashSet2.Add(item2);
					}
				}
			}
			return dictionary.Select(delegate(KeyValuePair<int, HashSet<int>> x)
			{
				int key2 = x.Key;
				int count = x.Value.Count;
				int num = x.Value.Min();
				DateTime item = ((num != -1) ? data.AuthorDates[num] : default(DateTime));
				return (authorIndex: key2, commitCount: count, recentCommitDate: item);
			}).ToArray();
		}
	}
}
