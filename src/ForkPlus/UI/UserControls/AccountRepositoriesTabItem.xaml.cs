// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除（Avalonia code-behind 不需 IComponentConnector using）
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - RoutedEventArgs → Avalonia.Interactivity.RoutedEventArgs（新增 using Avalonia.Interactivity）
// - Dispatcher.Async 保持（自定义扩展方法 DispatcherExtension.Async，内部转发 Dispatcher.Post）
// TODO(4.5): account.ServiceType.Icon()（RemoteTypeBridgeExtensions.Icon，定义于 BridgeExtensions.cs，尚未迁移）
//            仍返回 WPF System.Windows.Media.ImageSource，赋值给 IImage _icon 类型不一致。
//            待 BridgeExtensions 迁移为返回 Avalonia.Media.IImage 后类型一致（参考 ReferencePanel 对 Remote.Icon 的处理）。
// TODO(4.5): AccountRepositoryItem（AccountRepositoryItem.cs，尚未迁移）构造函数仍接收 WPF System.Windows.Media.ImageSource。
//            待 AccountRepositoryItem 迁移为接收 Avalonia.Media.IImage 后，下方 new AccountRepositoryItem(x, icon) 类型一致。
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class AccountRepositoriesTabItem : TabItem
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly DelayedAction<string> _refreshFilterAction;

		private Account _account;

		private IImage _icon;

		private GitServiceRepository[] _repositories;

		public AccountRepositoriesTabItem()
		{
			InitializeComponent();
			FilterTextBox.FilterRequestChanged += FilterPanel_FilterRequestChanged;
			_refreshFilterAction = new DelayedAction<string>(UpdateList, 0.1);
		}

		public void Refresh(Account account)
		{
			_account = account;
			_icon = account.ServiceType.Icon();
			FallbackUserControl.FallbackMessage = Translate("Loading repositories...");
			FallbackUserControl.Show();
			_jobQueue.Add(Translate("Get repositories"), delegate
			{
				ServiceResult<GitServiceRepository[]> repositoriesResponse = account.Service.GetRepositories().LoadAll();
				if (!repositoriesResponse.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						RepositoriesListBox.ItemsSource = null;
						FallbackUserControl.FallbackTitle = Translate("Unable to load repositories");
						FallbackUserControl.FallbackMessage = repositoriesResponse.Error.FriendlyMessage;
						FallbackUserControl.Show();
					});
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						FallbackUserControl.Collapse();
						_repositories = repositoriesResponse.Result;
						_refreshFilterAction.InvokeNow(FilterTextBox.FilterRequest);
					});
				}
			}, JobFlags.Hidden);
		}

		private void FilterPanel_FilterRequestChanged(object sender, EventArgs e)
		{
			_refreshFilterAction.InvokeWithDelay(FilterTextBox.FilterRequest);
		}

		private void CloneButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: AccountRepositoryItem dataContext })
			{
				MainWindow.Commands.ShowCloneWindow.Execute(dataContext.Repository.GitHttpsUrl, _account);
			}
		}

		private void UpdateList(string filterString)
		{
			List<GitServiceRepository> repositories = _repositories.Filter((GitServiceRepository x) => x.Name.ToLower().Contains(filterString.ToLower()));
			RepositoriesListBox.ItemsSource = GetAccountItems(repositories, _icon);
		}

		private AccountItem[] GetAccountItems(IReadOnlyList<GitServiceRepository> repositories, IImage icon)
		{
			Dictionary<string, GitServiceRepository[]> dictionary = (from x in repositories
				group x by x.Owner).ToDictionary((IGrouping<string, GitServiceRepository> x) => x.Key, (IGrouping<string, GitServiceRepository> x) => x.ToArray());
			List<AccountItem> list = new List<AccountItem>(24);
			foreach (KeyValuePair<string, GitServiceRepository[]> item in dictionary)
			{
				list.Add(new AccountHeaderItem(item.Key));
				list.AddRange(item.Value.Map((GitServiceRepository x) => new AccountRepositoryItem(x, icon)));
			}
			return list.ToArray();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
