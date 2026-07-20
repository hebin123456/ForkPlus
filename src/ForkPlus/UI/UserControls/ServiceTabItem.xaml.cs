using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public partial class ServiceTabItem : TabItem
	{

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public ServiceTabItem()
		{
			InitializeComponent();
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
			PullRequestsTabItem.Initialize(repositoryUserControl);
			IssuesTabItem.Initialize(repositoryUserControl);
		}

		public void SetServices(Remote[] remotesWithService)
		{
			PullRequestsTabItem.SetServices(remotesWithService);
			List<Remote> list = remotesWithService.Filter((Remote x) => x.AccountConcrete.Service.SupportsIssues);
			if (list.Count > 0)
			{
				IssuesTabItem.Show();
				IssuesTabItem.SetServices(list.ToArray());
			}
			else
			{
				IssuesTabItem.Collapse();
			}
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			PullRequestsTabItem pullRequestsTabItem = e.AddedItems.FirstItem<PullRequestsTabItem>();
			if (pullRequestsTabItem != null)
			{
				pullRequestsTabItem?.OnActivated();
				return;
			}
			IssuesTabItem issuesTabItem = e.AddedItems.FirstItem<IssuesTabItem>();
			if (issuesTabItem != null)
			{
				issuesTabItem?.OnActivated();
			}
		}

	}
}
