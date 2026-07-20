using System.Collections.Generic;

namespace ForkPlus.Git
{
	internal class RevisionSidebarSearch
	{
		private readonly HashSet<Sha> _matches;

		private readonly RevisionVisualGraph _revisionVisualGraph;

		private int _lastSearchMatchIndex;

		public RevisionSearchQuery Query { get; }

		public RevisionSidebarSearch(RevisionVisualGraph revisionsView, RevisionSearchQuery query)
		{
			Query = query;
			_revisionVisualGraph = revisionsView;
			_matches = new HashSet<Sha>();
		}

		public int AddSearchMatch(Sha sha)
		{
			for (int i = _lastSearchMatchIndex; i < _revisionVisualGraph.Count; i++)
			{
				if (_revisionVisualGraph.GetShaAtRow(i) == sha)
				{
					_matches.Add(sha);
					_lastSearchMatchIndex = i;
					return i;
				}
			}
			for (int j = 0; j < _lastSearchMatchIndex; j++)
			{
				if (_revisionVisualGraph.GetShaAtRow(j) == sha)
				{
					_matches.Add(sha);
					_lastSearchMatchIndex = j;
					return j;
				}
			}
			return -1;
		}

		public bool Match(Sha sha)
		{
			return _matches.Contains(sha);
		}
	}
}
