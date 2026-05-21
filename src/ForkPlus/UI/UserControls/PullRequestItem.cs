using System;
using ForkPlus.Accounts;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class PullRequestItem : MultiselectionTreeViewItem
	{
		private PullRequest PullRequest { get; }

		public PullRequestState State => PullRequest.State;

		[Null]
		public string SourceBranch => PullRequest.SourceBranch;

		[Null]
		public string AvatarUrl => PullRequest.AuthorAvatarUrl;

		public string AvatarTooltip { get; }

		public string PullRequestNumber { get; }

		public bool ShowBorder { get; }

		public override bool IsFocusable => false;

		public PullRequestItem(PullRequest pullRequest)
		{
			PullRequest = pullRequest;
			base.Title = pullRequest.Title;
			PullRequestNumber = "#" + pullRequest.Id;
			ShowBorder = true;
			AvatarTooltip = CreateAvatarTooltip(pullRequest);
			string authorName = PullRequest.AuthorName;
			if (authorName != null && PullRequest.AuthorUsername != authorName)
			{
				AvatarTooltip = "Author: " + authorName + " (" + PullRequest.AuthorUsername + ")";
			}
			else
			{
				AvatarTooltip = "Author: " + PullRequest.AuthorUsername;
			}
		}

		public void OpenInBrowser()
		{
			new Uri(PullRequest.WebUrl).OpenInBrowser();
		}

		[Null]
		private static string CreateAvatarTooltip(PullRequest pullRequest)
		{
			string authorName = pullRequest.AuthorName;
			if (authorName != null && pullRequest.AuthorName != authorName)
			{
				return "Author: " + authorName + " (" + pullRequest.AuthorName + ")";
			}
			return "Author: " + pullRequest.AuthorName;
		}
	}
}
