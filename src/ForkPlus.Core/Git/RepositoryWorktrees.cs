using ForkPlus.Services;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class RepositoryWorktrees
	{
		public static readonly RepositoryWorktrees Empty = new RepositoryWorktrees(null, new Worktree[0]);

		[Null]
		public Worktree? MainWorktree { get; }

		public Worktree[] Items { get; }

		public Dictionary<string, Worktree> WorktreesByFullReference { get; }

		public bool IsEnabled
		{
			get
			{
				if (MainWorktree.HasValue || Items.Length != 0)
				{
					return true;
				}
				return ServiceLocator.UserSettings.ShowWorktrees;
			}
		}

		public RepositoryWorktrees([Null] Worktree? mainWorktree, Worktree[] linkedWorktrees)
		{
			MainWorktree = mainWorktree;
			Items = linkedWorktrees;
			int capacity = linkedWorktrees.Length + (mainWorktree.HasValue ? 1 : 0);
			WorktreesByFullReference = new Dictionary<string, Worktree>(capacity);
			if (mainWorktree.HasValue)
			{
				Worktree valueOrDefault = mainWorktree.GetValueOrDefault();
				WorktreesByFullReference[valueOrDefault.HeadString] = valueOrDefault;
			}
			for (int i = 0; i < linkedWorktrees.Length; i++)
			{
				WorktreesByFullReference[linkedWorktrees[i].HeadString] = linkedWorktrees[i];
			}
		}

		public bool DataEquals(RepositoryWorktrees other)
		{
			if (!NullableWorktreeEquals(MainWorktree, other.MainWorktree))
			{
				return false;
			}
			if (Items.Length != other.Items.Length)
			{
				return false;
			}
			for (int i = 0; i < Items.Length; i++)
			{
				if (!Items[i].DataEquals(other.Items[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool NullableWorktreeEquals([Null] Worktree? a, [Null] Worktree? b)
		{
			if (!a.HasValue && !b.HasValue)
			{
				return true;
			}
			if (!a.HasValue || !b.HasValue)
			{
				return false;
			}
			return a.Value.DataEquals(b.Value);
		}
	}
}
