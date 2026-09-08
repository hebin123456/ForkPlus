using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls
{
	public partial class ServiceTabItem : TabItem
	{
		// Migration note：同 SearchTabItem——ControlTheme 按 StyleKey 精确匹配，TabItem 子类
		// 匹配不到隐式 TabItem 主题 → 回落 ContentControl 主题，服务页（拉取请求/问题）
		// 整个 Content 平铺进 TabControl headerPanel（原版此功能隐藏时也不该出现）。
		// StyleKeyOverride=TabItem 恢复基类主题。
		protected override global::System.Type StyleKeyOverride => typeof(TabItem);

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
