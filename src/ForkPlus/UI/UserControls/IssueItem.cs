using System;
using ForkPlus.Accounts;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public class IssueItem : MultiselectionTreeViewItem
	{
		private Issue Issue { get; }

		public IssueState State => Issue.State;

		[Null]
		public string AvatarUrl => Issue.AssigneeAvatarUrl;

		public string AvatarTooltip { get; }

		public string IssueNumber { get; }

		public bool ShowBorder { get; }

		public override bool IsFocusable => false;

		public IssueItem(Issue issue)
		{
			Issue = issue;
			base.Title = issue.Title;
			IssueNumber = "#" + issue.Id;
			ShowBorder = true;
			AvatarTooltip = CreateAvatarTooltip(issue);
		}

		public void OpenInBrowser()
		{
			new Uri(Issue.WebUrl).OpenInBrowser();
		}

		[Null]
		private static string CreateAvatarTooltip(Issue issue)
		{
			string assigneeUsername = issue.AssigneeUsername;
			if (assigneeUsername != null)
			{
				string assigneeName = issue.AssigneeName;
				if (assigneeName != null && assigneeUsername != assigneeName)
				{
					return "Assignee: " + assigneeName + " (" + assigneeUsername + ")";
				}
				return "Assignee: " + assigneeUsername;
			}
			return null;
		}
	}
}
