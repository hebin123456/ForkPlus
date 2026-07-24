// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → 移除（本文件无 Avalonia 命名空间下的类型引用）
// - using System.Windows.Controls → using Avalonia.Controls（TabItem、SelectionChangedEventArgs）
// - 移除 using System.Windows.Markup（WPF XAML 代码生成专用，Avalonia 不需要）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
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
			List<Remote> list = remotesWithService.Filter((Remote x) => x.Account.Service.SupportsIssues);
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
