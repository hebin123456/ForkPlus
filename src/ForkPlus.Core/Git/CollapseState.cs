using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class CollapseState
	{
		public static readonly CollapseState Empty = new CollapseState(collapseAllMode: false, new HashSet<Sha>());

		public readonly bool CollapseAllMode;

		public readonly HashSet<Sha> ToggledShas;

		public CollapseState(bool collapseAllMode, HashSet<Sha> toggledShas)
		{
			CollapseAllMode = collapseAllMode;
			ToggledShas = toggledShas;
		}

		public bool IsCollapsed(Sha sha)
		{
			if (CollapseAllMode)
			{
				return !ToggledShas.Contains(sha);
			}
			return ToggledShas.Contains(sha);
		}

		public CollapseState Collapse(Sha sha)
		{
			HashSet<Sha> hashSet = new HashSet<Sha>(ToggledShas);
			if (!CollapseAllMode)
			{
				hashSet.Add(sha);
			}
			else
			{
				hashSet.Remove(sha);
			}
			return new CollapseState(CollapseAllMode, hashSet);
		}

		public CollapseState Expand(Sha sha)
		{
			HashSet<Sha> hashSet = new HashSet<Sha>(ToggledShas);
			if (CollapseAllMode)
			{
				hashSet.Add(sha);
			}
			else
			{
				hashSet.Remove(sha);
			}
			return new CollapseState(CollapseAllMode, hashSet);
		}

		public CollapseState CollapseAll()
		{
			return new CollapseState(collapseAllMode: true, new HashSet<Sha>());
		}

		public CollapseState ExpandAll()
		{
			return new CollapseState(collapseAllMode: false, new HashSet<Sha>());
		}
	}
}
