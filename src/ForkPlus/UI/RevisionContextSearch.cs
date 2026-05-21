using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public struct RevisionContextSearch
	{
		private readonly HashSet<Sha> _matches;

		public int MatchCount => _matches.Count;

		public string SearchString { get; }

		public HashSet<Sha> Matches => _matches;

		public RevisionContextSearch(string searchString, HashSet<Sha> matches)
		{
			SearchString = searchString;
			_matches = matches;
		}

		public bool IsMatch(Sha sha)
		{
			return _matches.Contains(sha);
		}
	}
}
