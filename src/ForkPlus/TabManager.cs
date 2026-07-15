using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;

namespace ForkPlus
{
	public class TabManager
	{
		private readonly ClosableTabControl _tabControl;

		private bool _tabRestoreInprogress;

		[Null]
		public ClosableTabItem ActiveTab => _tabControl.SelectedTab;

		[Null]
		public RepositoryUserControl ActiveRepositoryUserControl => ActiveTab?.RepositoryUserControl ?? ActiveTab?.GitMmUserControl?.ActiveRepositoryUserControl;

		[Null]
		public GitMmUserControl ActiveGitMmUserControl => ActiveTab?.GitMmUserControl;

		public RepositoryUserControl[] RepositoryUserControls
		{
			get
			{
				List<RepositoryUserControl> list = new List<RepositoryUserControl>(_tabControl.Items.Count);
				foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
				{
					if (item.Mode == TabItemMode.Repository)
					{
						list.Add(item.RepositoryUserControl);
					}
				}
				return list.ToArray();
			}
		}

		[Null]
		public RepositoryManagerUserControl ActiveRepositoryManager
		{
			get
			{
				foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
				{
					if (item.Mode == TabItemMode.RepositoryManager)
					{
						return item.RepositoryManagerUserControl;
					}
				}
				return null;
			}
		}

		public TabManager(ClosableTabControl tabControl)
		{
			_tabControl = tabControl;
			tabControl.AddButtonClicked = (EventHandler)Delegate.Combine(tabControl.AddButtonClicked, new EventHandler(TabControl_AddClicked));
			tabControl.TabItemRemoved = (EventHandler)Delegate.Combine(tabControl.TabItemRemoved, new EventHandler(TabControl_ItemRemoved));
			tabControl.SelectedTabItemChanged = (EventHandler<EventArgs<ClosableTabItem>>)Delegate.Combine(tabControl.SelectedTabItemChanged, new EventHandler<EventArgs<ClosableTabItem>>(TabControl_SelectedTabItemChanged));
		}

		public void SaveSession()
		{
			string activeRepository = null;
			List<string> list = new List<string>(_tabControl.Items.Count);
			foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
			{
				GitModule gitModule = item.RepositoryUserControl?.GitModule;
				if (gitModule != null)
				{
					if (item == _tabControl.SelectedTab)
					{
						activeRepository = PathHelper.Normalize(gitModule.Path);
					}
					list.Add(PathHelper.Normalize(gitModule.Path));
				}
				else if (item.GitMmUserControl != null)
				{
					string path = PathHelper.Normalize(item.GitMmUserControl.WorkspacePath);
					if (item == _tabControl.SelectedTab)
					{
						activeRepository = path;
					}
					item.GitMmUserControl.Save();
					list.Add(path);
				}
			}
			ForkPlusSettings.Default.Workspaces.ActiveWorkspace.Repositories = list.ToArray();
			ForkPlusSettings.Default.Workspaces.ActiveWorkspace.ActiveRepository = activeRepository;
		}

		public void RestoreSession()
		{
			_tabRestoreInprogress = true;
			_tabControl.Items.Clear();
			Workspace activeWorkspace = ForkPlusSettings.Default.Workspaces.ActiveWorkspace;
			Log.Info($"Restore workspance '{activeWorkspace.Name}' with {activeWorkspace.Repositories.Length} tabs");
			ClosableTabItem closableTabItem = null;
			string[] repositories = activeWorkspace.Repositories;
			foreach (string text in repositories)
			{
				ClosableTabItem closableTabItem2 = null;
				if (GitMmUserControl.IsGitMmWorkspace(text))
				{
					closableTabItem2 = new ClosableTabItem();
					closableTabItem2.ActivateGitMmMode(text);
					_tabControl.AddTab(closableTabItem2);
				}
				else
				{
					GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(text);
					if (gitCommandResult.Succeeded)
					{
						closableTabItem2 = new ClosableTabItem();
						closableTabItem2.ActivateRepositoryViewMode(gitCommandResult.Result);
						_tabControl.AddTab(closableTabItem2);
					}
				}
				if (closableTabItem2 != null && text == activeWorkspace.ActiveRepository)
				{
					closableTabItem = closableTabItem2;
				}
			}
			RefreshTabTitles();
			closableTabItem = closableTabItem ?? _tabControl.Items.FirstItem<ClosableTabItem>();
			if (closableTabItem != null)
			{
				_tabControl.SelectTab(closableTabItem);
				NotificationCenter.Current.RaiseActiveTabChanged(this, closableTabItem);
			}
			_tabRestoreInprogress = false;
			if (_tabControl.Items.Count == 0)
			{
				ClosableTabItem closableTabItem3 = new ClosableTabItem();
				closableTabItem3.ActivateRepositoryManagerMode();
				_tabControl.AddTab(closableTabItem3);
				_tabControl.SelectTab(closableTabItem3);
			}
			_tabControl.SelectedTab?.Refresh();
		}

		public void RefreshTabTitles()
		{
			foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
			{
				item.RefreshTitle();
			}
		}

		public bool OpenRepository(string path, GitModule nextTo = null)
		{
			if (GitMmUserControl.IsGitMmWorkspace(path))
			{
				ClosableTabItem gitMmTab = FindTab(path) ?? CreateNewGitMmTab(path);
				_tabControl.SelectTab(gitMmTab);
				RepositoryManager.Instance.AddOrUpdateLastOpened(path);
				RepositoryManager.Instance.Save();
				NotificationCenter.Current.RaiseRepositoryManagerRepositoriesUpdated(this);
				ClosableTabItem repositoryManagerTabItem = FindRepositoryManagerTabItem();
				if (repositoryManagerTabItem != null)
				{
					_tabControl.RemoveTab(repositoryManagerTabItem);
				}
				RefreshTabTitles();
				return true;
			}
			GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(path);
			if (!gitCommandResult.Succeeded)
			{
				return false;
			}
			GitModule result = gitCommandResult.Result;
			ClosableTabItem itemToSelect = FindTab(result) ?? CreateNewTab(TabItemMode.Repository, result, nextTo);
			_tabControl.SelectTab(itemToSelect);
			ClosableTabItem closableTabItem = FindRepositoryManagerTabItem();
			if (closableTabItem != null)
			{
				_tabControl.RemoveTab(closableTabItem);
			}
			if (result.Type != ModuleType.Submodule && result.Type != ModuleType.Worktree)
			{
				RepositoryManager.Instance.AddOrUpdateLastOpened(result);
			}
			RefreshTabTitles();
			return true;
		}

		/// <summary>
		/// 查找指定子仓路径所属的 git mm 工作区：先在已打开的 git mm 页签中查找
		/// （ContainsSubrepoPath），未命中再向上查找 .repo/.mm 工作区根
		/// （即便 git mm 页签尚未打开也能识别）。用于在子仓页签右键菜单提供
		/// “打开 git mm 仓”快捷入口。返回所属工作区路径；未找到返回 null。
		/// </summary>
		[Null]
		public string FindGitMmWorkspacePathForSubrepo(string subrepoPath)
		{
			if (string.IsNullOrWhiteSpace(subrepoPath))
			{
				return null;
			}
			foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
			{
				GitMmUserControl gitMmUserControl = item.GitMmUserControl;
				if (gitMmUserControl != null && gitMmUserControl.ContainsSubrepoPath(subrepoPath))
				{
					return gitMmUserControl.WorkspacePath;
				}
			}
			// 未在已打开的 git mm 页签中找到时，向上查找 .repo/.mm 工作区根
			return GitMmUserControl.FindAncestorGitMmWorkspace(subrepoPath);
		}

		public void OpenRepositories(string[] repositoryPaths)
		{
			ClosableTabItem closableTabItem = null;
			foreach (string path in repositoryPaths)
			{
				if (GitMmUserControl.IsGitMmWorkspace(path))
				{
					closableTabItem = FindTab(path) ?? CreateNewGitMmTab(path);
					RepositoryManager.Instance.AddOrUpdateLastOpened(path);
				}
				else
				{
					GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(path);
					if (gitCommandResult.Succeeded)
					{
						GitModule result = gitCommandResult.Result;
						closableTabItem = FindTab(result) ?? CreateNewTab(TabItemMode.Repository, result);
					}
				}
			}
			RepositoryManager.Instance.Save();
			NotificationCenter.Current.RaiseRepositoryManagerRepositoriesUpdated(this);
			if (closableTabItem != null)
			{
				ClosableTabItem closableTabItem2 = FindRepositoryManagerTabItem();
				if (closableTabItem2 != null)
				{
					_tabControl.RemoveTab(closableTabItem2);
				}
				closableTabItem.IsSelected = true;
			}
			RefreshTabTitles();
		}

		public void SelectPreviousTab()
		{
			_tabControl.SelectPreviousTab();
		}

		public void SelectNextTab()
		{
			_tabControl.SelectNextTab();
		}

		public void CloseActiveTab()
		{
			_tabControl.SelectedTab?.Close();
		}

		public void CloseTab(string path)
		{
			FindTab(path)?.Close();
		}

		public void NewTab()
		{
			ClosableTabItem closableTabItem = FindRepositoryManagerTabItem();
			if (closableTabItem != null)
			{
				_tabControl.SelectTab(closableTabItem);
				return;
			}
			_tabControl.SelectTab(CreateNewTab(TabItemMode.RepositoryManager, null));
			RefreshTabTitles();
		}

		private void TabControl_SelectedTabItemChanged(object sender, EventArgs<ClosableTabItem> e)
		{
			if (!_tabRestoreInprogress)
			{
				ClosableTabItem value = e.Value;
				if (value != null)
				{
					NotificationCenter.Current.RaiseActiveTabChanged(this, value);
					SaveSession();
					ForkPlusSettings.Default.Save();
					Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
					{
						if (_tabControl.SelectedTab == value)
						{
							value.Refresh();
						}
					}));
				}
			}
		}

		private void TabControl_AddClicked(object sender, EventArgs e)
		{
			NewTab();
		}

		[Null]
		private ClosableTabItem FindRepositoryManagerTabItem()
		{
			foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
			{
				if (item.Mode == TabItemMode.RepositoryManager)
				{
					return item;
				}
			}
			return null;
		}

		private void TabControl_ItemRemoved(object sender, EventArgs e)
		{
			RefreshTabTitles();
			SaveSession();
			ForkPlusSettings.Default.Save();
		}

		private ClosableTabItem CreateNewTab(TabItemMode tabItemMode, GitModule gitModule, [Null] GitModule nextTo = null)
		{
			ClosableTabItem closableTabItem = new ClosableTabItem();
			if (tabItemMode == TabItemMode.Repository)
			{
				closableTabItem.ActivateRepositoryViewMode(gitModule);
			}
			else
			{
				closableTabItem.ActivateRepositoryManagerMode();
			}
			if (nextTo != null)
			{
				for (int i = 0; i < _tabControl.Items.Count; i++)
				{
					if ((_tabControl.Items[i] as ClosableTabItem)?.RepositoryUserControl?.GitModule == nextTo)
					{
						_tabControl.InsertAt(closableTabItem, i + 1);
						return closableTabItem;
					}
				}
			}
			_tabControl.AddTab(closableTabItem);
			return closableTabItem;
		}

		private ClosableTabItem CreateNewGitMmTab(string path)
		{
			ClosableTabItem closableTabItem = new ClosableTabItem();
			closableTabItem.ActivateGitMmMode(path);
			_tabControl.AddTab(closableTabItem);
			return closableTabItem;
		}

		[Null]
		private ClosableTabItem FindTab(GitModule gitModule)
		{
			return FindTab(gitModule.Path);
		}

		[Null]
		private ClosableTabItem FindTab(string path)
		{
			foreach (ClosableTabItem item in (IEnumerable)_tabControl.Items)
			{
				GitModule gitModule = item.RepositoryUserControl?.GitModule;
				if (gitModule != null && string.Compare(gitModule.Path, path, ignoreCase: true) == 0)
				{
					return item;
				}
				GitMmUserControl gitMmUserControl = item.GitMmUserControl;
				if (gitMmUserControl != null && string.Compare(gitMmUserControl.WorkspacePath, path, ignoreCase: true) == 0)
				{
					return item;
				}
			}
			return null;
		}
	}
}
