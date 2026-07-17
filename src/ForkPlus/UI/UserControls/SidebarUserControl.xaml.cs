using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Accounts.AiServices;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class SidebarUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private class ReferenceByTypeThenByTitleComparer : IComparer<ForkPlus.Git.Reference>
		{
			public int Compare(ForkPlus.Git.Reference l, ForkPlus.Git.Reference r)
			{
				return NaturalStringComparer.Instance.Compare(l.Name, r.Name);
			}
		}

		private class ReferenceByTypeThenByTitleBackwardComparer : IComparer<ForkPlus.Git.Reference>
		{
			public int Compare(ForkPlus.Git.Reference l, ForkPlus.Git.Reference r)
			{
				return -1 * NaturalStringComparer.Instance.Compare(l.Name, r.Name);
			}
		}

		private class ReferenceByTypeThenByDateComparer : IComparer<ForkPlus.Git.Reference>
		{
			public int Compare(ForkPlus.Git.Reference l, ForkPlus.Git.Reference r)
			{
				return -1 * l.CommitterDate.CompareTo(r.CommitterDate);
			}
		}

		private static readonly ReferenceEqualityComparer _referenceComparer = new ReferenceEqualityComparer();

		private static readonly RemoteEqualityComparer _remoteComparer = new RemoteEqualityComparer();

		private readonly FolderSidebarItem _root;

		private readonly SidebarGroupItem _pinned;

		private readonly SidebarGroupItem _branches;

		private readonly SidebarGroupItem _remotes;

		private readonly SidebarGroupItem _tags;

		private readonly SidebarGroupItem _stashes;

		private readonly SidebarGroupItem _submodules;

		private readonly SidebarGroupItem _worktrees;

		private readonly DelayedAction<string> _refreshFilterAction;

		private bool _updateRepositoryDataInProgress;

		private bool _shouldExpandWorktrees;

		private bool _selectionInProgress;

		private RepositoryData _repositoryData = RepositoryData.Empty;

		private LocalBranch[] _oldLocalBranches;

		private Tag[] _oldTags;

		private bool _oldTruncateStashes = true;

		private List<string> _nonTruncatedItems = new List<string>();

		[Null]
		private Popup _aiPopup;

		private bool _initialized;

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public SidebarUserControl()
		{
			InitializeComponent();
			SidebarTreeView.AllowDragDrop = true;
			SidebarTreeView.RememberExpandedItems = true;
			_root = new FolderSidebarItem("", null, this);
			_pinned = CreateSidebarGroupItem(SidebarGroupItem.Group.Pinned);
			_branches = CreateSidebarGroupItem(SidebarGroupItem.Group.Branches);
			_remotes = CreateSidebarGroupItem(SidebarGroupItem.Group.Remotes);
			_tags = CreateSidebarGroupItem(SidebarGroupItem.Group.Tags);
			_stashes = CreateSidebarGroupItem(SidebarGroupItem.Group.Stashes);
			_submodules = CreateSidebarGroupItem(SidebarGroupItem.Group.Submodules);
			_worktrees = CreateSidebarGroupItem(SidebarGroupItem.Group.Worktrees);
			_root.Children.Add(_branches);
			_root.Children.Add(_remotes);
			_root.Children.Add(_tags);
			_root.Children.Add(_stashes);
			_root.Children.Add(_submodules);
			_refreshFilterAction = new DelayedAction<string>(UpdateFilter, 0.1);
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
			SidebarTreeView.RootItem = _root;
			SearchTabItem.Initialize(repositoryUserControl);
			ServiceTabItem.Initialize(repositoryUserControl);
			WeakEventManager<NotificationCenter, EventArgs>.AddHandler(NotificationCenter.Current, "ReferenceSortOrderChanged", ReferenceSortOrderChanged);
			SidebarTreeView.ContextMenuOpening += SidebarTreeView_ContextMenuOpening;
			SidebarTreeView.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(SidebarTreeView_ButtonClick));
			SidebarTreeView.SelectionChanged += SidebarTreeView_SelectionChanged;
			SidebarTreeView.MouseDown += SidebarTreeView_MouseDown;
			SidebarTreeView.MouseDoubleClick += SidebarTreeView_MouseDoubleClick;
			SidebarTreeView.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.F && Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.LeftShift))
				{
					FilterTextBox.FocusAndSelectAllText();
					e.Handled = true;
				}
				else if (e.Key == Key.Escape)
				{
					FilterTextBox.Clear();
					e.Handled = true;
				}
			};
			InitializeKeyBindings();
			FilterTextBox.FilterRequestChanged += delegate
			{
				_refreshFilterAction.InvokeWithDelay(FilterTextBox.FilterRequest);
			};
			_initialized = true;
		}

		private void SidebarTreeView_ButtonClick(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is not Button button)
			{
				return;
			}
			switch (button.Name)
			{
			case "PinButton":
				PinButton_Click(button, e);
				e.Handled = true;
				break;
			case "FilterButton":
				FilterButton_Click(button, e);
				e.Handled = true;
				break;
			case "HideButton":
				HideButton_Click(button, e);
				e.Handled = true;
				break;
			}
		}

		public void RefreshTitle()
		{
			string repositoryName = RepositoryUserControl.RepositoryName;
			string text = RepositoryUserControl.GitModule?.Path ?? "";
			RepositoryNameTextBlock.Value = repositoryName;
			string parentRepositoryName = RepositoryUserControl.ParentRepositoryName;
			if (parentRepositoryName != null)
			{
				string text2 = RepositoryUserControl.GitModule?.ParentRepoPath;
				if (text2 != null)
				{
					RepositoryParentNameTextBlock.Text = parentRepositoryName + ": ";
					RepositoryParentNameTextBlock.ToolTip = parentRepositoryName + "\n" + text2;
					RepositoryNameTextBlock.ToolTip = parentRepositoryName + ": " + repositoryName + "\n" + text;
					return;
				}
			}
			RepositoryParentNameTextBlock.Text = "";
			RepositoryParentNameTextBlock.ToolTip = "";
			RepositoryNameTextBlock.ToolTip = repositoryName + "\n" + text;
		}

		public void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			RepositorySettingsDropdownButton.ToolTip = Preferences.PreferencesLocalization.Translate("Repository Settings", language);
			FilterTextBox.Placeholder = Preferences.PreferencesLocalization.Translate("Filter", language);
			AllCommitsTextBlock.Text = Preferences.PreferencesLocalization.Translate("All Commits", language);
			if (RepositoryUserControl?.RepositoryStatus == null)
			{
				ChangesTextBlock.Text = Preferences.PreferencesLocalization.Translate("Changes", language);
			}
			BranchesRadioButton.ToolTip = Preferences.PreferencesLocalization.Translate("Branches", language);
			SearchRadioButton.ToolTip = Preferences.PreferencesLocalization.Translate("Search Commits", language);
			ServiceRadioButton.ToolTip = Preferences.PreferencesLocalization.Translate("Pull Requests", language);
			SearchTabItem.ApplyLocalization();
			_pinned.RefreshTitle();
			_branches.RefreshTitle();
			_remotes.RefreshTitle();
			_tags.RefreshTitle();
			_stashes.RefreshTitle();
			_submodules.RefreshTitle();
			_worktrees.RefreshTitle();
			ApplyReferenceItemLocalization(_root.Children);
			if (RepositoryUserControl?.RepositoryStatus != null)
			{
				UpdateRepositoryStatus(RepositoryUserControl.RepositoryStatus);
			}
		}

		private static void ApplyReferenceItemLocalization(MultiselectionTreeViewItemCollection items)
		{
			foreach (MultiselectionTreeViewItem item in items)
			{
				if (item is ReferenceSidebarItem referenceSidebarItem)
				{
					referenceSidebarItem.ApplyLocalization();
				}
				if (item is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
				{
					filterableRemoteSidebarItem.ApplyLocalization();
				}
				if (item is FilterableFolderSidebarItem filterableFolderSidebarItem)
				{
					filterableFolderSidebarItem.ApplyLocalization();
				}
				ApplyReferenceItemLocalization(item.Children);
			}
		}

		public void SetRepositoryViewMode(RepositoryViewMode viewMode)
		{
			switch (viewMode)
			{
			case RepositoryViewMode.RevisionViewMode:
				AllCommitsRadioButton.IsChecked = true;
				break;
			case RepositoryViewMode.CommitViewMode:
				ChangesRadioButton.IsChecked = true;
				break;
			default:
				throw new CannotReachHereException();
			}
		}

		public void ActivateRepositoryTab()
		{
			BranchesTabItem.IsSelected = true;
		}

		public void ActivateSearchTab()
		{
			SearchTabItem.IsSelected = true;
			SearchTabItem.OnActivated();
		}

		public void UpdateRepositoryData(RepositoryData repositoryData)
		{
			Reload(repositoryData, forceRefresh: false, SidebarTreeView.FilterString);
		}

		public void UpdateRepositoryStatus(RepositoryStatus repositoryStatus)
		{
			Submodule[] dirtySubmodules = repositoryStatus.ChangedFiles.CompactMap((ChangedFile x) => (x as SubmoduleChangedFile)?.Submodule);
			UpdateChangedSubmodules(_submodules, dirtySubmodules);
			int filesCount = repositoryStatus.FilesCount;
			string localChanges = Preferences.PreferencesLocalization.Current("Local Changes");
			string text = ((filesCount > 0) ? $"{localChanges} ({filesCount})" : localChanges);
			ChangesTextBlock.Text = text;
		}

		public void OnDirectoryItemIsExpandedChanged()
		{
			if (_initialized && string.IsNullOrEmpty(SidebarTreeView.FilterString) && !_updateRepositoryDataInProgress)
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					gitModule.Settings.ExpandedSidebarItems = SidebarTreeView.GetExpandedItems();
					gitModule.Settings.Save();
					_shouldExpandWorktrees = false;
				}
			}
		}

		public void RevealActiveBranch()
		{
			if (!BranchesTabItem.IsSelected)
			{
				ActivateRepositoryTab();
			}
			LocalBranchSidebarItem localBranchSidebarItem = FindActiveBranchItem(_pinned);
			if (localBranchSidebarItem != null)
			{
				SidebarTreeView.ScrollIntoView(localBranchSidebarItem);
				return;
			}
			LocalBranchSidebarItem localBranchSidebarItem2 = FindActiveBranchItem(_branches);
			if (localBranchSidebarItem2 != null)
			{
				SidebarTreeView.ScrollIntoView(localBranchSidebarItem2);
			}
		}

		private void ToggleTruncate(FolderSidebarItem section)
		{
			if (_nonTruncatedItems.Contains(section.Title))
			{
				_nonTruncatedItems.Remove(section.Title);
			}
			else
			{
				_nonTruncatedItems.Add(section.Title);
			}
			Reload(_repositoryData, forceRefresh: true, SidebarTreeView.FilterString);
		}

		private void TagTitleTextBlock_ToolTipOpening(object sender, ToolTipEventArgs e)
		{
			if (sender is TextBlock { DataContext: TagSidebarItem { Reference: Tag reference } } textBlock)
			{
				textBlock.ToolTip = $"Tag '{reference.Name}'\n{reference.CommitterDate}";
			}
		}

		private void TruncateSidebarItem_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			if ((sender as FrameworkContentElement)?.DataContext is TruncateSidebarItem { Parent: FolderSidebarItem parent })
			{
				ToggleTruncate(parent);
			}
		}

		private void PinButton_Click(object sender, RoutedEventArgs e)
		{
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule != null && (sender as Button)?.Parent<Grid>()?.DataContext is ReferenceSidebarItem referenceSidebarItem)
			{
				if (!referenceSidebarItem.Pinned)
				{
					RepositoryUserControl.Commands.AddReferenceStar.Execute(gitModule, _repositoryData.References.Items, referenceSidebarItem.Reference);
				}
				else
				{
					RepositoryUserControl.Commands.RemoveReferenceStar.Execute(gitModule, _repositoryData.References.Items, referenceSidebarItem.Reference);
				}
			}
		}

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			bool isCtrlDown = KeyboardHelper.IsCtrlDown;
			if (((sender as Button)?.Parent as Grid).DataContext is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
			{
				ReferenceFilterState filterStatus = ((filterableRemoteSidebarItem.FilterState != ReferenceFilterState.Filter) ? ReferenceFilterState.Filter : ReferenceFilterState.None);
				string[] patterns = new string[1] { filterableRemoteSidebarItem.FullReference };
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, patterns, filterStatus, isCtrlDown);
			}
			else if (((sender as Button)?.Parent as Grid).DataContext is FilterableFolderSidebarItem filterableFolderSidebarItem)
			{
				ReferenceFilterState filterStatus2 = ((filterableFolderSidebarItem.FilterState != ReferenceFilterState.Filter) ? ReferenceFilterState.Filter : ReferenceFilterState.None);
				List<string> list = new List<string>();
				list.Add(filterableFolderSidebarItem.FullReference);
				if (filterableFolderSidebarItem.FullReference.StartsWith("refs/heads/"))
				{
					Remote[] items = _repositoryData.Remotes.Items;
					foreach (Remote remote in items)
					{
						string item = filterableFolderSidebarItem.FullReference.Replace("refs/heads/", "refs/remotes/" + remote.Name + "/");
						list.Add(item);
					}
				}
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, list.ToArray(), filterStatus2, isCtrlDown);
			}
			else if (((sender as Button)?.Parent as Grid).DataContext is ReferenceSidebarItem referenceSidebarItem)
			{
				ReferenceFilterState filterStatus3 = ((referenceSidebarItem.FilterState != ReferenceFilterState.Filter) ? ReferenceFilterState.Filter : ReferenceFilterState.None);
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, referenceSidebarItem.Reference, filterStatus3, isCtrlDown);
			}
		}

		private void HideButton_Click(object sender, RoutedEventArgs e)
		{
			bool isCtrlDown = KeyboardHelper.IsCtrlDown;
			if (((sender as Button)?.Parent as Grid).DataContext is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
			{
				ReferenceFilterState filterStatus = ((filterableRemoteSidebarItem.FilterState != ReferenceFilterState.Hide) ? ReferenceFilterState.Hide : ReferenceFilterState.None);
				string[] patterns = new string[1] { filterableRemoteSidebarItem.FullReference };
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, patterns, filterStatus, isCtrlDown);
			}
			else if (((sender as Button)?.Parent as Grid).DataContext is FilterableFolderSidebarItem filterableFolderSidebarItem)
			{
				ReferenceFilterState filterStatus2 = ((filterableFolderSidebarItem.FilterState != ReferenceFilterState.Hide) ? ReferenceFilterState.Hide : ReferenceFilterState.None);
				List<string> list = new List<string>();
				list.Add(filterableFolderSidebarItem.FullReference);
				if (filterableFolderSidebarItem.FullReference.StartsWith("refs/heads/"))
				{
					Remote[] items = _repositoryData.Remotes.Items;
					foreach (Remote remote in items)
					{
						string item = filterableFolderSidebarItem.FullReference.Replace("refs/heads/", "refs/remotes/" + remote.Name + "/");
						list.Add(item);
					}
				}
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, list.ToArray(), filterStatus2, isCtrlDown);
			}
			else if (((sender as Button)?.Parent as Grid).DataContext is ReferenceSidebarItem referenceSidebarItem)
			{
				ReferenceFilterState filterStatus3 = ((referenceSidebarItem.FilterState != ReferenceFilterState.Hide) ? ReferenceFilterState.Hide : ReferenceFilterState.None);
				RepositoryUserControl.Commands.UpdateReferenceFilter.SetFilterState(RepositoryUserControl, referenceSidebarItem.Reference, filterStatus3, isCtrlDown);
			}
		}

		private void RepositorySettingsDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			bool flag = gitModule.Type == ModuleType.Submodule || gitModule.Type == ModuleType.Worktree;
			ContextMenu obj = sender as ContextMenu;
			obj.Items.Clear();
			MenuItem menuItem = new MenuItem();
			menuItem.Header = Preferences.PreferencesLocalization.Current("Rename Repository");
			menuItem.IsEnabled = !flag;
			menuItem.Click += delegate
			{
				RepositoryNameTextBlock.ShowEditor(RepositoryUserControl.RepositoryName, delegate(bool success, string newName)
				{
					RepositoryNameTextBlock.HideEditor();
					if (success)
					{
						RenameRepository(newName);
					}
				});
			};
			obj.Items.Add(menuItem);
			obj.Items.Add(new Separator());
			obj.Items.Add(RepositoryUserControl.Commands.ShowRepositorySettingsWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowRepositorySettingsWindow.Execute(gitModule, _repositoryData);
			}));
		}

		private void RenameRepository(string newName)
		{
			string text = RepositoryUserControl?.GitModule.Path;
			if (text != null)
			{
				RepositoryManager.Instance.RenameRepository(text, newName);
				NotificationCenter.Current.RaiseRepositoryNameChanged(this, text);
				RepositoryManager.Instance.Save();
			}
		}

		private void AllCommits_Selected(object sender, RoutedEventArgs e)
		{
			MainWindow.Commands.ActivateRevisionList.Execute();
		}

		private void Changes_Selected(object sender, RoutedEventArgs e)
		{
			MainWindow.Commands.ActivateCommitView.Execute();
		}

		public void ShowDropContextMenu(ForkPlus.Git.Reference target, Branch sourceBranch)
		{
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule != null)
			{
				if (target is LocalBranch destinationBranch)
				{
					SidebarTreeView.ContextMenu.LayoutTransform = Theme.LayoutScaleTransform;
					SidebarTreeView.ContextMenu.SetItems(CreateLocalBranchDropContextMenuItems(repositoryUserControl, gitModule, destinationBranch, sourceBranch));
					SidebarTreeView.ContextMenu.IsOpen = true;
				}
				else if (target is RemoteBranch destinationBranch2 && sourceBranch is LocalBranch sourceBranch2)
				{
					SidebarTreeView.ContextMenu.LayoutTransform = Theme.LayoutScaleTransform;
					SidebarTreeView.ContextMenu.SetItems(CreateRemoteBranchDropContextMenuItems(repositoryUserControl, gitModule, destinationBranch2, sourceBranch2));
					SidebarTreeView.ContextMenu.IsOpen = true;
				}
			}
		}

		private static SidebarItem[] ClickedItems(MultiselectionTreeView treeView)
		{
			if (!(treeView.LastClickedItem is SidebarItem sidebarItem))
			{
				return new SidebarItem[0];
			}
			if (treeView.SelectedItems.Contains(sidebarItem))
			{
				return treeView.SelectedItems.CompactMap((object x) => x as SidebarItem);
			}
			return new SidebarItem[1] { sidebarItem };
		}

		private void InitializeKeyBindings()
		{
			SidebarTreeView.CommandBindings.Add(RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.CreateShortcutCommandBinding(delegate
			{
				GitModule gitModule2 = RepositoryUserControl.GitModule;
				if (gitModule2 != null && SidebarTreeView.SelectedItems.Count == 1 && SidebarTreeView.SelectedItems[0] is LocalBranchSidebarItem { Reference: LocalBranch reference })
				{
					RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(RepositoryUserControl, gitModule2, _repositoryData.References, reference);
				}
			}));
			SidebarTreeView.CommandBindings.Add(RepositoryUserControl.Commands.RemoveReferenceCommand.CreateShortcutCommandBinding(delegate
			{
				if (RepositoryUserControl.GitModule != null)
				{
					SidebarItem[] array3 = SidebarTreeView.SelectedItems.CompactMap((object x) => x as SidebarItem);
					if (array3.Length != 0)
					{
						if (array3.All((SidebarItem x) => x is StashSidebarItem))
						{
							StashRevision[] stashes = array3.CompactMap((SidebarItem x) => (x as StashSidebarItem)?.Stash);
							RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(RepositoryUserControl, stashes);
						}
						else
						{
							ForkPlus.Git.Reference[] source = array3.CompactMap((SidebarItem x) => (x as ReferenceSidebarItem)?.Reference);
							if (source.All((ForkPlus.Git.Reference x) => x is LocalBranch))
							{
								LocalBranch[] branches = source.Map((ForkPlus.Git.Reference x) => x as LocalBranch);
								RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(RepositoryUserControl, branches);
							}
							else if (source.All((ForkPlus.Git.Reference x) => x is RemoteBranch))
							{
								RemoteBranch[] branches2 = source.Map((ForkPlus.Git.Reference x) => x as RemoteBranch);
								RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(RepositoryUserControl, branches2);
							}
							else if (source.All((ForkPlus.Git.Reference x) => x is Tag))
							{
								Tag[] tags = source.Map((ForkPlus.Git.Reference x) => x as Tag);
								RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(RepositoryUserControl, tags);
							}
						}
					}
				}
			}));
			SidebarTreeView.CommandBindings.Add(RepositoryUserControl.Commands.CopyFilePaths.CreateShortcutCommandBinding(delegate
			{
				SubmoduleSidebarItem[] array2 = SidebarTreeView.SelectedItems.CompactMap((object x) => x as SubmoduleSidebarItem);
				if (array2.Length != 0)
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(array2.Map((SubmoduleSidebarItem x) => x.Submodule.Path));
				}
			}));
			SidebarTreeView.CommandBindings.Add(RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateShortcutCommandBinding(delegate
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					SubmoduleSidebarItem[] array = SidebarTreeView.SelectedItems.CompactMap((object x) => x as SubmoduleSidebarItem);
					if (array.Length != 0)
					{
						RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, array.Map((SubmoduleSidebarItem x) => x.Submodule.Path));
					}
				}
			}));
		}

		private static void UpdateChangedSubmodules(FolderSidebarItem parent, Submodule[] dirtySubmodules)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				SubmoduleSidebarItem submoduleSidebarItem = child as SubmoduleSidebarItem;
				if (submoduleSidebarItem != null)
				{
					submoduleSidebarItem.IsDirty = dirtySubmodules.ContainsItem((Submodule x) => x == submoduleSidebarItem.Submodule);
				}
				else if (child is FolderSidebarItem parent2)
				{
					UpdateChangedSubmodules(parent2, dirtySubmodules);
				}
			}
		}

		private void SidebarTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = RepositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = RepositoryUserControl.SubmodulesToUpdate();
			SidebarItem[] source = ClickedItems(SidebarTreeView);
			SidebarItem sidebarItem = source.FirstItem();
			if (sidebarItem == null)
			{
				return;
			}
			if (sidebarItem is ReferenceSidebarItem)
			{
				ForkPlus.Git.Reference[] source2 = source.CompactMap((SidebarItem x) => (x as ReferenceSidebarItem)?.Reference);
				Remote[] remotes = _repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
				LocalBranch activeBranch = _repositoryData.References.ActiveBranch;
				if (source2.All((ForkPlus.Git.Reference x) => x is LocalBranch))
				{
					LocalBranch[] branches = source2.Map((ForkPlus.Git.Reference x) => x as LocalBranch);
					SidebarTreeView.ContextMenu.SetItems(CreateLocalBranchContextMenuItems(repositoryUserControl, gitModule, _repositoryData, branches, activeBranch, remotes, sidebarItem, commitGraphCache));
				}
				else if (source2.All((ForkPlus.Git.Reference x) => x is RemoteBranch))
				{
					RemoteBranch[] branches2 = source2.Map((ForkPlus.Git.Reference x) => x as RemoteBranch);
					SidebarTreeView.ContextMenu.SetItems(CreateRemoteBranchContextMenuItems(repositoryUserControl, gitModule, _repositoryData, branches2, activeBranch, sidebarItem, commitGraphCache));
				}
				else if (source2.All((ForkPlus.Git.Reference x) => x is Tag))
				{
					Tag[] tags = source2.Map((ForkPlus.Git.Reference x) => x as Tag);
					SidebarTreeView.ContextMenu.SetItems(CreateTagContextMenuItems(repositoryUserControl, gitModule, _repositoryData, tags, activeBranch));
				}
				else
				{
					Log.Warn("Unknown reference type in menu item");
					e.Handled = true;
					SidebarTreeView.ContextMenu.IsOpen = false;
				}
			}
			else if (sidebarItem is StashSidebarItem)
			{
				StashRevision[] stashes = source.CompactMap((SidebarItem x) => (x as StashSidebarItem)?.Stash);
				SidebarTreeView.ContextMenu.SetItems(CreateStashContextMenuItems(repositoryUserControl, stashes));
			}
			else if (sidebarItem is RemoteSidebarItem remoteSidebarItem)
			{
				SidebarTreeView.ContextMenu.SetItems(CreateRemoteSidebarItemMenuItems(repositoryUserControl, gitModule, remoteSidebarItem.Remote));
			}
			else if (sidebarItem is SubmoduleSidebarItem)
			{
				Submodule[] submodules = source.CompactMap((SidebarItem x) => (x as SubmoduleSidebarItem)?.Submodule);
				SidebarTreeView.ContextMenu.SetItems(CreateSubmoduleSidebarItemMenuItems(repositoryUserControl, gitModule, submodules, submodulesToUpdate));
			}
			else if (sidebarItem is WorktreeSidebarItem)
			{
				Worktree[] worktrees = source.CompactMap((SidebarItem x) => x as WorktreeSidebarItem).Map((WorktreeSidebarItem x) => x.Worktree);
				SidebarTreeView.ContextMenu.SetItems(CreateWorktreeSidebarItemMenuItems(repositoryUserControl, gitModule, worktrees));
			}
			else if (sidebarItem is MainWorktreeSidebarItem)
			{
				Worktree[] worktrees2 = source.CompactMap((SidebarItem x) => x as MainWorktreeSidebarItem).Map((MainWorktreeSidebarItem x) => x.Worktree);
				SidebarTreeView.ContextMenu.SetItems(CreateWorktreeSidebarItemMenuItems(repositoryUserControl, gitModule, worktrees2));
			}
			else if (sidebarItem is SidebarGroupItem { GroupType: var groupType })
			{
				switch (groupType)
				{
				case SidebarGroupItem.Group.Branches:
					SidebarTreeView.ContextMenu.SetItems(CreateBranchesSidebarItemGroupMenuItems(repositoryUserControl));
					return;
				case SidebarGroupItem.Group.Tags:
					SidebarTreeView.ContextMenu.SetItems(CreateTagsSidebarItemGroupMenuItems(repositoryUserControl));
					return;
				case SidebarGroupItem.Group.Remotes:
					SidebarTreeView.ContextMenu.SetItems(CreateRemoteSidebarItemGroupMenuItems(repositoryUserControl, gitModule));
					return;
				case SidebarGroupItem.Group.Submodules:
					SidebarTreeView.ContextMenu.SetItems(CreateSubmoduleSidebarItemGroupMenuItems(repositoryUserControl, gitModule, _repositoryData.Submodules.Items, submodulesToUpdate));
					return;
				case SidebarGroupItem.Group.Worktrees:
					SidebarTreeView.ContextMenu.SetItems(CreateWorktreesSidebarItemGroupMenuItems(repositoryUserControl));
					return;
				}
				Log.Warn("Unknown sidebar group type in menu item");
				e.Handled = true;
				SidebarTreeView.ContextMenu.IsOpen = false;
			}
			else
			{
				e.Handled = true;
				SidebarTreeView.ContextMenu.IsOpen = false;
			}
		}

		private IEnumerable<Control> CreateBranchesSidebarItemGroupMenuItems(RepositoryUserControl repositoryUserControl)
		{
			yield return RepositoryUserControl.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, null);
			});
			MenuItem menuItem = new MenuItem();
			menuItem.Header = Preferences.PreferencesLocalization.MenuHeader("Select Stale Local Branches");
			menuItem.Click += delegate
			{
				SelectStaleBranches();
			};
			yield return menuItem;
			yield return new Separator();
			yield return new HeaderMenuItem("Sort Branches:");
			yield return new ToggleMenuItem("Alphabetically", delegate
			{
				ForkPlusSettings.Default.LocalBranchSortOrder = ReferenceSortOrder.Alphabetically;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.LocalBranchSortOrder == ReferenceSortOrder.Alphabetically);
			yield return new ToggleMenuItem("Alphabetically backward", delegate
			{
				ForkPlusSettings.Default.LocalBranchSortOrder = ReferenceSortOrder.AlphabeticallyBackward;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.LocalBranchSortOrder == ReferenceSortOrder.AlphabeticallyBackward);
			yield return new ToggleMenuItem("Recently used", delegate
			{
				ForkPlusSettings.Default.LocalBranchSortOrder = ReferenceSortOrder.RecentlyUsed;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.LocalBranchSortOrder == ReferenceSortOrder.RecentlyUsed);
		}

		private void SelectStaleBranches()
		{
			_selectionInProgress = true;
			SidebarTreeView.SelectedItems.Clear();
			LocalBranchSidebarItem[] array = FindStaleBranchItems(_branches);
			if (array.Length == 0)
			{
				_selectionInProgress = false;
				return;
			}
			LocalBranchSidebarItem[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				foreach (MultiselectionTreeViewItem item in array2[i].Ancestors())
				{
					item.IsExpanded = true;
				}
			}
			for (int j = 0; j < array.Length; j++)
			{
				if (j == array.Length - 1)
				{
					_selectionInProgress = false;
				}
				SidebarTreeView.SelectedItems.Add(array[j]);
			}
			SidebarTreeView.FocusNode(array[0]);
		}

		private static LocalBranchSidebarItem[] FindStaleBranchItems(FolderSidebarItem folder)
		{
			List<LocalBranchSidebarItem> list = new List<LocalBranchSidebarItem>();
			foreach (MultiselectionTreeViewItem child in folder.Children)
			{
				if (child is FolderSidebarItem folder2)
				{
					list.AddRange(FindStaleBranchItems(folder2));
				}
				else if (child is LocalBranchSidebarItem localBranchSidebarItem && localBranchSidebarItem.LocalBranch.UpstreamFullName != null && localBranchSidebarItem.UpstreamStatus.HasValue && !localBranchSidebarItem.UpstreamStatus.Value.IsValid)
				{
					list.Add(localBranchSidebarItem);
				}
			}
			return list.ToArray();
		}

		private IEnumerable<Control> CreateTagsSidebarItemGroupMenuItems(RepositoryUserControl repositoryUserControl)
		{
			yield return RepositoryUserControl.Commands.ShowCreateTagWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, null);
			});
			yield return new Separator();
			yield return new HeaderMenuItem("Sort Tags:");
			yield return new ToggleMenuItem("Alphabetically", delegate
			{
				ForkPlusSettings.Default.TagSortOrder = ReferenceSortOrder.Alphabetically;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.TagSortOrder == ReferenceSortOrder.Alphabetically);
			yield return new ToggleMenuItem("Alphabetically backward", delegate
			{
				ForkPlusSettings.Default.TagSortOrder = ReferenceSortOrder.AlphabeticallyBackward;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.TagSortOrder == ReferenceSortOrder.AlphabeticallyBackward);
			yield return new ToggleMenuItem("Recently used", delegate
			{
				ForkPlusSettings.Default.TagSortOrder = ReferenceSortOrder.RecentlyUsed;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.TagSortOrder == ReferenceSortOrder.RecentlyUsed);
		}

		private IEnumerable<Control> CreateRemoteSidebarItemGroupMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			yield return RepositoryUserControl.Commands.ShowAddRemoteWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowAddRemoteWindow.Execute(repositoryUserControl, gitModule);
			});
			yield return new Separator();
			yield return new HeaderMenuItem("Sort Branches:");
			yield return new ToggleMenuItem("Alphabetically", delegate
			{
				ForkPlusSettings.Default.RemoteBranchSortOrder = ReferenceSortOrder.Alphabetically;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.RemoteBranchSortOrder == ReferenceSortOrder.Alphabetically);
			yield return new ToggleMenuItem("Alphabetically backward", delegate
			{
				ForkPlusSettings.Default.RemoteBranchSortOrder = ReferenceSortOrder.AlphabeticallyBackward;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.RemoteBranchSortOrder == ReferenceSortOrder.AlphabeticallyBackward);
			yield return new ToggleMenuItem("Recently used", delegate
			{
				ForkPlusSettings.Default.RemoteBranchSortOrder = ReferenceSortOrder.RecentlyUsed;
				NotificationCenter.Current.RaiseReferenceSortOrderChanged(this);
			}, ForkPlusSettings.Default.RemoteBranchSortOrder == ReferenceSortOrder.RecentlyUsed);
		}

		private IEnumerable<Control> CreateRemoteSidebarItemMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote)
		{
			List<Control> list = new List<Control>();
			list.Add(MainWindow.Commands.ShowFetchWindow.CreateMenuItem(PreferencesLocalization.FormatCurrent("Fetch '{0}'...", remote.Name), delegate
			{
				MainWindow.Commands.ShowFetchWindow.Execute(repositoryUserControl, gitModule, remote);
			}));
			list.Add(new Separator());
			list.Add(RepositoryUserControl.Commands.ShowEditRemoteWindow.CreateMenuItem(PreferencesLocalization.FormatCurrent("Edit '{0}'...", remote.Name), delegate
			{
				RepositoryUserControl.Commands.ShowEditRemoteWindow.Execute(repositoryUserControl, gitModule, remote);
			}));
			list.Add(RepositoryUserControl.Commands.ShowRemoveRemoteWindow.CreateMenuItem(PreferencesLocalization.FormatCurrent("Delete '{0}'...", remote.Name), delegate
			{
				RepositoryUserControl.Commands.ShowRemoveRemoteWindow.Execute(repositoryUserControl, gitModule, remote);
			}));
			list.Add(new Separator());
			MenuItem menuItem = RepositoryUserControl.Commands.DisableImplicitRemoteFetch.CreateMenuItem(PreferencesLocalization.FormatCurrent("Fetch '{0}' Automatically", remote.Name), delegate
			{
				RepositoryUserControl.Commands.DisableImplicitRemoteFetch.Execute(repositoryUserControl, gitModule, remote, !remote.DisableImplicitFetch);
			});
			menuItem.IsChecked = !remote.DisableImplicitFetch;
			list.Add(menuItem);
			string repositoryWebpageUrl = new RepositoryUrlBuilder(remote).RepositoryWebpageUrl;
			if (repositoryWebpageUrl != null)
			{
				string gitServiceName = remote.RemoteType.FriendlyName();
				if (gitServiceName != null)
				{
					list.Add(new Separator());
					list.Add(MainWindow.Commands.OpenUrl.CreateMenuItem("View on " + gitServiceName, delegate
					{
						MainWindow.Commands.OpenUrl.Execute(repositoryWebpageUrl);
					}));
				}
			}
			list.Add(new Separator());
			list.Add(RepositoryUserControl.Commands.ShowAddRemoteWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowAddRemoteWindow.Execute(repositoryUserControl, gitModule);
			}));
			list.Add(new Separator());
			list.Add(RepositoryUserControl.Commands.CopyRemoteAddress.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRemoteAddress.Execute(remote?.Url);
			}));
			return list;
		}

		private IEnumerable<Control> CreateLocalBranchContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, LocalBranch[] branches, [Null] LocalBranch activeBranch, Remote[] remotes, SidebarItem sidebarItem, CommitGraphCache commitGraphCache)
		{
			List<Control> list = new List<Control>();
			if (branches.Length == 1)
			{
				LocalBranch branch = branches.FirstItem();
				RemoteBranch upstreamBranch = null;
				string upstreamFullReference = branch.UpstreamFullReference;
				if (upstreamFullReference != null)
				{
					RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
					if (remoteBranch != null)
					{
						upstreamBranch = remoteBranch;
					}
				}
				if (repositoryData.Worktrees.WorktreesByFullReference.TryGetValue(branch.FullReference, out Worktree branchWorktree))
				{
					if (!branchWorktree.IsMain)
					{
						list.Add(RepositoryUserControl.Commands.OpenWorktree.CreateMenuItem("Open '" + branchWorktree.FriendlyName + "' Worktree", delegate
						{
							RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { branchWorktree });
						}));
						list.Add(new Separator());
					}
				}
				else
				{
					list.Add(RepositoryUserControl.Commands.ShowCheckoutBranchWindow.CreateMenuItem("Checkout '" + branch.Name + "'", delegate
					{
						RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, branch);
					}));
					if (repositoryData.Worktrees.IsEnabled)
					{
						list.Add(MainWindow.Commands.ShowCheckoutBranchAsWorktreeWindow.CreateMenuItem("Checkout '" + branch.Name + "' as Worktree...", delegate
						{
							MainWindow.Commands.ShowCheckoutBranchAsWorktreeWindow.Execute(repositoryUserControl, branch);
						}));
					}
					list.Add(new Separator());
				}
				bool flag = repositoryData.Worktrees.WorktreesByFullReference.TryGetValue(branch.FullReference, out Worktree value) && !value.IsActive;
				bool? isLocalBranchInfrontUpstream = ((upstreamBranch != null) ? new bool?(branch.IsInfrontUpstream(upstreamBranch, gitModule, commitGraphCache)) : null);
				if (upstreamBranch != null)
				{
					if (isLocalBranchInfrontUpstream == false)
					{
						list.Add(RepositoryUserControl.Commands.FastForward.CreateMenuItem("Fast-Forward to '" + upstreamBranch.Name + "'", delegate
						{
							RepositoryUserControl.Commands.FastForward.Execute(repositoryUserControl, branch);
						}, !flag));
					}
					else
					{
						list.Add(RepositoryUserControl.Commands.FastForwardPull.CreateMenuItem("Fast-Forward to '" + upstreamBranch.Name + "'", delegate
						{
							RepositoryUserControl.Commands.FastForwardPull.Execute(repositoryUserControl, branch);
						}, !flag));
					}
					if (branch.IsActive)
					{
						list.Add(RepositoryUserControl.Commands.FastForward.CreateMenuItem("Pull '" + upstreamBranch.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.ShowPullWindow.Execute(repositoryUserControl, upstreamBranch);
						}));
					}
				}
				if (remotes.Length == 1)
				{
					Remote remote = remotes[0];
					list.Add(RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push '" + branch.Name + "' to '" + remote.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushBranchWindow.Execute(repositoryUserControl, remote, branch);
					}));
					string branchName = upstreamBranch?.ShortName ?? branch.Name;
					string pullRequestUrl = new RepositoryUrlBuilder(remote).CreatePullRequestUrl(branchName);
					if (pullRequestUrl != null)
					{
						bool pushRequired = upstreamBranch == null || isLocalBranchInfrontUpstream.GetValueOrDefault();
						string header = pushRequired ? PreferencesLocalization.FormatCurrent("Push and Create Pull Request on '{0}'...", remote.Name) : PreferencesLocalization.FormatCurrent("Create Pull Request on '{0}'...", remote.Name);
						list.Add(RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem(header, delegate
						{
							if (pushRequired)
							{
								RepositoryUserControl.Commands.CreatePullRequest.Execute(repositoryUserControl, branch, upstreamBranch, remote.Name, pullRequestUrl);
							}
							else
							{
								RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
							}
						}));
					}
				}
				else if (remotes.Length != 0)
				{
					MenuItem menuItem = RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push");
					MenuItem pullRequestMenuItem = RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Create Pull Request");
					foreach (Remote remote in remotes)
					{
						Remote currentRemote = remote;
						menuItem.Items.Add(RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push '" + branch.Name + "' to '" + currentRemote.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.ShowPushBranchWindow.Execute(repositoryUserControl, currentRemote, branch);
						}));
						string branchName = upstreamBranch?.ShortName ?? branch.Name;
						string pullRequestUrl = new RepositoryUrlBuilder(currentRemote).CreatePullRequestUrl(branchName);
						if (pullRequestUrl != null)
						{
							bool pushRequired = upstreamBranch == null || isLocalBranchInfrontUpstream.GetValueOrDefault();
							string header = pushRequired ? PreferencesLocalization.FormatCurrent("Push and Create Pull Request on '{0}'...", currentRemote.Name) : PreferencesLocalization.FormatCurrent("Create Pull Request on '{0}'...", currentRemote.Name);
							pullRequestMenuItem.Items.Add(RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem(header, delegate
							{
								if (pushRequired)
								{
									RepositoryUserControl.Commands.CreatePullRequest.Execute(repositoryUserControl, branch, upstreamBranch, currentRemote.Name, pullRequestUrl);
								}
								else
								{
									RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
								}
							}));
						}
					}
					list.Add(menuItem);
					if (pullRequestMenuItem.Items.Count > 0)
					{
						list.Add(pullRequestMenuItem);
					}
				}
				else
			{
				list.Add(RepositoryUserControl.Commands.ShowPushBranchWindow.CreateMenuItem("Push '" + branch.Name + "' to 'origin'...", delegate
				{
					RepositoryUserControl.Commands.ShowPushBranchWindow.Execute(repositoryUserControl, null, branch);
				}, isEnabled: false));
			}
			// 远端同步冲突预检：二级菜单列出所有远端分支，选定后立即弹框检测。
			// 不再默认用本地分支名去 upstream 找（远端不一定有同名分支），而是让用户显式选择远端分支。
			MenuItem checkRemoteSyncMenuItem = new MenuItem
			{
				Header = Preferences.PreferencesLocalization.MenuHeader("Check Remote Sync Status...")
			};
			RemoteBranch[] allRemoteBranches = repositoryData.References.RemoteBranches;
			if (allRemoteBranches != null && allRemoteBranches.Length > 0)
			{
				// 按远端名分组，每组用可搜索子菜单（置顶搜索框 + 可滚动分支列表）
				IEnumerable<IGrouping<string, RemoteBranch>> grouped = allRemoteBranches.GroupBy((RemoteBranch rb) => rb.Remote ?? "").OrderBy((IGrouping<string, RemoteBranch> g) => g.Key);
				foreach (IGrouping<string, RemoteBranch> group in grouped)
				{
					MenuItem remoteGroupItem = CreateSearchableRemoteGroupMenuItem(group.Key, group, delegate(RemoteBranch rb)
					{
						RepositoryUserControl.Commands.CheckForkSync.Execute(repositoryUserControl, branch, rb);
					}, isCheckedPredicate: null);
					checkRemoteSyncMenuItem.Items.Add(remoteGroupItem);
				}
			}
			// 没有远端分支时禁用整项
			if (checkRemoteSyncMenuItem.Items.Count == 0)
			{
				checkRemoteSyncMenuItem.IsEnabled = false;
			}
			list.Add(checkRemoteSyncMenuItem);
			list.Add(new Separator());
			if (activeBranch != null && activeBranch != branch)
			{
					list.Add(RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, branch, activeBranch);
					}, !branch.IsActive));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + activeBranch.Name + "' on '" + branch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, activeBranch, branch);
					}, !branch.IsActive));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Interactively Rebase '" + activeBranch.Name + "' on '" + branch.Name + "...", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, activeBranch, branch);
					}));
					list.Add(new Separator());
				}
				list.Add(RepositoryUserControl.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, branch);
				}));
				list.Add(RepositoryUserControl.Commands.ShowCreateTagWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, branch);
				}));
				list.Add(new Separator());
				if (repositoryData.GitFlowSettings != null)
				{
					list.Add(CreateLocalBranchGitFlowMenuItem(repositoryUserControl, gitModule, repositoryData, repositoryData.GitFlowSettings, branch));
					list.Add(new Separator());
				}
				// 跟踪：改为二级菜单（和检查远端同步状态一致），按远端名分组列出远端分支，每组带搜索框。
			MenuItem trackingMenuItem = new MenuItem
			{
				Header = Preferences.PreferencesLocalization.MenuHeader("Tracking")
			};
			RemoteBranch[] trackingRemoteBranches = repositoryData.References.RemoteBranches;
			if (trackingRemoteBranches != null && trackingRemoteBranches.Length > 0)
			{
				// "Remove tracking reference" 放在二级菜单顶层（不属于某个远端分组）
				if (!string.IsNullOrWhiteSpace(branch.UpstreamFullReference))
				{
					MenuItem removeTrackingItem = RepositoryUserControl.Commands.UpdateTrackingReference.CreateMenuItem("Remove tracking reference", delegate
					{
						RepositoryUserControl.Commands.UpdateTrackingReference.Execute(repositoryUserControl, gitModule, branch, null);
					});
					trackingMenuItem.Items.Add(removeTrackingItem);
					trackingMenuItem.Items.Add(new Separator());
				}
				// 按远端名分组，每组用可搜索子菜单
				IEnumerable<IGrouping<string, RemoteBranch>> trackingGrouped = trackingRemoteBranches.GroupBy((RemoteBranch rb) => rb.Remote ?? "").OrderBy((IGrouping<string, RemoteBranch> g) => g.Key);
				foreach (IGrouping<string, RemoteBranch> group in trackingGrouped)
				{
					MenuItem remoteGroupItem = CreateSearchableRemoteGroupMenuItem(group.Key, group, delegate(RemoteBranch rb)
					{
						RepositoryUserControl.Commands.UpdateTrackingReference.Execute(repositoryUserControl, gitModule, branch, rb);
					}, isCheckedPredicate: (RemoteBranch rb) => rb.FullReference == branch.UpstreamFullReference);
					trackingMenuItem.Items.Add(remoteGroupItem);
				}
			}
			if (trackingMenuItem.Items.Count == 0)
			{
				trackingMenuItem.IsEnabled = false;
			}
			list.Add(trackingMenuItem);
			list.Add(RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.CreateMenuItem("Rename '" + branch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(repositoryUserControl, gitModule, repositoryData.References, branch);
				}));
				list.Add(RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.CreateMenuItem("Delete '" + branch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, branches);
				}, activeBranch != branch));
				if (AiAgent.GetAvailableAiAgents().Length != 0 || ForkPlus.Accounts.AiServices.OpenAiService.IsAiReviewConfigured())
				{
					LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
					if (localBranch != null)
					{
						RemoteBranch remoteMain = repositoryData.References.Upstream(localBranch);
						if (remoteMain != null)
						{
							list.Add(new Separator());
						list.Add(RevisionListViewUserControl.CreateBranchAiCodeReviewMenuItem(repositoryUserControl, gitModule, branch, remoteMain, commitGraphCache));
					}
				}
			}
			list.Add(new Separator());
			list.Add(RepositoryUserControl.Commands.ShowRepositoryStatisticsWindow.CreateMenuItem(
				PreferencesLocalization.Current("Code statistics..."), delegate
			{
				// 以分支名作为初始 refSpec 打开统计窗口，并滚动到代码行数统计区域
				RepositoryUserControl.Commands.ShowRepositoryStatisticsWindow.Execute(gitModule, branch.Name, true);
			}));
			list.Add(new Separator());
			list.Add(RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Branch Name", delegate
			{
				RepositoryUserControl.Commands.CopyReferenceName.Execute(branch);
			}));
			list.AddRange(GetReferenceCustomCommands(repositoryUserControl, gitModule, _repositoryData, branch));
		}
		else if (branches.Length > 1)
			{
				RemoteBranch[] remoteBranches = repositoryData.References.RemoteBranches;
				list.Add(RepositoryUserControl.Commands.FastForward.CreateMenuItem($"Fast-Forward {branches.Length} branches to their upstreams", delegate
				{
					foreach (LocalBranch localBranch in branches)
					{
						string upstreamFullReference = localBranch.UpstreamFullReference;
						if (upstreamFullReference == null)
						{
							continue;
						}
						RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
						if (remoteBranch != null)
						{
							if (!localBranch.IsInfrontUpstream(remoteBranch, gitModule, commitGraphCache))
							{
								RepositoryUserControl.Commands.FastForward.Execute(repositoryUserControl, localBranch);
							}
							else
							{
								RepositoryUserControl.Commands.FastForwardPull.Execute(repositoryUserControl, localBranch);
							}
						}
					}
				}));
				if (remotes.Length == 1)
				{
					Remote remote = remotes[0];
					list.Add(RepositoryUserControl.Commands.ShowPushMultipleBranchesWindow.CreateMenuItem($"Push {branches.Length} branches to '{remote.Name}'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushMultipleBranchesWindow.Execute(repositoryUserControl, branches, remote);
					}));
					list.Add(new Separator());
				}
				else if (remotes.Length != 0)
				{
					MenuItem menuItem = RepositoryUserControl.Commands.ShowPushMultipleBranchesWindow.CreateMenuItem($"Push {branches.Length} branches to");
					foreach (Remote remote in remotes)
					{
						Remote currentRemote = remote;
						menuItem.Items.Add(RepositoryUserControl.Commands.ShowPushMultipleBranchesWindow.CreateMenuItem(currentRemote.Name + "...", delegate
						{
							RepositoryUserControl.Commands.ShowPushMultipleBranchesWindow.Execute(repositoryUserControl, branches, currentRemote);
						}));
					}
					list.Add(menuItem);
					list.Add(new Separator());
				}
				list.Add(RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.CreateMenuItem($"Delete {branches.Length} Branches...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, branches);
				}));
			}
			return list;
		}

		private Control CreateLocalBranchGitFlowMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, GitFlowSettings gitFlowSettings, LocalBranch gitFlowBranch)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = Preferences.PreferencesLocalization.MenuHeader("Git Flow")
			};
			LocalBranch localBranch = gitFlowBranch;
			if (localBranch != null && localBranch.IsFeatureBranch(gitFlowSettings))
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
				}));
			}
			else
			{
				LocalBranch localBranch2 = gitFlowBranch;
				if (localBranch2 != null && localBranch2.IsReleaseBranch(gitFlowSettings))
				{
					menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
					}));
				}
				else
				{
					LocalBranch localBranch3 = gitFlowBranch;
					if (localBranch3 != null && localBranch3.IsHotfixBranch(gitFlowSettings))
					{
						menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.CreateMenuItem("Finish '" + gitFlowBranch.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.Execute(repositoryUserControl, gitModule, repositoryData, gitFlowBranch);
						}));
					}
				}
			}
			if (menuItem.Items.Count > 0)
			{
				menuItem.Items.Add(new Separator());
			}
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.Execute(repositoryUserControl, gitModule);
			}));
			return menuItem;
		}

		/// <summary>
		/// 创建一个可搜索的远端分组子菜单：Popup 顶部置顶搜索框（不随列表滚动），下方是该远端的分支列表。
		/// 用于"跟踪"和"检查远端同步状态"二级菜单的远端分组项。
		/// </summary>
		/// <param name="remoteName">远端名（作为分组 Header）。</param>
		/// <param name="remoteBranches">该远端下的远端分支集合。</param>
		/// <param name="onBranchSelected">用户选定某个远端分支时的回调。</param>
		/// <param name="isCheckedPredicate">可选，判断某个远端分支是否应显示勾选状态（如当前 tracking 的分支）。</param>
		private static MenuItem CreateSearchableRemoteGroupMenuItem(
			string remoteName,
			IEnumerable<RemoteBranch> remoteBranches,
			Action<RemoteBranch> onBranchSelected,
			Func<RemoteBranch, bool> isCheckedPredicate)
		{
			MenuItem groupItem = new MenuItem
			{
				Header = remoteName
			};
			// 应用可搜索子菜单模板（置顶搜索框 + 可滚动分支列表）
			Style searchableStyle = (Style)Application.Current.TryFindResource("SearchableSubmenuMenuItem");
			if (searchableStyle != null)
			{
				groupItem.Style = searchableStyle;
			}
			foreach (RemoteBranch rb in remoteBranches.OrderBy((RemoteBranch b) => b.Name, StringComparer.Ordinal))
			{
				RemoteBranch currentRemoteBranch = rb;
				MenuItem branchItem = new MenuItem
				{
					Header = currentRemoteBranch.ShortName
				};
				if (isCheckedPredicate != null && isCheckedPredicate(currentRemoteBranch))
				{
					branchItem.IsChecked = true;
				}
				branchItem.Click += delegate
				{
					onBranchSelected?.Invoke(currentRemoteBranch);
				};
				groupItem.Items.Add(branchItem);
			}
			// 子菜单打开时，找到模板里的 PART_SearchBox，订阅文本变化做分支过滤。
			// Popup 内容在独立视觉树，且 SubmenuOpened 触发时模板可能尚未完全生成，
			// 因此用 Dispatcher.BeginInvoke 延迟到下一轮渲染后再查找部件。
			groupItem.SubmenuOpened += delegate
			{
				groupItem.Dispatcher.BeginInvoke(new Action(delegate
				{
					PlaceholderTextBox searchBox = FindTemplatePart<PlaceholderTextBox>(groupItem, "PART_SearchBox");
					if (searchBox == null)
					{
						return;
					}
					// 清空上次打开残留的搜索文本
					searchBox.Text = string.Empty;
					searchBox.TextChanged -= SearchBox_TextChanged;
					searchBox.TextChanged += SearchBox_TextChanged;
					// 把分支项缓存到 Tag，供过滤回调使用
					searchBox.Tag = groupItem;
					// 自动聚焦搜索框
					searchBox.Focus();
				}));
			};
			return groupItem;
		}

		/// <summary>搜索框文本变化：隐藏不匹配的分支项（MenuItem.Header 含搜索文本即匹配，不区分大小写）。</summary>
		private static void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			PlaceholderTextBox searchBox = sender as PlaceholderTextBox;
			if (searchBox == null)
			{
				return;
			}
			MenuItem groupItem = searchBox.Tag as MenuItem;
			if (groupItem == null)
			{
				return;
			}
			string filter = (searchBox.Text ?? string.Empty).Trim();
			foreach (object item in groupItem.Items)
			{
				MenuItem branchItem = item as MenuItem;
				if (branchItem == null)
				{
					continue;
				}
				string header = branchItem.Header as string;
				if (string.IsNullOrEmpty(filter) || (header != null && header.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					branchItem.Visibility = Visibility.Visible;
				}
				else
				{
					branchItem.Visibility = Visibility.Collapsed;
				}
			}
			// 过滤改变子项可见性后，WPF 菜单的焦点管理可能把键盘焦点抢到第一个可见 MenuItem，
			// 导致搜索框失焦、无法继续输入。过滤完成后立即把焦点夺回搜索框。
			// 用 Keyboard.Focus（比 Control.Focus 更可靠）并配合 Dispatcher.BeginInvoke 确保在
			// 菜单焦点逻辑之后执行。
			groupItem.Dispatcher.BeginInvoke(new Action(delegate
			{
				if (searchBox.IsVisible && !searchBox.IsKeyboardFocused)
				{
					Keyboard.Focus(searchBox);
					searchBox.Focus();
					// 恢复光标到末尾，避免 Focus 把光标跑到开头影响继续输入
					searchBox.CaretIndex = searchBox.Text.Length;
				}
			}), System.Windows.Threading.DispatcherPriority.Background);
		}

		/// <summary>在 MenuItem 的子菜单 Popup 视觉树里按名字查找模板部件。</summary>
		/// <remarks>
		/// WPF 的 Popup 内容位于独立视觉树（不挂在 MenuItem 的视觉子树里），因此不能直接从
		/// menuItem 往下遍历。先在 menuItem 模板树里找到 PART_Popup（Popup 元素本身在视觉树里），
		/// 再从 popup.Child 往下遍历找到目标部件。
		/// </remarks>
		[Null]
		private static T FindTemplatePart<T>(MenuItem menuItem, string name) where T : FrameworkElement
		{
			if (menuItem == null)
			{
				return null;
			}
			// 先找 PART_Popup：它本身在 menuItem 的模板视觉树里
			Popup popup = FindVisualDescendantByName<Popup>(menuItem, "PART_Popup");
			if (popup == null || popup.Child == null)
			{
				return null;
			}
			// Popup 的 Child 是独立视觉树的根（MenuShadowBorderStyle Border），从这里往下找目标部件
			if (popup.Child is T direct && direct.Name == name)
			{
				return direct;
			}
			return FindVisualDescendantByName<T>(popup.Child, name);
		}

		/// <summary>递归在视觉树里按名字查找指定类型的后代。</summary>
		[Null]
		private static T FindVisualDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
		{
			if (root == null)
			{
				return null;
			}
			int count = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child is T typed && typed.Name == name)
				{
					return typed;
				}
				T found = FindVisualDescendantByName<T>(child, name);
				if (found != null)
				{
					return found;
				}
			}
			return null;
		}

		private IEnumerable<Control> CreateRemoteBranchContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, RemoteBranch[] branches, [Null] LocalBranch activeBranch, SidebarItem sidebarItem, CommitGraphCache commitGraphCache)
		{
			List<Control> list = new List<Control>();
			if (branches.Length == 1)
			{
				RemoteBranch branch = branches.FirstItem();
				list.Add(RepositoryUserControl.Commands.ShowCheckoutBranchWindow.CreateMenuItem("Checkout '" + branch.Name + "'", delegate
				{
					RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, branch);
				}));
				list.Add(new Separator());
				if (activeBranch != null)
				{
					list.Add(RepositoryUserControl.Commands.ShowPullWindow.CreateMenuItem("Pull '" + branch.Name + "' into '" + activeBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPullWindow.Execute(repositoryUserControl, branch);
					}));
					list.Add(new Separator());
					Sha sha = branch.Sha;
					Sha? obj = activeBranch?.Sha;
					bool enabled = sha != obj;
					list.Add(RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, branch, activeBranch);
					}, enabled));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + activeBranch.Name + "' on '" + branch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, activeBranch, branch);
					}, enabled));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Interactively Rebase '" + activeBranch.Name + "' on '" + branch.Name + "...", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, activeBranch, branch);
					}));
					list.Add(new Separator());
				}
				Remote remote = IReadOnlyListExtensions.FirstItem(repositoryData.Remotes.Items, (Remote x) => x.Name == branch.Remote);
				if (remote != null)
				{
					string pullRequestUrl = new RepositoryUrlBuilder(remote.GitUrl).CreatePullRequestUrl(branch.ShortName);
					if (pullRequestUrl != null)
					{
						list.Add(RepositoryUserControl.Commands.CreatePullRequest.CreateMenuItem("Create Pull Request on '" + remote.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.CreatePullRequest.Execute(pullRequestUrl);
						}));
					}
				}
				list.Add(RepositoryUserControl.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, branch);
				}));
				list.Add(RepositoryUserControl.Commands.ShowCreateTagWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, branch);
				}));
				list.Add(new Separator());
				list.Add(RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.CreateMenuItem("Delete '" + branch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(repositoryUserControl, branches);
				}));
				if (AiAgent.GetAvailableAiAgents().Length != 0 || ForkPlus.Accounts.AiServices.OpenAiService.IsAiReviewConfigured())
				{
					LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
					if (localBranch != null)
					{
						RemoteBranch remoteBranch = repositoryData.References.Upstream(localBranch);
						if (remoteBranch != null)
						{
							list.Add(RevisionListViewUserControl.CreateBranchAiCodeReviewMenuItem(repositoryUserControl, gitModule, branch, remoteBranch, commitGraphCache));
						}
					}
				}
				list.Add(new Separator());
				list.Add(RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Branch Name", delegate
				{
					RepositoryUserControl.Commands.CopyReferenceName.Execute(branch);
				}));
				list.AddRange(GetReferenceCustomCommands(repositoryUserControl, gitModule, _repositoryData, branch));
			}
			else if (branches.Length > 1)
			{
				list.Add(RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.CreateMenuItem($"Delete {branches.Length} Remote Branches...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(repositoryUserControl, branches);
				}));
			}
			return list;
		}

		private IEnumerable<Control> CreateTagContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, Tag[] tags, [Null] LocalBranch activeBranch)
		{
			List<Control> list = new List<Control>();
			Remote[] remotes = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			if (tags.Length == 1)
			{
				Tag tag = tags.FirstItem();
				list.Add(RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.CreateMenuItem("Checkout '" + tag.Name + "'", delegate
				{
					RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.Execute(repositoryUserControl, tag, tag.Sha);
				}));
				if (tag.TargetObjectSha.HasValue)
				{
					list.Add(RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.CreateMenuItem("Show '" + tag.Name + "' Details...", delegate
					{
						RepositoryUserControl.Commands.ShowTagDetailsWindow.Execute(repositoryUserControl, tag);
					}));
				}
				list.Add(new Separator());
				if (remotes.Length == 1)
				{
					Remote remote = remotes[0];
					list.Add(RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem("Push '" + tag.Name + "' to '" + remote.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushTagWindowCommand.Execute(repositoryUserControl, tag, remote);
					}));
					list.Add(new Separator());
				}
				else if (remotes.Length > 1)
				{
					MenuItem menuItem = RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem();
					foreach (Remote remote in remotes)
					{
						Remote currentRemote = remote;
						menuItem.Items.Add(RepositoryUserControl.Commands.ShowPushTagWindowCommand.CreateMenuItem("Push to '" + currentRemote.Name + "'...", delegate
						{
							RepositoryUserControl.Commands.ShowPushTagWindowCommand.Execute(repositoryUserControl, tag, currentRemote);
						}));
					}
					list.Add(menuItem);
					list.Add(new Separator());
				}
				if (activeBranch != null)
				{
					Sha sha = tag.Sha;
					Sha? obj = activeBranch?.Sha;
					bool enabled = sha != obj;
					list.Add(RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge into '" + activeBranch.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, tag, activeBranch);
					}, enabled));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + activeBranch.Name + "' on '" + tag.Name + "'...", delegate
					{
						RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, activeBranch, tag);
					}, enabled));
					list.Add(RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Interactively Rebase '" + activeBranch.Name + "' on '" + tag.Name + "...", delegate
					{
						RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, activeBranch, tag);
					}));
					list.Add(new Separator());
				}
				list.Add(RepositoryUserControl.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, tag);
				}));
				list.Add(RepositoryUserControl.Commands.ShowCreateTagWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowCreateTagWindow.Execute(repositoryUserControl, tag);
				}));
				list.Add(new Separator());
				list.Add(RepositoryUserControl.Commands.ShowRemoveTagWindow.CreateMenuItem("Delete '" + tag.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(repositoryUserControl, new Tag[1] { tag });
				}));
				list.Add(new Separator());
				list.Add(RepositoryUserControl.Commands.CopyReferenceName.CreateMenuItem("Copy Tag Name", delegate
				{
					RepositoryUserControl.Commands.CopyReferenceName.Execute(tag);
				}));
				list.AddRange(GetReferenceCustomCommands(repositoryUserControl, gitModule, _repositoryData, tag));
			}
			else if (tags.Length > 1)
			{
				if (remotes.Length == 1)
				{
					Remote remote = remotes[0];
					list.Add(RepositoryUserControl.Commands.ShowPushMultipleTagsWindow.CreateMenuItem($"Push {tags.Length} tags to '{remote.Name}'...", delegate
					{
						RepositoryUserControl.Commands.ShowPushMultipleTagsWindow.Execute(repositoryUserControl, tags, remote);
					}));
					list.Add(new Separator());
				}
				else if (remotes.Length != 0)
				{
					MenuItem menuItem = RepositoryUserControl.Commands.ShowPushMultipleTagsWindow.CreateMenuItem($"Push {tags.Length} tags to");
					foreach (Remote remote in remotes)
					{
						Remote currentRemote = remote;
						menuItem.Items.Add(RepositoryUserControl.Commands.ShowPushMultipleTagsWindow.CreateMenuItem(currentRemote.Name + "...", delegate
						{
							RepositoryUserControl.Commands.ShowPushMultipleTagsWindow.Execute(repositoryUserControl, tags, currentRemote);
						}));
					}
					list.Add(menuItem);
					list.Add(new Separator());
				}
				list.Add(RepositoryUserControl.Commands.ShowRemoveTagWindow.CreateMenuItem($"Delete {tags.Length} Tags...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(repositoryUserControl, tags);
				}));
			}
			return list;
		}

		private static IEnumerable<Control> GetReferenceCustomCommands(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, ForkPlus.Git.Reference reference)
		{
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryData, CustomCommandTarget.Reference);
			IReadOnlyList<CustomCommand> result = null;
			if (reference is LocalBranch)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.LocalBranch) ?? false);
			}
			else if (reference is RemoteBranch)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.RemoteBranch) ?? false);
			}
			else if (reference is Tag)
			{
				result = customCommands.Filter((CustomCommand x) => x.ReferenceTargets?.Contains(CustomCommandRefTarget.Tag) ?? false);
			}
			if (result == null || result.Count == 0)
			{
				yield break;
			}
			yield return new Separator();
			yield return new HeaderMenuItem("Custom Commands");
			List<MenuItem> list = new List<MenuItem>();
			foreach (CustomCommand item in result)
			{
				if (item.OS.IsSupported())
				{
					CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, reference);
					item.AddCustomCommandItem(repositoryUserControl, env, item.Name.Split(Consts.Chars.Slash), 0, list);
				}
			}
			foreach (MenuItem item in list)
			{
				yield return item;
			}
		}

		private static IEnumerable<Control> GetSubmoduleCustomCommands(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, Submodule submodule)
		{
			CustomCommand[] submoduleCustomCommands = CustomCommandManager.Current.GetCustomCommands(repositoryData, CustomCommandTarget.Submodule);
			if (submoduleCustomCommands == null || submoduleCustomCommands.Length == 0)
			{
				yield break;
			}
			yield return new Separator();
			yield return new HeaderMenuItem("Custom Commands");
			foreach (CustomCommand customCommand in submoduleCustomCommands)
			{
				CustomCommand command = customCommand;
				if (!command.OS.IsSupported())
				{
					continue;
				}
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(submodule)
				};
				CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, parameters);
				yield return RepositoryUserControl.Commands.RunCustomCommand.CreateMenuItem(env.ReplaceVariablesWithValues(command.Name), delegate
				{
					RepositoryUserControl.Commands.RunCustomCommand.Execute(repositoryUserControl, command, env);
				});
			}
		}

		private IEnumerable<Control> CreateStashContextMenuItems(RepositoryUserControl repositoryUserControl, StashRevision[] stashes)
		{
			StashRevision stash = stashes.SingleItem();
			if (stash != null)
			{
				yield return RepositoryUserControl.Commands.ShowApplyStashWindow.CreateMenuItem("Apply '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowApplyStashWindow.Execute(repositoryUserControl, stash);
				});
				yield return RepositoryUserControl.Commands.ShowRenameStashWindow.CreateMenuItem("Rename '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRenameStashWindow.Execute(repositoryUserControl, stash);
				});
				yield return RepositoryUserControl.Commands.ShowRemoveStashWindow.CreateMenuItem("Delete '" + stash.Message + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(repositoryUserControl, stashes);
				});
			}
			else if (stashes.Length > 1)
			{
				yield return RepositoryUserControl.Commands.ShowRemoveStashWindow.CreateMenuItem($"Delete {stashes.Length} Stashes...", delegate
				{
					RepositoryUserControl.Commands.ShowRemoveStashWindow.Execute(repositoryUserControl, stashes);
				});
			}
		}

		private IEnumerable<Control> CreateSubmoduleSidebarItemGroupMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, Submodule[] submodules, SubmodulesToUpdate submodulesToUpdate)
		{
			yield return RepositoryUserControl.Commands.ShowAddSubmoduleWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowAddSubmoduleWindow.Execute(repositoryUserControl, gitModule, submodulesToUpdate);
			});
			yield return RepositoryUserControl.Commands.UpdateSubmodule.CreateMenuItem("Update Submodules", delegate
			{
				RepositoryUserControl.Commands.UpdateSubmodule.Execute(repositoryUserControl, gitModule, submodules.Filter((Submodule x) => x.IsActive).ToArray());
			}, submodules.Length != 0);
		}

		private IEnumerable<Control> CreateSubmoduleSidebarItemMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, Submodule[] submodules, SubmodulesToUpdate submodulesToUpdate)
		{
			if (submodules.Length == 1)
			{
				Submodule submodule = submodules[0];
				string submoduleAbsolutePath = gitModule.MakePath(submodule.Path);
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem("Open '" + submodule.Path + "'...", delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, submodules);
				});
				yield return new Separator();
				if (!(ForkPlusSettings.Default.ShellTool is ShellTool.Default))
				{
					yield return MainWindow.Commands.OpenRepositoryInDefaultShellTool.CreateMenuItem(delegate
					{
						MainWindow.Commands.OpenRepositoryInDefaultShellTool.Execute(submoduleAbsolutePath);
					});
				}
				yield return MainWindow.Commands.OpenRepositoryInShellTool.CreateMenuItem(delegate
				{
					MainWindow.Commands.OpenRepositoryInShellTool.Execute(submoduleAbsolutePath);
				}, isEnabled: true, showShortcut: false);
				yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, submodule.Path);
				});
				ExternalProjectEditor[] availableExternalProjectEditors = ExternalProjectEditor.GetAvailableEditors();
				ExternalRepositoryEditor[] availableExternalRepositoryEditors = ExternalRepositoryEditor.GetAvailableEditors();
				if (availableExternalProjectEditors.Length != 0 || availableExternalRepositoryEditors.Length != 0)
				{
					yield return new Separator();
					foreach (ExternalProjectEditor availableExternalProjectEditor in availableExternalProjectEditors)
					{
						ExternalProjectEditor editor = availableExternalProjectEditor;
						foreach (string projectFilePath in editor.GetProjectFilePaths(submoduleAbsolutePath))
						{
							string absoluteProjectFilePath = projectFilePath;
							string text = PathHelper.RelativePathOrFileName(submoduleAbsolutePath, absoluteProjectFilePath);
							yield return MainWindow.Commands.OpenRepositoryInExternalEditor.CreateMenuItem("Open '" + text + "' in " + editor.Name, delegate
							{
								editor.OpenProject(absoluteProjectFilePath);
							}, isEnabled: true, new Image
							{
								Source = editor.Icon
							});
						}
					}
					foreach (ExternalRepositoryEditor availableExternalRepositoryEditor in availableExternalRepositoryEditors)
					{
						ExternalRepositoryEditor editor2 = availableExternalRepositoryEditor;
						yield return MainWindow.Commands.OpenRepositoryInExternalEditor.CreateMenuItem("Open in " + editor2.Name, delegate
						{
							MainWindow.Commands.OpenRepositoryInExternalEditor.Execute(submoduleAbsolutePath, editor2);
						}, isEnabled: true, new Image
						{
							Source = editor2.Icon
						});
					}
					yield return new Separator();
				}
				yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, new ShowFileHistoryWindowCommand.Mode.File(submodule.Path), null);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.UpdateSubmodule.CreateMenuItem("Update '" + submodule.Path + "'", delegate
				{
					RepositoryUserControl.Commands.UpdateSubmodule.Execute(repositoryUserControl, gitModule, submodules);
				});
				yield return RepositoryUserControl.Commands.MoveSubmodule.CreateMenuItem("Move '" + submodule.Path + "'...", delegate
				{
					RepositoryUserControl.Commands.MoveSubmodule.Execute(repositoryUserControl, gitModule, submodule);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowDeleteSubmoduleWindow.CreateMenuItem("Delete '" + submodule.Path + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowDeleteSubmoduleWindow.Execute(repositoryUserControl, gitModule, submodule);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowAddSubmoduleWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowAddSubmoduleWindow.Execute(repositoryUserControl, gitModule, submodulesToUpdate);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { submodule.Path });
				});
				yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, new string[1] { submodule.Path });
				});
				foreach (Control item in GetSubmoduleCustomCommands(repositoryUserControl, gitModule, _repositoryData, submodule))
				{
					yield return item;
				}
			}
			else if (submodules.Length > 1)
			{
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem($"Open {submodules.Length} submodules", delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, submodules);
				});
				yield return RepositoryUserControl.Commands.UpdateSubmodule.CreateMenuItem($"Update {submodules.Length} submodules", delegate
				{
					RepositoryUserControl.Commands.UpdateSubmodule.Execute(repositoryUserControl, gitModule, submodules);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowAddSubmoduleWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowAddSubmoduleWindow.Execute(repositoryUserControl, gitModule, submodulesToUpdate);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(submodules.Map((Submodule x) => x.Path));
				});
				yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, submodules.Map((Submodule x) => x.Path));
				});
			}
		}

		private IEnumerable<Control> CreateWorktreesSidebarItemGroupMenuItems(RepositoryUserControl repositoryUserControl)
		{
			yield return MainWindow.Commands.ShowCreateWorktreeWindow.CreateMenuItem(delegate
			{
				MainWindow.Commands.ShowCreateWorktreeWindow.Execute(repositoryUserControl);
			});
		}

		private IEnumerable<Control> CreateWorktreeSidebarItemMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, Worktree[] worktrees)
		{
			if (worktrees.Length == 1)
			{
				Worktree worktree = worktrees[0];
				yield return RepositoryUserControl.Commands.OpenWorktree.CreateMenuItem("Open '" + worktree.FriendlyName + "'...", delegate
				{
					RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { worktree });
				});
				yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, worktree.Path);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowDeleteWorktreeWindow.CreateMenuItem("Delete '" + worktree.FriendlyName + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowDeleteWorktreeWindow.Execute(repositoryUserControl, worktree);
				}, !worktree.IsActive);
				yield return new Separator();
				yield return RepositoryUserControl.Commands.CopyWorktreePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyWorktreePaths.Execute(new Worktree[1] { worktree });
				});
			}
			else if (worktrees.Length > 1)
			{
				yield return RepositoryUserControl.Commands.OpenWorktree.CreateMenuItem($"Open {worktrees.Length} worktrees...", delegate
				{
					RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, worktrees);
				});
				yield return new Separator();
				yield return RepositoryUserControl.Commands.CopyWorktreePaths.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.CopyWorktreePaths.Execute(worktrees);
				});
			}
		}

		private bool IsTruncated(FolderSidebarItem section)
		{
			return !_nonTruncatedItems.Contains(section.Title);
		}

		private void UpdateFilter(string filterString)
		{
			Reload(_repositoryData, forceRefresh: false, filterString);
			SidebarTreeView.FilterString = filterString;
		}

		private SidebarGroupItem CreateSidebarGroupItem(SidebarGroupItem.Group sidebarGroup, string name = null)
		{
			return new SidebarGroupItem(Preferences.PreferencesLocalization.Current(sidebarGroup.ToString()), null, sidebarGroup, this);
		}

		private void SidebarTreeView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Point position = e.GetPosition(SidebarTreeView);
			if (SidebarTreeView.GetObjectAtPoint<TreeViewControlItem>(position) is MultiselectionTreeViewItem { IsSelected: not false })
			{
				SidebarTreeView_SelectionChanged(this, e);
			}
		}

		private void SidebarTreeView_SelectionChanged(object sender, EventArgs e)
		{
			if (_updateRepositoryDataInProgress || _selectionInProgress)
			{
				return;
			}
			RepositoryUserControl.ActivateRevisionView();
			SidebarItem[] array = SidebarTreeView.SelectedItems.CompactMap((object x) => x as SidebarItem);
			MultiselectionTreeViewItem[] items = array;
			items.RefreshSelectionType();
			if (array.Length < 1)
			{
				return;
			}
			if (array[0] is StashSidebarItem stashSidebarItem)
			{
				RepositoryUserControl.SelectRevision(stashSidebarItem.Stash.Sha);
			}
			else if (array[0] is ReferenceSidebarItem)
			{
				RepositoryUserControl.SelectRevisions(array.CompactMapStruct((SidebarItem x) => (x as ReferenceSidebarItem)?.Reference.Sha), fetchIfNeeded: true);
			}
			else if (array[0] is WorktreeSidebarItem worktreeSidebarItem)
			{
				Sha? sha = WorktreeSha(worktreeSidebarItem.Worktree);
				if (sha.HasValue)
				{
					Sha valueOrDefault = sha.GetValueOrDefault();
					RepositoryUserControl.SelectRevisions(new Sha[1] { valueOrDefault }, fetchIfNeeded: true);
				}
			}
			else if (array[0] is MainWorktreeSidebarItem mainWorktreeSidebarItem)
			{
				Sha? sha = WorktreeSha(mainWorktreeSidebarItem.Worktree);
				if (sha.HasValue)
				{
					Sha valueOrDefault2 = sha.GetValueOrDefault();
					RepositoryUserControl.SelectRevisions(new Sha[1] { valueOrDefault2 }, fetchIfNeeded: true);
				}
			}
		}

		private Sha? WorktreeSha(Worktree worktree)
		{
			ForkPlus.Git.Reference reference = IReadOnlyListExtensions.FirstItem(_repositoryData.References.Items, (ForkPlus.Git.Reference x) => x.FullReference == worktree.HeadString);
			if (reference == null)
			{
				return Sha.Parse(worktree.HeadString);
			}
			return reference.Sha;
		}

		private void ReferenceSortOrderChanged(object sender, EventArgs args)
		{
			Reload(_repositoryData, forceRefresh: true, SidebarTreeView.FilterString);
		}

		private void Reload(RepositoryData newRepositoryData, bool forceRefresh, string sidebarFilterString)
		{
			RepositoryData repositoryData = _repositoryData;
			if (!forceRefresh && sidebarFilterString == SidebarTreeView.FilterString && repositoryData.References == newRepositoryData.References && repositoryData.UpstreamStatus == newRepositoryData.UpstreamStatus && repositoryData.Stashes == newRepositoryData.Stashes && repositoryData.Remotes == newRepositoryData.Remotes && repositoryData.Submodules == newRepositoryData.Submodules && repositoryData.Worktrees == newRepositoryData.Worktrees)
			{
				return;
			}
			UpdateVisibleTabs(newRepositoryData);
			bool sidebarFilterEnabled = !string.IsNullOrEmpty(sidebarFilterString);
			using (SidebarTreeView.LockUpdates())
			{
				_updateRepositoryDataInProgress = true;
				UpdateSidebarItems(repositoryData, newRepositoryData, sidebarFilterEnabled, forceRefresh);
				ExpandedTreeViewElement[] expandedSidebarItems = RepositoryUserControl.GitModule.Settings.ExpandedSidebarItems;
				if (expandedSidebarItems != null)
				{
					SidebarTreeView.SetExpandedItems(expandedSidebarItems);
				}
				else
				{
					_pinned.IsExpanded = true;
					_branches.IsExpanded = true;
					_remotes.IsExpanded = true;
					_stashes.IsExpanded = true;
				}
				if (_shouldExpandWorktrees)
				{
					_worktrees.IsExpanded = true;
					foreach (MultiselectionTreeViewItem child in _worktrees.Children)
					{
						child.IsExpanded = true;
					}
				}
				if (repositoryData != null && repositoryData.References.ActiveBranch != newRepositoryData?.References.ActiveBranch)
				{
					RevealActiveBranch();
				}
				_updateRepositoryDataInProgress = false;
			}
			SidebarTreeView.Refilter();
			_repositoryData = newRepositoryData;
			if (repositoryData == null && _root.Children.FirstOrDefault() is SidebarGroupItem node)
			{
				SidebarTreeView.ScrollIntoView(node);
			}
		}

		private void UpdateVisibleTabs(RepositoryData repositoryData)
		{
			Remote[] array = repositoryData.Remotes.Items.Filter((Remote x) => x.Account != null).ToArray();
			if (array.Length != 0)
			{
				Remote remote = array[0];
				ServiceRadioButton.Show();
				ServiceRadioButton.Content = remote.IconGeometry;
				ServiceTabItem.SetServices(array);
			}
			else
			{
				if (ServiceTabItem.IsSelected)
				{
					BranchesTabItem.IsSelected = true;
				}
				ServiceRadioButton.Collapse();
			}
		}

		private void UpdateSidebarItems(RepositoryData oldRepositoryData, RepositoryData repositoryData, bool sidebarFilterEnabled, bool forceRefresh = false)
		{
			_submodules.Children.Clear();
			Func<SidebarItem, SidebarItem, bool> predicate = ComparerFor(ForkPlusSettings.Default.LocalBranchSortOrder);
			Func<SidebarItem, SidebarItem, bool> predicate2 = ComparerFor(ForkPlusSettings.Default.RemoteBranchSortOrder);
			Func<SidebarItem, SidebarItem, bool> predicate3 = ComparerFor(ForkPlusSettings.Default.TagSortOrder);
			Diff(oldRepositoryData.Remotes.Items, repositoryData.Remotes.Items, _remoteComparer, forceRefresh: false, out var removed, out var added);
			Remote[] array = removed;
			foreach (Remote other in array)
			{
				foreach (MultiselectionTreeViewItem child in _remotes.Children)
				{
					if ((child as RemoteSidebarItem).Remote.DataEquals(other))
					{
						_remotes.Children.Remove(child);
						break;
					}
				}
			}
			array = added;
			foreach (Remote remote in array)
			{
				InsertItem(_remotes, new FilterableRemoteSidebarItem(remote.Name, _remotes, remote, this), ByTypeThenByTitlePredicate);
			}
			RepositoryReferences references = repositoryData.References;
			bool flag = removed.Length != 0 && added.Length != 0;
			Diff(oldRepositoryData.References.Pinned, references.Pinned, _referenceComparer, forceRefresh || flag, out var removed2, out var added2);
			ForkPlus.Git.Reference[] array2 = removed2;
			foreach (ForkPlus.Git.Reference reference in array2)
			{
				RemoveItemWithReference(reference, _pinned, reference.Name);
			}
			array2 = added2;
			foreach (ForkPlus.Git.Reference reference2 in array2)
			{
				LocalBranch localBranch2 = reference2 as LocalBranch;
				if (localBranch2 != null)
				{
					AddItem(reference2.Name, _pinned, (string name) => new LocalBranchSidebarItem(this, name, _branches, localBranch2), ByTypeThenByTitlePredicate, filterableFolder: false);
					continue;
				}
				if (reference2 is RemoteBranch remoteBranch)
				{
					AddRemoteBranchItem(repositoryData, _pinned, remoteBranch, ByTypeThenByTitlePredicate, filterableFolder: false);
					continue;
				}
				Tag tag2 = reference2 as Tag;
				if (tag2 != null)
				{
					AddItem(reference2.Name, _pinned, (string name) => new TagSidebarItem(name, _tags, tag2), ByTypeThenByTitlePredicate, filterableFolder: false);
				}
			}
			if (_branches.Children.FirstOrDefault((MultiselectionTreeViewItem x) => x is TruncateSidebarItem) is TruncateSidebarItem item)
			{
				_branches.Children.Remove(item);
			}
			bool truncate = ForkPlusSettings.Default.LocalBranchSortOrder == ReferenceSortOrder.RecentlyUsed && IsTruncated(_branches) && !sidebarFilterEnabled;
			LocalBranch[] localBranches = GetLocalBranches(references, truncate);
			array2 = _oldLocalBranches;
			ForkPlus.Git.Reference[] old = array2;
			array2 = localBranches;
			Diff(old, array2, _referenceComparer, forceRefresh, out var removed3, out var added3);
			array2 = removed3;
			foreach (ForkPlus.Git.Reference reference3 in array2)
			{
				RemoveItemWithReference(reference3, _branches, reference3.Name);
			}
			array2 = added3;
			foreach (ForkPlus.Git.Reference reference4 in array2)
			{
				LocalBranch localBranch = reference4 as LocalBranch;
				if (localBranch != null)
				{
					AddItem(reference4.Name, _branches, (string name) => new LocalBranchSidebarItem(this, name, _branches, localBranch), predicate);
				}
			}
			if (ForkPlusSettings.Default.LocalBranchSortOrder == ReferenceSortOrder.RecentlyUsed && references.LocalBranches.Length > 20)
			{
				string title = Preferences.PreferencesLocalization.Current(IsTruncated(_branches) ? $"Show all local branches ({references.LocalBranches.Length})" : "Show less");
				_branches.Children.Insert(_branches.Children.Count, new TruncateSidebarItem(title, _branches));
			}
			array2 = oldRepositoryData.References.RemoteBranches;
			ForkPlus.Git.Reference[] old2 = array2;
			array2 = references.RemoteBranches;
			Diff(old2, array2, _referenceComparer, forceRefresh || flag, out var removed4, out var added4);
			array2 = removed4;
			foreach (ForkPlus.Git.Reference reference5 in array2)
			{
				RemoveRemoteBranchItem(reference5 as RemoteBranch, _remotes);
			}
			array2 = added4;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] is RemoteBranch remoteBranch2)
				{
					AddRemoteBranchItem(repositoryData, _remotes, remoteBranch2, predicate2);
				}
			}
			if (_tags.Children.FirstOrDefault((MultiselectionTreeViewItem x) => x is TruncateSidebarItem) is TruncateSidebarItem item2)
			{
				_tags.Children.Remove(item2);
			}
			bool truncate2 = ForkPlusSettings.Default.TagSortOrder == ReferenceSortOrder.RecentlyUsed && IsTruncated(_tags) && !sidebarFilterEnabled;
			Tag[] tags = GetTags(references, truncate2);
			array2 = _oldTags;
			ForkPlus.Git.Reference[] old3 = array2;
			array2 = tags;
			Diff(old3, array2, _referenceComparer, forceRefresh, out var removed5, out var added5);
			array2 = removed5;
			foreach (ForkPlus.Git.Reference reference6 in array2)
			{
				RemoveItemWithReference(reference6, _tags, reference6.Name);
			}
			array2 = added5;
			foreach (ForkPlus.Git.Reference reference7 in array2)
			{
				Tag tag = reference7 as Tag;
				if (tag != null)
				{
					AddItem(reference7.Name, _tags, (string name) => new TagSidebarItem(name, _tags, tag), predicate3);
				}
			}
			if (ForkPlusSettings.Default.TagSortOrder == ReferenceSortOrder.RecentlyUsed && references.Tags.Length > 20)
			{
				string title2 = Preferences.PreferencesLocalization.Current(IsTruncated(_tags) ? $"Show all tags ({references.Tags.Length})" : "Show less");
				_tags.Children.Insert(_tags.Children.Count, new TruncateSidebarItem(title2, _tags));
			}
			RepositoryWorktrees worktrees = repositoryData.Worktrees;
			Worktree[] items = worktrees.Items;
			if (worktrees.MainWorktree.HasValue || items.Length > 0)
			{
				_worktrees.Children.Clear();
				Worktree? mainWorktree = worktrees.MainWorktree;
				SidebarItem sidebarItem = _worktrees;
				if (mainWorktree.HasValue)
				{
					Worktree valueOrDefault = mainWorktree.GetValueOrDefault();
					if (!valueOrDefault.IsActive)
					{
						MainWorktreeSidebarItem mainWorktreeSidebarItem = new MainWorktreeSidebarItem(valueOrDefault.FriendlyName, _worktrees, valueOrDefault, this);
						_worktrees.Children.Add(mainWorktreeSidebarItem);
						sidebarItem = mainWorktreeSidebarItem;
					}
				}
				Worktree[] array3 = items;
				for (int i = 0; i < array3.Length; i++)
				{
					Worktree worktree = array3[i];
					string text = worktree.FriendlyName;
					mainWorktree = items.FirstItemStruct((Worktree x) => x.Path != worktree.Path && x.FriendlyName == worktree.FriendlyName);
					if (mainWorktree.HasValue)
					{
						Worktree valueOrDefault2 = mainWorktree.GetValueOrDefault();
						string item3 = PathHelper.FindFirstDifferentComponent(worktree.Path, valueOrDefault2.Path).Item1;
						if (item3 != null)
						{
							text = text + " (" + item3 + ")";
						}
					}
					sidebarItem.Children.Add(new WorktreeSidebarItem(text, sidebarItem, worktree));
				}
				if (!_root.Children.AnyItem((MultiselectionTreeViewItem x) => x == _worktrees))
				{
					_root.Children.Insert(0, _worktrees);
					_shouldExpandWorktrees = true;
				}
			}
			else if (_root.Children.AnyItem((MultiselectionTreeViewItem x) => x == _worktrees))
			{
				_worktrees.Children.Clear();
				_root.Children.Remove(_worktrees);
			}
			int index = (_root.Children.AnyItem((MultiselectionTreeViewItem x) => x == _worktrees) ? 1 : 0);
			bool flag2 = _root.Children.AnyItem((MultiselectionTreeViewItem x) => x == _pinned);
			if (_pinned.Children.Count > 0)
			{
				if (!flag2)
				{
					_root.Children.Insert(index, _pinned);
				}
			}
			else if (flag2)
			{
				_root.Children.Remove(_pinned);
			}
			UpdateUpstreamStatus(_pinned.Children, repositoryData);
			UpdateUpstreamStatus(_branches.Children, repositoryData);
			UpdateFilterState(_pinned.Children, repositoryData.References);
			UpdateFilterState(_branches.Children, repositoryData.References);
			UpdateFilterState(_remotes.Children, repositoryData.References);
			UpdateFilterState(_tags.Children, repositoryData.References);
			UpdatePinnedStatus(_pinned.Children, repositoryData.References);
			UpdatePinnedStatus(_branches.Children, repositoryData.References);
			UpdatePinnedStatus(_remotes.Children, repositoryData.References);
			UpdatePinnedStatus(_tags.Children, repositoryData.References);
			bool flag3 = IsTruncated(_stashes) && !sidebarFilterEnabled;
			if (oldRepositoryData.Stashes != repositoryData.Stashes || flag3 != _oldTruncateStashes)
			{
				_stashes.Children.Clear();
				StashRevision[] stashes = GetStashes(repositoryData.Stashes, flag3);
				foreach (StashRevision stashRevision in stashes)
				{
					_stashes.Children.Add(new StashSidebarItem(stashRevision.Message, _stashes, stashRevision));
				}
				if (repositoryData.Stashes.Items.Length > 20)
				{
					string title3 = Preferences.PreferencesLocalization.Current(IsTruncated(_stashes) ? $"Show all stashes ({repositoryData.Stashes.Items.Length})" : "Show less");
					_stashes.Children.Insert(_stashes.Children.Count, new TruncateSidebarItem(title3, _stashes));
				}
			}
			Submodule[] items2 = repositoryData.Submodules.Items;
			foreach (Submodule submodule in items2)
			{
				AddItem(submodule.Path, _submodules, (string name) => new SubmoduleSidebarItem(name, _submodules, submodule), ByTypeThenByTitlePredicate, filterableFolder: false);
			}
			_oldLocalBranches = localBranches;
			_oldTags = tags;
			_oldTruncateStashes = flag3;
		}

		private StashRevision[] GetStashes(RepositoryStashes stashes, bool truncate)
		{
			if (truncate)
			{
				return stashes.Items.Subsequence(0, 20);
			}
			return stashes.Items;
		}

		private Tag[] GetTags(RepositoryReferences references, bool truncate)
		{
			IComparer<ForkPlus.Git.Reference> comparer = ReferenceComparerFor(ForkPlusSettings.Default.TagSortOrder);
			Tag[] array = references.Tags.CopyArray();
			ForkPlus.Git.Reference[] array2 = array;
			Array.Sort(array2, comparer);
			if (truncate)
			{
				return array.Subsequence(0, 20);
			}
			return array;
		}

		private static LocalBranch[] GetLocalBranches(RepositoryReferences references, bool truncate)
		{
			IComparer<ForkPlus.Git.Reference> comparer = ReferenceComparerFor(ForkPlusSettings.Default.LocalBranchSortOrder);
			LocalBranch[] array = references.LocalBranches.CopyArray();
			ForkPlus.Git.Reference[] array2 = array;
			Array.Sort(array2, comparer);
			if (truncate)
			{
				return array.Subsequence(0, 20);
			}
			return array;
		}

		private static void UpdateFilterState(MultiselectionTreeViewItemCollection children, RepositoryReferences references)
		{
			foreach (MultiselectionTreeViewItem child in children)
			{
				if (child is ReferenceSidebarItem referenceSidebarItem)
				{
					referenceSidebarItem.FilterState = GetFilterState(references, referenceSidebarItem.Reference);
				}
				else if (child is FolderSidebarItem folderSidebarItem)
				{
					if (child is FilterableFolderSidebarItem { FullReference: var fullReference } filterableFolderSidebarItem)
					{
						filterableFolderSidebarItem.FilterState = GetFilterState(references, fullReference);
					}
					else if (child is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
					{
						filterableRemoteSidebarItem.FilterState = GetFilterState(references, filterableRemoteSidebarItem.FullReference);
					}
					UpdateFilterState(folderSidebarItem.Children, references);
				}
			}
		}

		private static void UpdatePinnedStatus(MultiselectionTreeViewItemCollection children, RepositoryReferences references)
		{
			foreach (MultiselectionTreeViewItem child in children)
			{
				if (child is ReferenceSidebarItem referenceSidebarItem)
				{
					referenceSidebarItem.Pinned = references.IsPinned(referenceSidebarItem.Reference);
				}
				else if (child is FolderSidebarItem folderSidebarItem)
				{
					UpdatePinnedStatus(folderSidebarItem.Children, references);
				}
			}
		}

		private static void UpdateUpstreamStatus(MultiselectionTreeViewItemCollection children, RepositoryData repositoryData)
		{
			foreach (MultiselectionTreeViewItem child in children)
			{
				if (child is LocalBranchSidebarItem localBranchSidebarItem)
				{
					localBranchSidebarItem.UpstreamStatus = repositoryData.UpstreamStatus.GetUpstreamStatus(localBranchSidebarItem.LocalBranch);
				}
				else if (child is FolderSidebarItem folderSidebarItem)
				{
					UpdateUpstreamStatus(folderSidebarItem.Children, repositoryData);
				}
			}
		}

		private void AddItem(string name, FolderSidebarItem root, Func<string, SidebarItem> createSidebarItem, Func<SidebarItem, SidebarItem, bool> predicate, bool filterableFolder = true)
		{
			FolderSidebarItem parent = root;
			string[] array = name.Split('/');
			int num = array.Length - 1;
			for (int i = 0; i < num; i++)
			{
				parent = FindOrCreateFolder(parent, array[i], predicate, filterableFolder);
			}
			string arg = array[num];
			SidebarItem item = createSidebarItem(arg);
			InsertItem(parent, item, predicate);
		}

		private void InsertItem(FolderSidebarItem parent, SidebarItem item, Func<SidebarItem, SidebarItem, bool> predicate)
		{
			int index = BinarySearch(parent.Children, item, predicate);
			parent.Children.Insert(index, item);
		}

		private LocalBranchSidebarItem FindActiveBranchItem(FolderSidebarItem parent)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				if (child is LocalBranchSidebarItem localBranchSidebarItem)
				{
					if (localBranchSidebarItem.LocalBranch.IsActive)
					{
						return localBranchSidebarItem;
					}
				}
				else if (child is FolderSidebarItem parent2)
				{
					LocalBranchSidebarItem localBranchSidebarItem2 = FindActiveBranchItem(parent2);
					if (localBranchSidebarItem2 != null)
					{
						return localBranchSidebarItem2;
					}
				}
			}
			return null;
		}

		private static void RemoveItemWithReference(ForkPlus.Git.Reference reference, FolderSidebarItem root, string name)
		{
			FolderSidebarItem folderSidebarItem = root;
			string[] array = name.Split('/');
			int num = array.Length - 1;
			for (int i = 0; i < num; i++)
			{
				folderSidebarItem = FindFolder(folderSidebarItem, array[i]);
				if (folderSidebarItem == null)
				{
					Log.Error("Can't find parent folder for '" + name + "'");
					return;
				}
			}
			foreach (MultiselectionTreeViewItem child in folderSidebarItem.Children)
			{
				if (child is ReferenceSidebarItem && (child as ReferenceSidebarItem).Reference.ReferenceEquals(reference))
				{
					RemoveItemAndEmptyParents(root, folderSidebarItem, child);
					folderSidebarItem.Children.Remove(child);
					break;
				}
			}
		}

		private static void RemoveItemAndEmptyParents(FolderSidebarItem root, FolderSidebarItem parent, MultiselectionTreeViewItem item)
		{
			parent.Children.Remove(item);
			if (parent != root && parent.Children.Count == 0 && parent.Parent is FolderSidebarItem)
			{
				RemoveItemAndEmptyParents(root, parent.ParentItem as FolderSidebarItem, parent);
			}
		}

		private void RemoveRemoteBranchItem(RemoteBranch remoteBranch, FolderSidebarItem parent)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				if (child is FolderSidebarItem folderSidebarItem && child.Title == remoteBranch.Remote)
				{
					RemoveItemWithReference(remoteBranch, folderSidebarItem, remoteBranch.ShortName);
					if (parent == _pinned && folderSidebarItem.Children.Count == 0)
					{
						_pinned.Children.Remove(folderSidebarItem);
					}
					break;
				}
			}
		}

		[Null]
		private static FolderSidebarItem FindFolder(FolderSidebarItem parent, string title)
		{
			if (parent.Children.FirstOrDefault((MultiselectionTreeViewItem x) => x.Title == title && x is FolderSidebarItem) is FolderSidebarItem result)
			{
				return result;
			}
			return null;
		}

		private FolderSidebarItem FindOrCreateFolder(FolderSidebarItem parent, string title, Func<SidebarItem, SidebarItem, bool> predicate, bool filterableFolder)
		{
			if (parent.Children.FirstOrDefault((MultiselectionTreeViewItem x) => x.Title == title && x is FolderSidebarItem) is FolderSidebarItem result)
			{
				return result;
			}
			FolderSidebarItem folderSidebarItem = (filterableFolder ? new FilterableFolderSidebarItem(title, parent, this) : new FolderSidebarItem(title, parent, this));
			int index = BinarySearch(parent.Children, folderSidebarItem, predicate);
			parent.Children.Insert(index, folderSidebarItem);
			return folderSidebarItem;
		}

		private static int BinarySearch(MultiselectionTreeViewItemCollection items, SidebarItem item, Func<SidebarItem, SidebarItem, bool> predicate)
		{
			int num = 0;
			int num2 = items.Count;
			while (num != num2)
			{
				int num3 = (num + num2) / 2;
				if (predicate(items[num3] as SidebarItem, item))
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			return num;
		}

		private static Func<SidebarItem, SidebarItem, bool> ComparerFor(ReferenceSortOrder sortOrder)
		{
			return sortOrder switch
			{
				ReferenceSortOrder.Alphabetically => ByTypeThenByTitlePredicate, 
				ReferenceSortOrder.AlphabeticallyBackward => ByTypeThenByTitleBackwardPredicate, 
				ReferenceSortOrder.RecentlyUsed => ByTypeThenByDatePredicate, 
				_ => ByTypeThenByTitlePredicate, 
			};
		}

		private static bool ByTypeThenByTitlePredicate(SidebarItem l, SidebarItem r)
		{
			if (l is FolderSidebarItem == r is FolderSidebarItem)
			{
				return NaturalStringComparer.Instance.Compare(l.Title, r.Title) < 0;
			}
			return l is FolderSidebarItem;
		}

		private static bool ByTypeThenByTitleBackwardPredicate(SidebarItem l, SidebarItem r)
		{
			if (l is FolderSidebarItem == r is FolderSidebarItem)
			{
				return NaturalStringComparer.Instance.Compare(l.Title, r.Title) >= 0;
			}
			return l is FolderSidebarItem;
		}

		private static bool ByTypeThenByDatePredicate(SidebarItem l, SidebarItem r)
		{
			if (l is ReferenceSidebarItem referenceSidebarItem && r is ReferenceSidebarItem referenceSidebarItem2)
			{
				return referenceSidebarItem.Reference.CommitterDate.CompareTo(referenceSidebarItem2.Reference.CommitterDate) > 0;
			}
			if (l is FolderSidebarItem == r is FolderSidebarItem)
			{
				return NaturalStringComparer.Instance.Compare(l.Title, r.Title) < 0;
			}
			return l is FolderSidebarItem;
		}

		private static ReferenceFilterState GetFilterState(RepositoryReferences references, ForkPlus.Git.Reference reference)
		{
			return GetFilterState(references, reference.FullReference);
		}

		private static ReferenceFilterState GetFilterState(RepositoryReferences references, string fullReference)
		{
			return references.GetFilterState(fullReference);
		}

		private void AddRemoteBranchItem(RepositoryData repositoryData, FolderSidebarItem remotes, RemoteBranch remoteBranch, Func<SidebarItem, SidebarItem, bool> predicate, bool filterableFolder = true)
		{
			foreach (MultiselectionTreeViewItem child in remotes.Children)
			{
				FolderSidebarItem folderItem = child as FolderSidebarItem;
				if (folderItem != null && folderItem.Title == remoteBranch.Remote)
				{
					AddItem(remoteBranch.ShortName, folderItem, (string name) => new RemoteBranchSidebarItem(this, name, folderItem, remoteBranch), predicate, filterableFolder);
					return;
				}
			}
			Remote[] items = repositoryData.Remotes.Items;
			foreach (Remote remote in items)
			{
				if (remote.Name == remoteBranch.Remote)
				{
					RemoteSidebarItem remoteItem = (filterableFolder ? new FilterableRemoteSidebarItem(remote.Name, remotes, remote, this) : new RemoteSidebarItem(remote.Name, remotes, remote, this));
					remotes.Children.Add(remoteItem);
					AddItem(remoteBranch.ShortName, remoteItem, (string name) => new RemoteBranchSidebarItem(this, name, remoteItem, remoteBranch), predicate, filterableFolder);
					break;
				}
			}
		}

		private void SidebarTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.IsClickedOnScrollbar())
			{
				return;
			}
			MultiselectionTreeViewItem lastClickedItem = SidebarTreeView.LastClickedItem;
			if (lastClickedItem != null && lastClickedItem.ShowExpander)
			{
				lastClickedItem.IsExpanded = !lastClickedItem.IsExpanded;
				return;
			}
			RepositoryUserControl repositoryUserControl = RepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule != null)
			{
				if (SidebarTreeView.SelectedItem is SubmoduleSidebarItem submoduleSidebarItem)
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, new Submodule[1] { submoduleSidebarItem.Submodule });
					e.Handled = true;
				}
				else if (SidebarTreeView.SelectedItem is WorktreeSidebarItem worktreeSidebarItem)
				{
					RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { worktreeSidebarItem.Worktree });
					e.Handled = true;
				}
				else if (SidebarTreeView.SelectedItem is LocalBranchSidebarItem { LocalBranch: var localBranch })
				{
					RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, localBranch);
				}
				else if (SidebarTreeView.SelectedItem is RemoteBranchSidebarItem remoteBranchSidebarItem)
				{
					RemoteBranch branch = remoteBranchSidebarItem.Reference as RemoteBranch;
					RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, branch);
				}
				else if (SidebarTreeView.SelectedItem is TagSidebarItem tagSidebarItem)
				{
					RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.Execute(repositoryUserControl, tagSidebarItem.Reference, tagSidebarItem.Reference.Sha);
				}
				else if (SidebarTreeView.SelectedItem is StashSidebarItem stashSidebarItem)
				{
					RepositoryUserControl.Commands.ShowApplyStashWindow.Execute(repositoryUserControl, stashSidebarItem.Stash);
				}
			}
		}

		private IEnumerable<Control> CreateLocalBranchDropContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, LocalBranch destinationBranch, Branch sourceBranch)
		{
			yield return RepositoryUserControl.Commands.ShowMergeBranchWindow.CreateMenuItem("Merge '" + sourceBranch.Name + "' into " + destinationBranch.Name + "...", delegate
			{
				SidebarTreeView.ContextMenu.IsOpen = false;
				RepositoryUserControl.Commands.ShowMergeBranchWindow.Execute(repositoryUserControl, sourceBranch, destinationBranch);
			});
			LocalBranch sourceLocalBranch = sourceBranch as LocalBranch;
			if (sourceLocalBranch != null)
			{
				yield return RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + sourceLocalBranch.Name + "' on '" + destinationBranch.Name + "...", delegate
				{
					SidebarTreeView.ContextMenu.IsOpen = false;
					RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, sourceLocalBranch, destinationBranch);
				});
				yield return RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Interactively Rebase '" + sourceLocalBranch.Name + "' on '" + destinationBranch.Name + "...", delegate
				{
					SidebarTreeView.ContextMenu.IsOpen = false;
					RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, sourceLocalBranch, destinationBranch);
				});
			}
		}

		private IEnumerable<Control> CreateRemoteBranchDropContextMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RemoteBranch destinationBranch, LocalBranch sourceBranch)
		{
			yield return RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Rebase '" + sourceBranch.Name + "' on '" + destinationBranch.Name + "...", delegate
			{
				SidebarTreeView.ContextMenu.IsOpen = false;
				RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, sourceBranch, destinationBranch);
			});
			yield return RepositoryUserControl.Commands.ShowRebaseBranchWindow.CreateMenuItem("Interactively Rebase '" + sourceBranch.Name + "' on '" + destinationBranch.Name + "...", delegate
			{
				SidebarTreeView.ContextMenu.IsOpen = false;
				RepositoryUserControl.Commands.ShowInteractiveRebaseWindow.Execute(repositoryUserControl, gitModule, sourceBranch, destinationBranch);
			});
		}

		private static void Diff<T>([Null] T[] old, T[] @new, IEqualityComparer<T> comparer, bool forceRefresh, out T[] removed, out T[] added)
		{
			if (forceRefresh)
			{
				removed = old ?? new T[0];
				added = @new;
				return;
			}
			HashSet<T> oldEntries = new HashSet<T>(old ?? new T[0], comparer);
			HashSet<T> newEntries = new HashSet<T>(@new, comparer);
			removed = oldEntries.Where((T x) => !newEntries.Contains(x)).ToArray();
			added = newEntries.Where((T x) => !oldEntries.Contains(x)).ToArray();
		}

		private static IComparer<ForkPlus.Git.Reference> ReferenceComparerFor(ReferenceSortOrder sortOrder)
		{
			return sortOrder switch
			{
				ReferenceSortOrder.Alphabetically => new ReferenceByTypeThenByTitleComparer(), 
				ReferenceSortOrder.AlphabeticallyBackward => new ReferenceByTypeThenByTitleBackwardComparer(), 
				ReferenceSortOrder.RecentlyUsed => new ReferenceByTypeThenByDateComparer(), 
				_ => new ReferenceByTypeThenByTitleComparer(), 
			};
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SearchTabItem searchTabItem = e.AddedItems.FirstItem<SearchTabItem>();
			if (searchTabItem != null)
			{
				searchTabItem?.OnActivated();
			}
			else
			{
				IssuesTabItem issuesTabItem = e.AddedItems.FirstItem<IssuesTabItem>();
				if (issuesTabItem != null)
				{
					issuesTabItem?.OnActivated();
				}
			}
			UpdateVisibleTabs(_repositoryData);
		}

		private void RepositoryParentNameTextBlock_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			if (RepositoryUserControl.ParentRepositoryName != null)
			{
				string text = RepositoryUserControl.GitModule?.ParentRepoPath;
				if (text != null)
				{
					Application.Current.MainWindow.Activate();
					Application.Current?.TabManager()?.OpenRepository(text);
				}
			}
		}

	}
}
