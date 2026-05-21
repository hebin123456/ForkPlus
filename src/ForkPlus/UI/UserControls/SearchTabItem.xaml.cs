using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class SearchTabItem : TabItem, ForkPlus.UI.ILocalizableControl
	{
		private bool _isSearchInProgress;

		private RevisionSearchType _searchType;

		private RevisionSearchScope _searchScope;

		private MultiselectionTreeViewItem _root = new MultiselectionTreeViewItem();

		public RevisionSearchQuery SearchQuery => new RevisionSearchQuery(_searchType, _searchScope, FilterTextBox.FilterRequest.Trim());

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public bool IsSearchInProgress
		{
			get
			{
				return _isSearchInProgress;
			}
			set
			{
				_isSearchInProgress = value;
				RefreshBusyIndicator();
			}
		}

		public event EventHandler SearchQueryChanged;

		public SearchTabItem()
		{
			InitializeComponent();
			RefreshSearchControls();
			TreeView.RootItem = _root;
			TreeView.SelectionChanged += TreeView_SelectionChanged;
			FilterTextBox.FilterRequestChanged += FilterTextBox_FilterRequestChanged;
			FilterTextBox.DropdownContextMenuOpened += FilterTextBox_DropdownContextMenuOpened;
			FilterTextBox.ClearButtonClicked += FilterTextBox_ClearButtonClicked;
			FilterTextBox.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					this.SearchQueryChanged?.Invoke(this, EventArgs.Empty);
					AddSearchQueryToRecent(SearchQuery);
				}
			};
			TreeView.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Delete || e.Key == Key.Back)
				{
					RemoveSelectedMatch();
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Escape)
				{
					this.SearchQueryChanged?.Invoke(this, EventArgs.Empty);
					RepositoryUserControl.SidebarActivateRepositoryTab();
					base.Dispatcher.Async(delegate
					{
						RepositoryUserControl.FocusSelectedRevision();
					});
				}
			};
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RefreshSearchControls();
			if (!string.IsNullOrEmpty(FoundCommitsTextBlock.Text))
			{
				RefreshResultCount();
			}
		}

		public void OnActivated()
		{
			if (!FilterTextBox.IsFocused)
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					UpdateLayout();
					FilterTextBox.FocusAndSelectAllText();
				});
			}
		}

		public void ClearMatches()
		{
			TreeView.SelectedItems.Clear();
			_root.Children.Clear();
			FoundCommitsTextBlock.Text = "";
		}

		public void AddMatch(RevisionWithFiles match)
		{
			string searchString = ((_searchType == RevisionSearchType.Message) ? FilterTextBox.FilterRequest : null);
			bool num = _root.Children.Count == 0;
			SidebarSearchItem sidebarSearchItem = new SidebarSearchItem(match, searchString)
			{
				IsExpanded = true
			};
			_root.Children.Add(sidebarSearchItem);
			if (num)
			{
				TreeView.Focus();
				TreeView.SelectAndFocus(sidebarSearchItem);
			}
			RefreshResultCount();
		}

		private void TreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SidebarSearchItem[] array = TreeView.SelectedItems.CompactMap((object x) => x as SidebarSearchItem);
			MultiselectionTreeViewItem[] items = array;
			items.RefreshSelectionType();
			if (array.FirstItem() is SidebarSearchFileItem sidebarSearchFileItem)
			{
				RepositoryUserControl.SelectRevisions(array.Map((SidebarSearchItem x) => x.Sha), NoUIAutomationListView.SelectOptions.ScrollIntoView, sidebarSearchFileItem.ChangedFile.Path);
			}
			else
			{
				RepositoryUserControl.SelectRevisions(array.Map((SidebarSearchItem x) => x.Sha));
			}
		}

		private void SearchDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu obj = sender as ContextMenu;
			obj.Items.Clear();
			obj.SetItems(CreateSearchDropdownMenuItems());
		}

		private void FilterTextBox_DropdownContextMenuOpened(object sender, EventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
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
			RevisionSearchQuery[] recentRevisionSearchQueries = gitModule.Settings.RecentRevisionSearchQueries;
			RevisionSearchQuery[] array = recentRevisionSearchQueries;
			foreach (RevisionSearchQuery query in array)
			{
				string header = GetQueryTypeName(query) + " " + query.SearchString.Replace("_", "__").Quotify();
				MenuItem menuItem = new MenuItem
				{
					Header = header,
					FontSize = 12.0
				};
				menuItem.Click += delegate
				{
					_searchType = query.Type;
					_searchScope = query.Scope;
					FilterTextBox.Text = query.SearchString;
					RefreshSearchControls();
				};
				contextMenu.Items.Add(menuItem);
			}
			if (recentRevisionSearchQueries.Length != 0)
			{
				contextMenu.Items.Add(new Separator());
			}
			MenuItem menuItem2 = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Clear Recents"),
				FontSize = 12.0,
				IsEnabled = (recentRevisionSearchQueries.Length != 0)
			};
			menuItem2.Click += delegate
			{
				gitModule.Settings.ClearRecentRevisionSearchQueries();
				gitModule.Settings.Save();
				FilterTextBox.FocusAndSelectAllText();
			};
			contextMenu.Items.Add(menuItem2);
		}

		private void FilterTextBox_FilterRequestChanged(object sender, EventArgs e)
		{
			RefreshBusyIndicator();
		}

		private void FilterTextBox_ClearButtonClicked(object sender, EventArgs e)
		{
			this.SearchQueryChanged?.Invoke(this, EventArgs.Empty);
		}

		private void RefreshBusyIndicator()
		{
			if (IsSearchInProgress)
			{
				BusyIndicator.Show();
				FilterTextBox.Padding = new Thickness(2.0, 1.0, 18.0, 1.0);
			}
			else
			{
				BusyIndicator.Collapse();
				FilterTextBox.Padding = new Thickness(2.0, 1.0, 2.0, 1.0);
			}
		}

		private void RefreshResultCount()
		{
			if (_root.Children.Count >= 1000)
			{
				FoundCommitsTextBlock.Text = string.Format(Translate(">{0} results"), 1000);
			}
			else
			{
				FoundCommitsTextBlock.Text = string.Format(Translate("{0} results"), _root.Children.Count);
			}
		}

		private string GetQueryTypeName(RevisionSearchQuery query)
		{
			return query.Type switch
			{
				RevisionSearchType.Message => Translate("Message") + "\t", 
				RevisionSearchType.Author => Translate("Author") + "\t", 
				RevisionSearchType.DiffPath => Translate("Path") + "\t", 
				RevisionSearchType.DiffContent => Translate("Diff Content") + "\t", 
				_ => throw new Exception("Cannot reach here"), 
			};
		}

		private IEnumerable<Control> CreateSearchDropdownMenuItems()
		{
			yield return new ToggleMenuItem(SearchMenuItemHeader(RevisionSearchType.Message), delegate
			{
				_searchType = RevisionSearchType.Message;
				RefreshSearchControls();
			}, _searchType == RevisionSearchType.Message);
			yield return new ToggleMenuItem(SearchMenuItemHeader(RevisionSearchType.Author), delegate
			{
				_searchType = RevisionSearchType.Author;
				RefreshSearchControls();
			}, _searchType == RevisionSearchType.Author);
			yield return new ToggleMenuItem(SearchMenuItemHeader(RevisionSearchType.DiffPath), delegate
			{
				_searchType = RevisionSearchType.DiffPath;
				RefreshSearchControls();
			}, _searchType == RevisionSearchType.DiffPath);
			yield return new ToggleMenuItem(SearchMenuItemHeader(RevisionSearchType.DiffContent), delegate
			{
				_searchType = RevisionSearchType.DiffContent;
				RefreshSearchControls();
			}, _searchType == RevisionSearchType.DiffContent);
			yield return new Separator();
			yield return new HeaderMenuItem("Search");
			yield return new ToggleMenuItem("Repository", delegate
			{
				_searchScope = RevisionSearchScope.Repository;
			}, _searchScope == RevisionSearchScope.Repository);
			yield return new ToggleMenuItem("Current Branch", delegate
			{
				_searchScope = RevisionSearchScope.CurrentBranch;
			}, _searchScope == RevisionSearchScope.CurrentBranch);
		}

		private void RefreshSearchControls()
		{
			SearchDropdownButton.Content = SearchMenuItemHeader(_searchType);
			FilterTextBox.Placeholder = SearchPlaceholder(_searchType);
			if (base.IsLoaded)
			{
				FilterTextBox.FocusAndSelectAllText();
			}
		}

		private void RemoveSelectedMatch()
		{
			SidebarSearchItem[] array = TreeView.SelectedItems.CompactMap((object x) => x as SidebarSearchItem);
			using (TreeView.LockUpdates())
			{
				SidebarSearchItem[] array2 = array;
				foreach (SidebarSearchItem sidebarSearchItem in array2)
				{
					if (sidebarSearchItem is SidebarSearchFileItem sidebarSearchFileItem)
					{
						if (sidebarSearchFileItem.ParentItem is SidebarSearchItem sidebarSearchItem2)
						{
							sidebarSearchItem2.Children.Remove(sidebarSearchFileItem);
						}
					}
					else
					{
						_root.Children.Remove(sidebarSearchItem);
					}
				}
			}
		}

		private void AddSearchQueryToRecent(RevisionSearchQuery revisionSearchQuery)
		{
			if (!string.IsNullOrWhiteSpace(revisionSearchQuery.SearchString))
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					gitModule.Settings.AddSearchQueryToRecent(revisionSearchQuery);
					gitModule.Settings.Save();
				}
			}
		}

		private static string SearchMenuItemHeader(RevisionSearchType searchType)
		{
			string text = searchType switch
			{
				RevisionSearchType.Message => "Commit Message", 
				RevisionSearchType.Author => "Author", 
				RevisionSearchType.DiffPath => "Path", 
				RevisionSearchType.DiffContent => "Diff Content", 
				_ => throw new Exception("Cannot reach here"), 
			};
			return Translate(text);
		}

		private static string SearchPlaceholder(RevisionSearchType searchType)
		{
			string text = searchType switch
			{
				RevisionSearchType.Message => "Commit message", 
				RevisionSearchType.Author => "Author or email", 
				RevisionSearchType.DiffPath => "Path, e.g. 'src/*.js'", 
				RevisionSearchType.DiffContent => "Source code", 
				_ => throw new Exception("Cannot reach here"), 
			};
			return Translate(text);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
