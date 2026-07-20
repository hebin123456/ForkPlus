using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class PullRequestsTabItem : TabItem
	{
		private MultiselectionTreeViewItem _root = new MultiselectionTreeViewItem();

		private bool _stopScrollChangedEvents;

		private Remote[] _remotes;

		[Null]
		private Remote _selectedRemote;

		[Null]
		private IPaged<PullRequest> _pagedItems;

		[Null]
		private Job _activeJob;

		private string _searchQuery;

		private ScrollViewer ScrollViewer => (ScrollViewer)VisualTreeHelper.GetChild((Border)VisualTreeHelper.GetChild(TreeView, 0), 0);

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public PullRequestsTabItem()
		{
			InitializeComponent();
			FallbackUserControl.FallbackMessageFontSize = 14.0;
			TreeView.RootItem = _root;
			FilterTextBox.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					Reset();
					LoadNext();
				}
			};
			FilterTextBox.DropdownContextMenuOpened += FilterTextBox_DropdownContextMenuOpened;
			TreeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
		}

		public void OnActivated()
		{
			RefreshIfNeeded();
		}

		public void SetServices(Remote[] remotesWithService)
		{
			if (remotesWithService.Length == 0)
			{
				return;
			}
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string defaultRemote = gitModule.Settings.PullRequestsDefaultRemote;
			Remote remote = IReadOnlyListExtensions.FirstItem(remotesWithService, (Remote x) => x.Name == defaultRemote) ?? remotesWithService[0];
			Remote selectedRemote = _selectedRemote;
			if (selectedRemote == null || !selectedRemote.DataEquals(remote))
			{
				_selectedRemote = remote;
				_remotes = remotesWithService;
				if (_remotes.Length > 1)
				{
					RemoteDropdownButton.Show();
					RemoteDropdownButtonImage.Source = _selectedRemote.Icon;
					RemoteDropdownButtonTitle.Text = _selectedRemote.Name;
				}
				else
				{
					RemoteDropdownButton.Collapse();
				}
				FilterTextBox.Hint = _selectedRemote?.AccountConcrete.Service.AllowedQueryParametersHint();
				Reset();
			}
		}

		private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			TreeView.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
			ScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
		}

		private void FilterTextBox_DropdownContextMenuOpened(object sender, EventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			if (_selectedRemote != null)
			{
				contextMenu.Items.Add(new HeaderMenuItem("Filter Pull Requests")
				{
					FontSize = 12.0,
					Margin = new Thickness(-10.0, 0.0, 0.0, 0.0)
				});
				if (_selectedRemote.AccountConcrete.Service.AllowedQueryParameters.ContainsItem(SearchQuery.Author.TryCreate))
				{
					MenuItem menuItem = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("My Pull Requests"),
						FontSize = 12.0
					};
					menuItem.Click += delegate
					{
						FilterTextBox.Text = "author:" + _selectedRemote.Account.Username;
						FilterTextBox.FocusAndSelectAllText();
						Reset();
						LoadNext();
					};
					contextMenu.Items.Add(menuItem);
				}
				if (_selectedRemote.AccountConcrete.Service.AllowedQueryParameters.ContainsItem(SearchQuery.Assignee.TryCreate))
				{
					MenuItem menuItem2 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Assigned to me"),
						FontSize = 12.0
					};
					menuItem2.Click += delegate
					{
						FilterTextBox.Text = "assignee:" + _selectedRemote.Account.Username;
						FilterTextBox.FocusAndSelectAllText();
						Reset();
						LoadNext();
					};
					contextMenu.Items.Add(menuItem2);
				}
			}
			contextMenu.Items.Add(new HeaderMenuItem("Recent Queries")
			{
				FontSize = 12.0,
				Margin = new Thickness(-10.0, 0.0, 0.0, 0.0)
			});
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string[] pullRequestsRecentSearchQueries = gitModule.Settings.PullRequestsRecentSearchQueries;
			string[] array = pullRequestsRecentSearchQueries;
			foreach (string query in array)
			{
				MenuItem menuItem3 = new MenuItem
				{
					Header = query.Replace("_", "__").Quotify(),
					FontSize = 12.0
				};
				menuItem3.Click += delegate
				{
					FilterTextBox.Text = query;
					FilterTextBox.FocusAndSelectAllText();
					Reset();
					LoadNext();
				};
				contextMenu.Items.Add(menuItem3);
			}
			if (pullRequestsRecentSearchQueries.Length != 0)
			{
				contextMenu.Items.Add(new Separator());
			}
			MenuItem menuItem4 = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Clear Recents"),
				FontSize = 12.0,
				IsEnabled = (pullRequestsRecentSearchQueries.Length != 0)
			};
			menuItem4.Click += delegate
			{
				gitModule.Settings.ClearRecentPullRequestSearchQueries();
				gitModule.Settings.Save();
				FilterTextBox.FocusAndSelectAllText();
			};
			contextMenu.Items.Add(menuItem4);
		}

		private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (!_stopScrollChangedEvents)
			{
				RefreshIfNeeded();
			}
		}

		private void RefreshIfNeeded()
		{
			if (_selectedRemote == null)
			{
				return;
			}
			if (VisualTreeHelper.GetChildrenCount(TreeView) == 0)
			{
				Log.Debug("Refresh: Layout is not initialized. Request the first page");
				LoadNext();
				return;
			}
			ScrollViewer scrollViewer = ScrollViewer;
			double num = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
			if ((double)_root.Children.Count <= scrollViewer.ViewportHeight)
			{
				Log.Debug("Item list is smaller than the viewport. Loading more to fill empty space");
				LoadNext();
				return;
			}
			double num2 = (double)_root.Children.Count - num;
			if (num2 < 9.0)
			{
				Log.Debug($"Items list tail {num2} is too short. Loading more items");
				LoadNext();
			}
		}

		private void Reset()
		{
			_activeJob?.Monitor.Cancel();
			_activeJob = null;
			BusyIndicator.Hide();
			RefreshButton.Show();
			_searchQuery = FilterTextBox.FilterRequest.Trim();
			_stopScrollChangedEvents = true;
			_root.Children.Clear();
			_stopScrollChangedEvents = false;
			if (_selectedRemote == null)
			{
				_pagedItems = null;
				return;
			}
			AddSearchQueryToRecent(_searchQuery);
			_pagedItems = _selectedRemote.AccountConcrete.Service.GetPullRequests(_selectedRemote, _searchQuery);
		}

		private void LoadNext()
		{
			if (_selectedRemote == null || _activeJob != null)
			{
				return;
			}
			IPaged<PullRequest> pagedItems = _pagedItems;
			if (pagedItems == null || !pagedItems.HasNext)
			{
				return;
			}
			BusyIndicator.Show();
			RefreshButton.Hide();
			_activeJob = RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Get pull requests"), delegate(JobMonitor monitor)
			{
				ServiceResult<PullRequest[]> pullRequestsResponse = _pagedItems.LoadNext();
				if (!pullRequestsResponse.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							BusyIndicator.Hide();
							RefreshButton.Show();
							_activeJob = null;
							FallbackUserControl.Show();
							FallbackUserControl.FallbackMessage = pullRequestsResponse.Error.FriendlyMessage;
						}
					});
				}
				else
				{
					PullRequest[] result = pullRequestsResponse.Result;
					PullRequestItem[] pullRequestItems = result.Map((PullRequest x) => new PullRequestItem(x));
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							BusyIndicator.Hide();
							RefreshButton.Show();
							FallbackUserControl.Hide();
							_activeJob = null;
							PullRequestItem[] array = pullRequestItems;
							foreach (PullRequestItem item in array)
							{
								_root.Children.Add(item);
							}
						}
					});
				}
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar | JobFlags.LongRunning);
		}

		private void PullRequestButton_Click(object sender, RoutedEventArgs e)
		{
			if (((sender as Button).Parent as Grid)?.DataContext is PullRequestItem pullRequestItem)
			{
				pullRequestItem.OpenInBrowser();
			}
		}

		private void NewPullRequestButton_Click(object sender, RoutedEventArgs e)
		{
			ServiceResult<string> newPullRequestUrl = _selectedRemote.AccountConcrete.Service.GetNewPullRequestUrl(_selectedRemote);
			if (newPullRequestUrl.Succeeded)
			{
				new Uri(newPullRequestUrl.Result).OpenInBrowser();
			}
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			Reset();
			LoadNext();
		}

		private void RemoteDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu obj = sender as ContextMenu;
			obj.Items.Clear();
			obj.SetItems(CreateRemotesDropdownMenuItems());
		}

		private IEnumerable<Control> CreateRemotesDropdownMenuItems()
		{
			yield return new HeaderMenuItem("Remotes");

			foreach (Remote remote in _remotes)
			{
				yield return new ToggleMenuItem(remote.Name, delegate
				{
					if (!_selectedRemote.DataEquals(remote))
					{
						GitModule gitModule = RepositoryUserControl.GitModule;
						if (gitModule != null)
						{
							_selectedRemote = remote;
							RemoteDropdownButtonImage.Source = remote.Icon;
							RemoteDropdownButtonTitle.Text = remote.Name;
							Reset();
							LoadNext();
							gitModule.Settings.PullRequestsDefaultRemote = remote.Name;
							gitModule.Settings.Save();
						}
					}
				}, _selectedRemote.DataEquals(remote), new Image
				{
					Source = remote.Icon
				});
			}
		}

		private void SourceBranchButton_Click(object sender, RoutedEventArgs e)
		{
			if (!((sender as Button).Parent<DockPanel>()?.DataContext is PullRequestItem pullRequestItem))
			{
				return;
			}
			string remoteBranchName = _selectedRemote.Name + "/" + pullRequestItem.SourceBranch;
			RemoteBranch[] array = RepositoryUserControl.RepositoryData?.References.RemoteBranches;
			if (array != null)
			{
				RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(array, (RemoteBranch x) => x.Name == remoteBranchName);
				if (remoteBranch != null)
				{
					RepositoryUserControl.SelectRevisions(new Sha[1] { remoteBranch.Sha });
				}
			}
		}

		private void AddSearchQueryToRecent(string searchQuery)
		{
			if (!string.IsNullOrWhiteSpace(searchQuery))
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					gitModule.Settings.AddPullRequestSearchQueryToRecent(searchQuery);
					gitModule.Settings.Save();
				}
			}
		}

	}
}
