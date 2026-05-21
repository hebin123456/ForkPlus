using System;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public static class WorktreeExtensions
	{
		public static string GetTooltip(this Worktree worktree)
		{
			if (worktree.HeadString.StartsWith("refs/heads/"))
			{
				string text = worktree.HeadString.Substring("refs/heads/".Length);
				return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine + "Location:\t\t" + worktree.Path + Environment.NewLine + "Branch:\t\t" + text;
			}
			return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine + "Location:\t\t" + worktree.Path + Environment.NewLine + "HEAD:\t\t" + worktree.HeadString.Abbreviated();
		}
	}
}
