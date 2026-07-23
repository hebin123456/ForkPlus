using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Dialogs;
using ForkPlus.Undo;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public static readonly RepositoryUserControlCommands Commands = new RepositoryUserControlCommands();

		// 阶段 3 里程碑 3.15：纯业务状态/逻辑由 VM 承载（零 WPF），本类保留公共成员签名作为薄转发层，
		// 维持 Commands 层与既有调用点契约不变（repositoryUserControl.GitModule 等仍可直访）。
		private readonly RepositoryUserControlViewModel _viewModel = new RepositoryUserControlViewModel();

		private bool _layoutInitialized;

		public TempFileManager TempFileManager => _viewModel.TempFileManager;

		public JobQueue JobQueue => _viewModel.JobQueue;

		/// <summary>本仓库的 Undo/Redo 历史栈。v3.0.0 新增。</summary>
		public UndoRedoStack UndoRedoStack => _viewModel.UndoRedoStack;

		/// <summary>Undo/Redo 状态变化时触发，UI 工具栏订阅以刷新按钮可用性。</summary>
		public event EventHandler UndoRedoStateChanged;

		// RefreshRepositoryCommand 是 internal 类（ForkPlus.UI.Commands），不可被 public 属性暴露（CS0053）。
		// 保留为 private 字段直接持有，不通过 VM 转发（命令对象本身无状态需 VM 化）。
		private readonly RefreshRepositoryCommand RefreshRepositoryCommand = new RefreshRepositoryCommand();

		public RepositoryData RepositoryData
		{
			get { return _viewModel.RepositoryData; }
			private set { _viewModel.RepositoryData = value; }
		}

		public RepositoryStatus RepositoryStatus
		{
			get { return _viewModel.RepositoryStatus; }
			private set { _viewModel.RepositoryStatus = value; }
		}

		public GitModule GitModule
		{
			get { return _viewModel.GitModule; }
			private set { _viewModel.GitModule = value; }
		}

		public CommitGraphCache CommitGraphCache
		{
			get { return _viewModel.CommitGraphCache; }
			private set { _viewModel.CommitGraphCache = value; }
		}

		public string RepositoryName
		{
			get { return _viewModel.RepositoryName; }
			private set { _viewModel.RepositoryName = value; }
		}

		public string ParentRepositoryName
		{
			get { return _viewModel.ParentRepositoryName; }
			private set { _viewModel.ParentRepositoryName = value; }
		}

		public string RepositoryTitle
		{
			get { return _viewModel.RepositoryTitle; }
			private set { _viewModel.RepositoryTitle = value; }
		}

		public bool IsDirty
		{
			get { return _viewModel.IsDirty; }
			set { _viewModel.IsDirty = value; }
		}

		public RepositoryColor RepositoryColor
		{
			get { return _viewModel.RepositoryColor; }
			private set { _viewModel.RepositoryColor = value; }
		}

		public SubDomain InvalidatedSubdomains => _viewModel.InvalidatedSubdomains;

		public RepositoryViewMode ViewMode
		{
			get { return _viewModel.ViewMode; }
			private set
			{
				// VM 的 ViewMode setter 仅更新状态 + 触发 ViewModeChanged 事件；
				// UI 副作用（Content/Sidebar.SetRepositoryViewMode + NotificationBar.Refresh）由本 View 订阅事件执行。
				// 这里直接调用 VM 的 ViewMode setter，事件订阅在构造函数中完成。
				_viewModel.ViewMode = value;
			}
		}

		public bool ShowReflogInRevisionList
		{
			get { return _viewModel.ShowReflogInRevisionList; }
			set { _viewModel.ShowReflogInRevisionList = value; }
		}

		public Job ActiveFetchRevisionsUntilShaJob
		{
			get { return _viewModel.ActiveFetchRevisionsUntilShaJob; }
			set { _viewModel.ActiveFetchRevisionsUntilShaJob = value; }
		}

		[Null]
		public Job ActiveFetchRevisionsNextPageJob
		{
			get { return _viewModel.ActiveFetchRevisionsNextPageJob; }
			set { _viewModel.ActiveFetchRevisionsNextPageJob = value; }
		}

		public SidebarUserControl Sidebar { get; private set; }

		public new RepositoryContentUserControl Content { get; private set; }

		public RepositoryUserControl()
		{
			InitializeComponent();
			WeakEventManager<NotificationCenter, EventArgs<string>>.AddHandler(NotificationCenter.Current, "RepositoryNameChanged", RepositoryNameChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryManager.Repository>>.AddHandler(NotificationCenter.Current, "RepositoryColorChanged", RepositoryColorChanged);
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current, "UpdateRepoStatusAutomaticallyChanged", UpdateRepoStatusAutomaticallyChanged);
			SidebarGridSplitter.DragCompleted += delegate
			{
				SaveSidebarColumnWidth();
			};
			// 订阅 VM 事件：ViewMode 变更 → 驱动子控件 UI 刷新；UndoRedo 状态变更 → 转发本类事件供工具栏订阅。
			_viewModel.ViewModeChanged += OnViewModeChanged;
			_viewModel.UndoRedoStateChanged += (s, e) => UndoRedoStateChanged?.Invoke(this, e);
		}

		/// <summary>ViewModeChanged 事件处理器：执行原 ViewMode setter 的 UI 副作用。</summary>
		private void OnViewModeChanged(RepositoryViewMode viewMode)
		{
			Content.SetRepositoryViewMode(viewMode);
			Sidebar.SetRepositoryViewMode(viewMode);
			NotificationBar.Refresh();
		}

		protected override void OnDrop(DragEventArgs e)
		{
			base.OnDrop(e);
			if (e.Data.GetData(DataFormats.FileDrop) is string[] source)
			{
				string text = source.FirstItem();
				if (text != null && text.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase))
				{
					Commands.ApplyPatch.Execute(this, text);
					e.Handled = true;
				}
			}
		}

		[Null]
		private string FindParentRepositoryName(GitModule gitModule)
		{
			return RepositoryUserControlViewModel.FindParentRepositoryName(gitModule);
		}

		public void RefreshRepositoryTitle()
		{
			GitModule gitModule = GitModule;
			if (gitModule == null)
			{
				RepositoryTitle = "";
				return;
			}
			string text = FindParentRepositoryName(gitModule);
			if (text != null)
			{
				string text2 = text + ": " + gitModule.RepositoryName;
				if (gitModule.Type == ModuleType.Worktree)
				{
					string text3 = FindSiblingWorktreeWithSameName(gitModule);
					if (text3 != null)
					{
						string item = PathHelper.FindFirstDifferentComponent(gitModule.Path, text3).Item1;
						if (item != null)
						{
							RepositoryTitle = text2 + " (" + item + ")";
							return;
						}
					}
				}
				RepositoryTitle = text2;
				return;
			}
			string repositoryPath = gitModule.Path;
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == repositoryPath);
			if (repository.HasValue)
			{
				RepositoryManager.Repository repo = repository.GetValueOrDefault();
				string alias = repo.Alias;
				if (alias != null)
				{
					RepositoryTitle = alias;
					return;
				}
				string name = repo.Name();
				repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path != repo.Path && x.Name() == name);
				if (repository.HasValue)
				{
					RepositoryManager.Repository valueOrDefault = repository.GetValueOrDefault();
					string item2 = PathHelper.FindFirstDifferentComponent(repo.Path, valueOrDefault.Path).Item1;
					if (item2 != null)
					{
						RepositoryTitle = RepositoryName + " (" + item2 + ")";
						return;
					}
				}
			}
			RepositoryTitle = gitModule.RepositoryName;
		}

		public void UpdateRepositoryData(RepositoryData repositoryData, RevisionContextSearch? contextSearch, [Null] RevisionSelector select)
		{
			RepositoryData repositoryData2 = RepositoryData;
			Sidebar.UpdateRepositoryData(repositoryData);
			RepositoryData = repositoryData;
			Content.RefreshRevisionItems(repositoryData2, repositoryData, contextSearch, select);
			RefreshToolbarBadges();
			NotificationCenter.Current.RaiseRepositoryDataUpdated(this, this, repositoryData2, RepositoryData);
		}

		public void UpdateRepositoryStatus(RepositoryStatus repositoryStatus)
		{
			if (RepositoryStateChanged(RepositoryStatus, repositoryStatus))
			{
				Content.CommitUserControl.EraseSavedCommitMessage();
				Content.CommitUserControl.LoadCommitMessage();
			}
			RepositoryStatus = repositoryStatus;
			RepositoryStatus displayRepositoryStatus = NormalizeRepositoryStatusForDisplay(repositoryStatus);
			Sidebar.UpdateRepositoryStatus(displayRepositoryStatus);
			UpdateIsDirtyState(((displayRepositoryStatus != null && displayRepositoryStatus.ChangedFiles.Length != 0) ? 1 : 0) > (false ? 1 : 0));
			NotificationCenter.Current.RaiseRepositoryStatusUpdated(this, this);
			NotificationBar.Refresh();
		}

		[Null]
		public RepositoryStatus NormalizeRepositoryStatusForDisplay([Null] RepositoryStatus repositoryStatus)
		{
			if (repositoryStatus == null)
			{
				return null;
			}
			ChangedFile[] changedFiles = NormalizeChangedFilesForDisplay(repositoryStatus.ChangedFiles);
			if (changedFiles.Length == repositoryStatus.ChangedFiles.Length)
			{
				return repositoryStatus;
			}
			return new RepositoryStatus(repositoryStatus.RepositoryState, CountDistinctChangedFiles(changedFiles), changedFiles);
		}

		public ChangedFile[] NormalizeChangedFilesForDisplay(ChangedFile[] changedFiles)
		{
			GitMmUserControl gitMmUserControl = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
			// 仅当当前 RepositoryUserControl 是 git mm 工作区根（主仓）时才过滤子仓入口变更。
			// 子仓 tab 内嵌的 RepositoryUserControl 自身展示的是子仓内部文件，
			// 若此时也走过滤，ContainsSubrepoPath 的前缀匹配会把子仓自己的变更文件全部误判为
			// "git mm 管理的子仓变更"而过滤掉，导致子仓视图本地变更/未暂存区为空。
			// "作为独立仓库打开"走 TabManager.OpenRepository，ActiveGitMmUserControl 为 null 不触发过滤，
			// 这正是两条路径表现不同的根因。
			if (gitMmUserControl != null && GitModule != null
				&& !GitMmUserControl.IsSamePath(GitModule.Path, gitMmUserControl.WorkspacePath))
			{
				return changedFiles ?? new ChangedFile[0];
			}
			return ChangedFilesDisplayNormalizer.NormalizeForDisplay(GitModule, changedFiles, gitMmUserControl);
		}

		private static int CountDistinctChangedFiles(ChangedFile[] changedFiles)
		{
			return RepositoryUserControlViewModel.CountDistinctChangedFiles(changedFiles);
		}

		public void RefreshToolbarBadges()
		{
			if (MainWindow.Instance?.TabManager.ActiveRepositoryUserControl == this)
			{
				LocalBranch localBranch = RepositoryData?.References.ActiveBranch;
				if (localBranch == null)
				{
					MainWindow.Instance.Toolbar.RefreshPullPushBadges(null);
					return;
				}
				UpstreamStatus? upstreamStatus = RepositoryData.UpstreamStatus.GetUpstreamStatus(localBranch);
				MainWindow.Instance.Toolbar.RefreshPullPushBadges(upstreamStatus);
			}
		}

		public void UpdateIsDirtyState(bool newIsDirtyState)
		{
			IsDirty = newIsDirtyState;
			NotificationCenter.Current.RaiseRepositoryUserControlIsDirtyChanged(this, this);
		}

		public void OpenRepository(GitModule gitModule)
		{
			GitModule = gitModule;
			CommitGraphCache = new CommitGraphCache(gitModule);
			if (gitModule.Type == ModuleType.Submodule)
			{
				RepositoryName = gitModule.RepositoryName;
				string parentRepoPath = gitModule.ParentRepoPath;
				if (parentRepoPath != null)
				{
					ParentRepositoryName = RepositoryManager.Instance.FindRepositoryName(parentRepoPath) ?? gitModule.ParentRepositoryName;
				}
				else
				{
					ParentRepositoryName = gitModule.ParentRepositoryName;
				}
			}
			else if (gitModule.Type == ModuleType.Worktree)
			{
				RepositoryName = gitModule.RepositoryName;
				string commonGitDir = gitModule.CommonGitDir;
				if (commonGitDir != null)
				{
					if (Path.GetFileName(commonGitDir) != ".git")
					{
						string directoryName = Path.GetDirectoryName(commonGitDir);
						ParentRepositoryName = RepositoryManager.Instance.FindRepositoryName(directoryName) ?? Path.GetFileName(directoryName);
					}
					else
					{
						string directoryName2 = Path.GetDirectoryName(commonGitDir);
						ParentRepositoryName = RepositoryManager.Instance.FindRepositoryName(directoryName2) ?? Path.GetFileName(directoryName2);
					}
				}
				else
				{
					ParentRepositoryName = null;
				}
			}
			else
			{
				RepositoryManager.Repository r = RepositoryManager.Instance.AddOrUpdateLastOpened(gitModule);
				RepositoryName = r.Name();
				RepositoryColor = r.Color;
				ParentRepositoryName = null;
			}
			Sidebar?.RefreshTitle();
		}

		public void ApplyLocalization()
		{
			Sidebar?.ApplyLocalization();
			Sidebar?.RefreshTitle();
			Content?.ApplyLocalization();
		}

		public void SidebarRevealActiveBranch()
		{
			Sidebar.RevealActiveBranch();
		}

		public void SidebarActivateRepositoryTab()
		{
			Sidebar.ActivateRepositoryTab();
		}

		public void SidebarActivateSearchTab()
		{
			Sidebar.ActivateSearchTab();
		}

		public void ActivateCommitView(bool focusCommitSubject = false)
		{
			bool isVisible = Content.CommitView.IsVisible;
			ViewMode = RepositoryViewMode.CommitViewMode;
			if (isVisible || focusCommitSubject)
			{
				Content.CommitUserControl.FocusCommitMessageField();
			}
			else
			{
				Content.CommitUserControl.StageFileUserControl.FocusActiveListView();
			}
		}

		public void ActivateRevisionView()
		{
			ViewMode = RepositoryViewMode.RevisionViewMode;
		}

		public bool SelectAndScrollIntoView(RevisionSelector selector)
		{
			return Content.RevisionListViewUserControl.Select(selector, (NoUIAutomationListView.SelectOptions)3);
		}

		public void SelectRevision(Sha sha, [Null] string filePath = null)
		{
			SelectRevisions(new Sha[1] { sha }, (NoUIAutomationListView.SelectOptions)3, filePath);
		}

		public void SelectRevisions(IReadOnlyList<Sha> shas, NoUIAutomationListView.SelectOptions selectOptions = (NoUIAutomationListView.SelectOptions)3, [Null] string filePath = null)
		{
			Content.SelectRevisions(shas, selectOptions, filePath);
		}

		public void SelectRevisions(IReadOnlyList<Sha> shas, bool fetchIfNeeded)
		{
			GitModule gitModule = GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			CancelActiveFetchRevisionsJobs();
			if (shas.Count == 0)
			{
				return;
			}
			var (foundRows, hashSet) = Content.RevisionListViewUserControl.RevisionsDataSource.FindRowsBySha(shas);
			if (hashSet.Count == 0 || !repositoryData.RevisionStorage.HasMore || !fetchIfNeeded)
			{
				NavigateToRows(foundRows);
				return;
			}
			StashRevision[] items = repositoryData.Stashes.Items;
			foreach (StashRevision stashRevision in items)
			{
				hashSet.Remove(stashRevision.Sha);
			}
			HandleEnumerator enumerator = repositoryData.RevisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				hashSet.Remove(repositoryData.RevisionStorage.GetSha(current));
			}
			if (hashSet.Count == 0)
			{
				NavigateToRows(foundRows);
				return;
			}
			Sha[] remainingShas2 = hashSet.ToArray();
			RevisionContextSearch? oldContextSearch = Content.RevisionListViewUserControl.RevisionsDataSource.ContextSearch;
			_activeFetchRevisionsUntilShaJob = JobQueue.Add(PreferencesLocalization.Current("fetch until shas"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					GitCommandResult<RevisionStorage> response = new GetRevisionStorageGitCommand().FetchUntil(gitModule, repositoryData.RevisionStorage, repositoryData.References.ReferenceStorage, repositoryData.SortOrder == RevisionSortOrder.Topo, repositoryData.Reflog, remainingShas2, commitGraphCache, monitor);
					if (!monitor.IsCanceled)
					{
						if (!response.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								if (!monitor.IsCanceled)
								{
									_activeFetchRevisionsUntilShaJob = null;
									new ErrorWindow(this, response.Error).ShowDialog();
								}
							});
						}
						else if (!monitor.IsCanceled)
						{
							RevisionStorage newRevisionStorage = response.Result;
							GitCommandResult<RevisionContextSearch?> newContextSearchResponse = ExpandContextSearch(gitModule, oldContextSearch, repositoryData.RevisionStorage, newRevisionStorage, repositoryData.References, monitor);
							if (!monitor.IsCanceled)
							{
								if (!newContextSearchResponse.Succeeded)
								{
									base.Dispatcher.Async(delegate
									{
										if (!monitor.IsCanceled)
										{
											_activeFetchRevisionsUntilShaJob = null;
											new ErrorWindow(this, newContextSearchResponse.Error).ShowDialog();
										}
									});
								}
								else
								{
									RevisionContextSearch? newContextSearch = newContextSearchResponse.Result;
									base.Dispatcher.Async(delegate
									{
										if (!monitor.IsCanceled)
										{
											if (repositoryData != RepositoryData)
											{
												_activeFetchRevisionsUntilShaJob = null;
											}
											else
											{
												RepositoryData repositoryData2 = repositoryData.With(newRevisionStorage);
												UpdateRepositoryData(repositoryData2, newContextSearch, null);
												IReadOnlyList<int> item = Content.RevisionListViewUserControl.RevisionsDataSource.FindRowsBySha(remainingShas2).Item1;
												List<int> list = new List<int>(foundRows);
												list.AddRange(item);
												NavigateToRows(list);
												_activeFetchRevisionsUntilShaJob = null;
											}
										}
									});
								}
							}
						}
					}
				}
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		private void NavigateToRows(IReadOnlyList<int> rows)
		{
			if (rows.Count != 0)
			{
				ActivateRevisionView();
				Content.RevisionListViewUserControl.Select(rows);
			}
		}

		public void FetchNextRevisionPage()
		{
			GitModule gitModule = GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = CommitGraphCache;
			if (commitGraphCache == null || !repositoryData.RevisionStorage.HasMore || _activeFetchRevisionsNextPageJob != null)
			{
				return;
			}
			RevisionContextSearch? oldContextSearch = Content.RevisionListViewUserControl.RevisionsDataSource.ContextSearch;
			_activeFetchRevisionsNextPageJob = JobQueue.Add(PreferencesLocalization.Current("fetch next page"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					GitCommandResult<RevisionStorage> response = new GetRevisionStorageGitCommand().FetchNextPage(gitModule, repositoryData.RevisionStorage, repositoryData.References.ReferenceStorage, repositoryData.SortOrder == RevisionSortOrder.Topo, repositoryData.Reflog, commitGraphCache, monitor);
					if (!monitor.IsCanceled)
					{
						if (!response.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								if (!monitor.IsCanceled)
								{
									_activeFetchRevisionsNextPageJob = null;
									new ErrorWindow(this, response.Error).ShowDialog();
								}
							});
						}
						else
						{
							RevisionStorage newRevisionStorage = response.Result;
							if (!monitor.IsCanceled)
							{
								GitCommandResult<RevisionContextSearch?> newContextSearchResponse = ExpandContextSearch(gitModule, oldContextSearch, repositoryData.RevisionStorage, newRevisionStorage, repositoryData.References, monitor);
								if (!monitor.IsCanceled)
								{
									if (!newContextSearchResponse.Succeeded)
									{
										base.Dispatcher.Async(delegate
										{
											if (!monitor.IsCanceled)
											{
												_activeFetchRevisionsNextPageJob = null;
												new ErrorWindow(this, newContextSearchResponse.Error).ShowDialog();
											}
										});
									}
									else
									{
										RevisionContextSearch? newContextSearch = newContextSearchResponse.Result;
										base.Dispatcher.Async(delegate
										{
											if (!monitor.IsCanceled)
											{
												if (repositoryData != RepositoryData)
												{
													_activeFetchRevisionsNextPageJob = null;
												}
												else if (Content.RevisionListViewUserControl.RevisionsDataSource.ContextSearch?.SearchString != oldContextSearch?.SearchString)
												{
													_activeFetchRevisionsNextPageJob = null;
												}
												else
												{
													RepositoryData repositoryData2 = repositoryData.With(newRevisionStorage);
													UpdateRepositoryData(repositoryData2, newContextSearch, null);
													_activeFetchRevisionsNextPageJob = null;
												}
											}
										});
									}
								}
							}
						}
					}
				}
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		public void FetchUntilFindContextSearchMatch(int selectedRow)
		{
			GitModule gitModule = GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = CommitGraphCache;
			if (commitGraphCache == null || !repositoryData.RevisionStorage.HasMore || _activeFetchRevisionsNextPageJob != null)
			{
				return;
			}
			RevisionContextSearch? contextSearch = Content.RevisionListViewUserControl.RevisionsDataSource.ContextSearch;
			if (!contextSearch.HasValue)
			{
				return;
			}
			_activeFetchRevisionsNextPageJob = JobQueue.Add(PreferencesLocalization.Current("fetch until search match"), delegate(JobMonitor monitor)
			{
				RevisionStorage revisionStorage = repositoryData.RevisionStorage;
				RevisionContextSearch? newContextSearch = null;
				RevisionStorage result;
				do
				{
					if (monitor.IsCanceled)
					{
						return;
					}
					GitCommandResult<RevisionStorage> response = new GetRevisionStorageGitCommand().FetchNextPage(gitModule, revisionStorage, repositoryData.References.ReferenceStorage, repositoryData.SortOrder == RevisionSortOrder.Topo, repositoryData.Reflog, commitGraphCache, monitor);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								_activeFetchRevisionsNextPageJob = null;
								new ErrorWindow(this, response.Error).ShowDialog();
							}
						});
						return;
					}
					result = response.Result;
					GitCommandResult<RevisionContextSearch?> newContextSearchResponse = ExpandContextSearch(gitModule, contextSearch, revisionStorage, result, repositoryData.References, monitor);
					if (!newContextSearchResponse.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								_activeFetchRevisionsNextPageJob = null;
								new ErrorWindow(this, newContextSearchResponse.Error).ShowDialog();
							}
						});
						return;
					}
					newContextSearch = newContextSearchResponse.Result;
					revisionStorage = result;
				}
				while (result.HasMore && newContextSearch?.MatchCount == contextSearch.Value.MatchCount);
				if (!monitor.IsCanceled)
				{
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							if (repositoryData != RepositoryData)
							{
								_activeFetchRevisionsNextPageJob = null;
							}
							else
							{
								RepositoryData repositoryData2 = repositoryData.With(revisionStorage);
								UpdateRepositoryData(repositoryData2, newContextSearch, null);
								int? num = Content.RevisionListViewUserControl.RevisionsDataSource.NextContextSearchMatch(selectedRow, initialJump: false);
								if (num.HasValue)
								{
									int valueOrDefault = num.GetValueOrDefault();
									Content.RevisionListViewUserControl.Select(new int[1] { valueOrDefault });
								}
								_activeFetchRevisionsNextPageJob = null;
							}
						}
					});
				}
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		public void CancelActiveFetchRevisionsJobs()
		{
			_viewModel.CancelActiveFetchRevisionsJobs();
		}

		private static GitCommandResult<RevisionContextSearch?> ExpandContextSearch(GitModule gitModule, RevisionContextSearch? oldContextSearch, RevisionStorage oldRevisionStorage, RevisionStorage newRevisionStorage, RepositoryReferences references, JobMonitor monitor)
		{
			if (!oldContextSearch.HasValue)
			{
				return GitCommandResult<RevisionContextSearch?>.Success(null);
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult<RevisionContextSearch?>.Failure(new GitCommandError.Cancelled());
			}
			string searchString = oldContextSearch.Value.SearchString;
			HashSet<Sha> hashSet = new HashSet<Sha>(oldContextSearch.Value.Matches);
			List<Sha> list = new List<Sha>(newRevisionStorage.Count - oldRevisionStorage.Count);
			int num = 0;
			HandleEnumerator enumerator = newRevisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				if (num >= oldRevisionStorage.Count)
				{
					list.Add(newRevisionStorage.GetSha(current));
				}
				num++;
			}
			Sha[] refMatches = references.Items.Filter((ForkPlus.Git.Reference x) => x.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) != -1).Map((ForkPlus.Git.Reference x) => x.Sha);
			GitCommandResult<Sha[]> gitCommandResult = new RevisionContextSearchGitCommand().Execute(gitModule, searchString, list.ToArray(), refMatches, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RevisionContextSearch?>.Failure(gitCommandResult.Error);
			}
			Sha[] result = gitCommandResult.Result;
			foreach (Sha item in result)
			{
				hashSet.Add(item);
			}
			return GitCommandResult<RevisionContextSearch?>.Success(new RevisionContextSearch(searchString, hashSet));
		}

		public void FocusSelectedRevision()
		{
			Content.RevisionListViewUserControl.FocusSelectedItem();
		}

		public void EraseSavedCommitMessage()
		{
			Content.CommitUserControl.EraseSavedCommitMessage();
			Content.CommitUserControl.FullCommitMessage = "";
		}

		public void CollapseAllMerges()
		{
			Content.RevisionListViewUserControl.CollapseAll();
		}

		public void ExpandAllMerges()
		{
			Content.RevisionListViewUserControl.ExpandAll();
		}

		public void ShowRevisionDetails(RevisionDiffTarget target, [Null] string fileToSelect = null)
		{
			Content.RevisionDetails.ShowRevisionDetails(target, fileToSelect);
		}

		private void EnsureLayoutInitialized()
		{
			if (!_layoutInitialized)
			{
				Content = new RepositoryContentUserControl();
				Sidebar = new SidebarUserControl();
				VisualTreeAttachmentHelper.TrySetContent(RepositoryContentContainer, Content, GetType().Name + ".RepositoryContentContainer");
				VisualTreeAttachmentHelper.TrySetContent(RepositorySidebarContainer, Sidebar, GetType().Name + ".RepositorySidebarContainer");
				Sidebar.Initialize(this);
				Content.Initialize(this, Sidebar.SearchTabItem);
				Content.RevisionListViewUserControl.RevisionsDataSource.OnFetchRevisionsNeeded = FetchNextRevisionPage;
				NotificationBar.Initialize(this);
				RestoreSidebarColumnWidth();
				_layoutInitialized = true;
				Sidebar.RefreshTitle();
				PreferencesLocalization.Apply(Sidebar, ForkPlusSettings.Default.UiLanguage);
				PreferencesLocalization.Apply(Content, ForkPlusSettings.Default.UiLanguage);
			}
		}

		private bool RepositoryStateChanged(RepositoryStatus oldRepositoryStatus, RepositoryStatus newRepositoryStatus)
		{
			RepositoryState repositoryState = oldRepositoryStatus?.RepositoryState;
			if (repositoryState == null)
			{
				return false;
			}
			RepositoryState repositoryState2 = newRepositoryStatus?.RepositoryState;
			if (repositoryState2 == null)
			{
				return false;
			}
			if (repositoryState is RepositoryState.OK && repositoryState2 is RepositoryState.OK)
			{
				return false;
			}
			if (repositoryState is RepositoryState.MergeInProgress && repositoryState2 is RepositoryState.MergeInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.SquashInProgress && repositoryState2 is RepositoryState.SquashInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.RebaseInProgress && repositoryState2 is RepositoryState.RebaseInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.CherryPickInProgress && repositoryState2 is RepositoryState.CherryPickInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.RevertInProgress && repositoryState2 is RepositoryState.RevertInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.SequencerInProgress && repositoryState2 is RepositoryState.SequencerInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.UnmergedIndex && repositoryState2 is RepositoryState.UnmergedIndex)
			{
				return false;
			}
			if (repositoryState is RepositoryState.BisectInProgress && repositoryState2 is RepositoryState.BisectInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.AmInProgress && repositoryState2 is RepositoryState.AmInProgress)
			{
				return false;
			}
			return true;
		}

		private void RestoreSidebarColumnWidth()
		{
			double sidebarColumnWidth = ForkPlusSettings.Default.SidebarColumnWidth;
			RepositoryUserControlGrid.ColumnDefinitions[0].Width = new GridLength(sidebarColumnWidth, GridUnitType.Pixel);
		}

		private void SaveSidebarColumnWidth()
		{
			double value = RepositoryUserControlGrid.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.SidebarColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void UpdateRepoStatusAutomaticallyChanged(object sender, EventArgs<int> e)
		{
			NotificationCenter.Current.RaiseRepositoryUserControlIsDirtyChanged(this, this);
		}

		private void RepositoryColorChanged(object sender, EventArgs<RepositoryManager.Repository> e)
		{
			string repositoryPath = e.Value.Path;
			if (!(PathHelper.Normalize(GitModule.Path) != repositoryPath))
			{
				RepositoryColor = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == repositoryPath)?.Color ?? RepositoryColor.None;
				NotificationCenter.Current.RaiseRepositoryUserControlColorChanged(this, this);
			}
		}

		private void RepositoryNameChanged(object sender, EventArgs<string> e)
		{
			string value = e.Value;
			if (!(PathHelper.Normalize(GitModule.Path) != value))
			{
				RefreshRepositoryName();
			}
		}

		private void RefreshRepositoryName()
		{
			GitModule gitModule = GitModule;
			if (gitModule != null)
			{
				RepositoryName = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == gitModule.Path)?.Name() ?? "unknown";
				RefreshRepositoryTitle();
			}
			else
			{
				RepositoryName = null;
				RepositoryTitle = null;
			}
			NotificationCenter.Current.RaiseRepositoryUserControlTitleChanged(this, this);
			Sidebar?.RefreshTitle();
		}

		[Null]
		private static string FindSiblingWorktreeWithSameName(GitModule gitModule)
		{
			string commonGitDir = gitModule.CommonGitDir;
			string repositoryName = gitModule.RepositoryName;
			string path = gitModule.Path;
			string path2 = Path.Combine(commonGitDir, "worktrees");
			if (!Directory.Exists(path2))
			{
				return null;
			}
			string[] directories;
			try
			{
				directories = Directory.GetDirectories(path2);
			}
			catch
			{
				return null;
			}
			string[] array = directories;
			foreach (string path3 in array)
			{
				string text;
				try
				{
					text = File.ReadAllText(Path.Combine(path3, "gitdir")).TrimEnd();
				}
				catch
				{
					continue;
				}
				string text2 = (Path.IsPathRooted(text) ? Path.GetDirectoryName(text) : Path.GetDirectoryName(Path.GetFullPath(Path.Combine(path3, text))));
				if (!string.Equals(text2, path, StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetFileName(text2), repositoryName, StringComparison.OrdinalIgnoreCase))
				{
					return text2;
				}
			}
			return null;
		}

		public void Invalidate(SubDomain subdomains)
		{
			_viewModel.Invalidate(subdomains);
		}

		public void ResetSubdomains(SubDomain subdomains)
		{
			_viewModel.ResetSubdomains(subdomains);
		}

		public void InvalidateAndRefresh(SubDomain subdomains, RevisionSelector select = null, RepositoryViewMode priority = RepositoryViewMode.RevisionViewMode)
		{
			EnsureLayoutInitialized();
			Invalidate(subdomains);
			CancelActiveFetchRevisionsJobs();
			List<Sha> list = new List<Sha>();
			Sha? bottomShaInViewPort = Content.RevisionListViewUserControl.GetBottomShaInViewPort();
			if (bottomShaInViewPort.HasValue)
			{
				Sha valueOrDefault = bottomShaInViewPort.GetValueOrDefault();
				list.Add(valueOrDefault);
			}
			bottomShaInViewPort = Content.RevisionListViewUserControl.GetBottomShaInSelection();
			if (bottomShaInViewPort.HasValue)
			{
				Sha valueOrDefault2 = bottomShaInViewPort.GetValueOrDefault();
				list.Add(valueOrDefault2);
			}
			if (select != null && select is RevisionSelector.Sha sha)
			{
				list.AddRange(sha.Shas);
			}
			RefreshRepositoryCommand.Execute(this, list.ToArray(), select, priority);
		}

		public void UncheckAmendCheckBox()
		{
			Content.CommitUserControl.AmendMode = false;
		}

		// ===== v3.0.0 Undo/Redo =====

		/// <summary>
		/// v3.3.0：抓取当前仓库轻量 entry（HEAD sha + 当前分支名）。
		/// 旧版抓 11 字段调用 7 次 git 进程，新版只抓 2 字段调用 2 次 git 进程，性能提升 ~70%。
		/// </summary>
		private UndoEntry TakeSnapshot(string operationName)
		{
			try
			{
				GitCommandResult<UndoEntry> r = new SnapshotGitCommand().Execute(GitModule, operationName);
				return r.Succeeded ? r.Result : null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// v3.3.0：实时检测工作区是否 dirty。取代旧 RepositorySnapshot.IsWorkingTreeDirty 字段。
		/// 在 Undo/Redo 前调用，避免抓快照时白白消耗一次 git status 进程。
		/// </summary>
		private bool IsWorkingTreeDirty()
		{
			try
			{
				GitRequestResult r = new GitRequest(GitModule).Command("status", "--porcelain").Execute(silent: true);
				return r.Success && !string.IsNullOrWhiteSpace(r.Stdout);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 触发 UndoRedoStateChanged 事件。工具栏订阅以刷新按钮可用性。
		/// </summary>
		public void RaiseUndoRedoStateChanged()
		{
			_viewModel.RaiseUndoRedoStateChanged();
		}

		/// <summary>
		/// 包装一个会改仓库的操作：操作前抓快照入栈，操作失败时弹出栈顶。
		/// 调用方需要返回 GitCommandResult 以让包装层感知成功/失败。
		/// v3.0.4：新增 UndoRedoEnabled 开关。关闭时直接走 JobQueue.Add，跳过快照抓取（避免卡顿）。
		/// 开启时把 TakeSnapshot 推迟到 Job 内（后台线程），UI 线程立即返回，且能响应取消。
		/// v3.3.0：操作成功后把 OperationName 写入 UndoIndexStore（持久化到 .git/forkplus-undo-index.json），
		/// 跨会话保留 + reflog 兜底时仍可显示友好操作名。
		/// </summary>
		public Job AddUndoable(string operationName, Func<JobMonitor, GitCommandResult> action, JobFlags flags = JobFlags.Default, bool showMessageWhenDone = true)
		{
			// v3.0.4：开关关闭时直接走原始 JobQueue.Add，不抓快照
			if (!ForkPlusSettings.Default.UndoRedoEnabled)
			{
				return JobQueue.Add(operationName, delegate(JobMonitor monitor)
				{
					action(monitor);
				}, flags, showMessageWhenDone);
			}

			// 开关开启：在 Job 内抓 entry（后台线程，不阻塞 UI，可响应取消）
			return JobQueue.Add(operationName, delegate(JobMonitor monitor)
			{
				// 1. Job 内抓 entry（后台线程执行，git 进程不阻塞 UI）
				UndoEntry entry = null;
				try
				{
					entry = TakeSnapshot(operationName);
				}
				catch
				{
					entry = null;
				}
				if (monitor.IsCanceled)
				{
					return;
				}
				if (entry != null)
				{
					UndoRedoStack.RecordBeforeOperation(entry);
				}

				// 2. 执行实际操作，失败时 CancelLastRecord
			GitCommandResult result = null;
			try
			{
				result = action(monitor);
			}
			catch
			{
				// v3.1.1：异常时也要看 IsCanceled。已取消的话栈顶 entry 应当弹出，否则栈里会留下"已取消但未弹出"的孤儿
				UndoRedoStack.CancelLastRecord();
				RaiseUndoRedoStateChanged();
				throw;
			}
			// v3.1.1：用户在 action 执行过程中按了取消（IsCanceled=true）时，栈顶 entry 必须弹出，
			// 否则会表现为：撤销栈里已经有这条提交，但状态栏还显示"提交 1 个文件"且一直转圈、取消不掉
			if (monitor.IsCanceled || result == null || !result.Succeeded)
			{
				UndoRedoStack.CancelLastRecord();
				RaiseUndoRedoStateChanged();
				return;
			}
			// v3.3.0：操作成功后，把 {entry.HeadSha → operationName} 写入持久化索引
			// 用操作"前"的 HeadSha 作为 key（即 Undo 后要回到的状态），这样 Undo/Redo 走 reflog 时
			// 仍可通过 HeadSha join 出 UI 友好的操作名
			if (entry != null && !string.IsNullOrEmpty(entry.HeadSha))
			{
				try
				{
					UndoIndexStore indexStore = new UndoIndexStore(GitModule);
					indexStore.Record(new UndoIndexEntry(entry.HeadSha, operationName, entry.TimestampUtc));
				}
				catch
				{
					// 静默：索引写入失败不阻断主操作
				}
			}
			RaiseUndoRedoStateChanged();
			}, flags, showMessageWhenDone);
		}

		/// <summary>
		/// 包装一个会改仓库的操作：调用方不返回 GitCommandResult 时使用。
		/// 此重载无法感知操作失败，栈顶会保留（即使操作失败）。
		/// 优先使用 Func 重载。
		/// </summary>
		public Job AddUndoable(string operationName, Action<JobMonitor> action, JobFlags flags = JobFlags.Default, bool showMessageWhenDone = true)
		{
			return AddUndoable(operationName, delegate(JobMonitor monitor)
			{
				action(monitor);
				return GitCommandResult.Success();
			}, flags, showMessageWhenDone);
		}

		/// <summary>
		/// 撤销最近一次操作。弹栈并恢复到栈顶 entry 描述的状态。
		/// v3.3.0：恢复走 git reset --hard &lt;target.HeadSha&gt;（2 步：checkout + reset），
		/// 不再走旧版 5 步（重建分支/tag/stash）。
		/// </summary>
		public void Undo()
		{
			if (!UndoRedoStack.CanUndo)
			{
				return;
			}
			string opLabel = PreferencesLocalization.Current("Undo");
			UndoEntry currentEntry = TakeSnapshot(opLabel);
			if (currentEntry == null)
			{
				RaiseUndoRedoStateChanged();
				return;
			}
			// v3.3.0：dirty 检测改为实时调用 git status（旧版从 snapshot 字段读）
			bool isDirty = IsWorkingTreeDirty();
			if (!ConfirmAndStashBeforeRestore(isDirty, opLabel, out bool shouldStashFirst))
			{
				return;
			}
			// P3.2：peek 目标 entry，检查 Undo 是否会回退已 push 的 commit
			UndoEntry peekedTarget = UndoRedoStack.UndoHistory.Count > 0 ? UndoRedoStack.UndoHistory[0] : null;
			bool forcePushAfterRestore = false;
			if (ShouldPromptForPushedCommits(currentEntry, peekedTarget))
			{
				if (!ConfirmPushedUndo(opLabel, out forcePushAfterRestore))
				{
					return;
				}
			}
			UndoEntry target = UndoRedoStack.PopForUndo(currentEntry);
			if (target == null)
			{
				RaiseUndoRedoStateChanged();
				return;
			}
			JobQueue.Add(opLabel, delegate(JobMonitor monitor)
			{
				GitCommandResult preResult = EnsureStashedIfNeeded(shouldStashFirst, opLabel, monitor);
				if (!preResult.Succeeded)
				{
					ShowRestoreFailureAsync(preResult.Error);
					return;
				}
				GitCommandResult result = new RestoreSnapshotGitCommand().Execute(GitModule, target, monitor);
				if (result.Succeeded && forcePushAfterRestore)
				{
					// P3.2：本地恢复成功后，按用户选择执行 force push
					GitCommandResult pushResult = ForcePushCurrentBranch(monitor);
					base.Dispatcher.Async(delegate
					{
						InvalidateAndRefresh(SubDomain.All);
						RaiseUndoRedoStateChanged();
						if (!pushResult.Succeeded)
						{
							new ErrorWindow(this, pushResult.Error).ShowDialog();
						}
					});
					return;
				}
				base.Dispatcher.Async(delegate
				{
					InvalidateAndRefresh(SubDomain.All);
					RaiseUndoRedoStateChanged();
					if (!result.Succeeded)
					{
						new ErrorWindow(this, result.Error).ShowDialog();
					}
				});
			});
		}

		/// <summary>
		/// 重做最近被撤销的操作。
		/// v3.3.0：恢复走 git reset --hard &lt;target.HeadSha&gt;（2 步：checkout + reset）。
		/// </summary>
		public void Redo()
		{
			if (!UndoRedoStack.CanRedo)
			{
				return;
			}
			string opLabel = PreferencesLocalization.Current("Redo");
			UndoEntry currentEntry = TakeSnapshot(opLabel);
			if (currentEntry == null)
			{
				RaiseUndoRedoStateChanged();
				return;
			}
			// v3.3.0：dirty 检测改为实时调用 git status
			bool isDirty = IsWorkingTreeDirty();
			if (!ConfirmAndStashBeforeRestore(isDirty, opLabel, out bool shouldStashFirst))
			{
				return;
			}
			UndoEntry target = UndoRedoStack.PopForRedo(currentEntry);
			if (target == null)
			{
				RaiseUndoRedoStateChanged();
				return;
			}
			JobQueue.Add(opLabel, delegate(JobMonitor monitor)
			{
				GitCommandResult preResult = EnsureStashedIfNeeded(shouldStashFirst, opLabel, monitor);
				if (!preResult.Succeeded)
				{
					ShowRestoreFailureAsync(preResult.Error);
					return;
				}
				GitCommandResult result = new RestoreSnapshotGitCommand().Execute(GitModule, target, monitor);
				base.Dispatcher.Async(delegate
				{
					InvalidateAndRefresh(SubDomain.All);
					RaiseUndoRedoStateChanged();
					if (!result.Succeeded)
					{
						new ErrorWindow(this, result.Error).ShowDialog();
					}
				});
			});
		}

		/// <summary>
		/// P3.1：Undo/Redo 前 dirty 检查弹窗。
		/// 返回 false 表示用户取消（不应继续），true 表示可以继续。
		/// 若用户选择 stash，shouldStashFirst 会被设为 true。
		/// </summary>
		private bool ConfirmAndStashBeforeRestore(bool isDirty, string opLabel, out bool shouldStashFirst)
		{
			shouldStashFirst = false;
			if (!isDirty)
			{
				return true;
			}
			string message = PreferencesLocalization.FormatCurrent(
				"Working directory has uncommitted changes. {0} will discard them. Stash changes first?",
				opLabel);
			MessageBoxResult r = MessageBox.Show(message, opLabel, MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (r != MessageBoxResult.Yes)
			{
				return false;
			}
			shouldStashFirst = true;
			return true;
		}

		/// <summary>
		/// 在 Job 内同步执行 stash（若需要）。失败时返回 Failure。
		/// </summary>
		private GitCommandResult EnsureStashedIfNeeded(bool shouldStashFirst, string opLabel, JobMonitor monitor)
		{
			if (!shouldStashFirst)
			{
				return GitCommandResult.Success();
			}
			string stashMsg = PreferencesLocalization.FormatCurrent("Auto-stash before {0}", opLabel);
			GitCommandResult<bool> sr = new SaveStashGitCommand().Execute(GitModule, stashMsg, false, monitor);
			return sr.Succeeded ? GitCommandResult.Success() : GitCommandResult.Failure(sr.Error);
		}

		/// <summary>
		/// P3.2：判断是否需要在 Undo 前提示"已 push"。
		/// 条件：HEAD 会移动（current != target），且当前 HEAD 已 push 到某个 remote 分支。
		/// v3.3.0：参数从 RepositorySnapshot 改为 UndoEntry。
		/// </summary>
		private bool ShouldPromptForPushedCommits(UndoEntry currentEntry, UndoEntry target)
		{
			if (currentEntry == null || target == null)
			{
				return false;
			}
			if (string.IsNullOrEmpty(currentEntry.HeadSha) || string.IsNullOrEmpty(target.HeadSha))
			{
				return false;
			}
			if (currentEntry.HeadSha == target.HeadSha)
			{
				return false;
			}
			try
			{
				// 静默查询是否有 remote 分支包含当前 HEAD sha
				GitRequestResult r = new GitRequest(GitModule).Command("branch", "--list", "--remotes", "--contains", currentEntry.HeadSha).Execute(silent: true);
				if (!r.Success)
				{
					return false;
				}
				return !string.IsNullOrWhiteSpace(r.Stdout);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// P3.2：弹窗询问用户如何处理已 push 的 commit。
		/// Yes = 本地 Undo + 强制推送（force-with-lease）
		/// No = 仅本地 Undo（远端保持不变）
		/// Cancel = 中止
		/// 返回 false 表示用户取消，true 表示继续。
		/// </summary>
		private bool ConfirmPushedUndo(string opLabel, out bool forcePushAfterRestore)
		{
			forcePushAfterRestore = false;
			string message = PreferencesLocalization.FormatCurrent(
				"{0} will undo commit(s) that have been pushed to remote. Force push to remote too?",
				opLabel);
			MessageBoxResult r = MessageBox.Show(message, opLabel, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
			if (r == MessageBoxResult.Cancel)
			{
				return false;
			}
			forcePushAfterRestore = (r == MessageBoxResult.Yes);
			return true;
		}

		/// <summary>
		/// P3.2：对当前分支执行 force push（--force-with-lease）。
		/// 依赖当前分支的 upstream 配置。失败时返回 Failure。
		/// </summary>
		private GitCommandResult ForcePushCurrentBranch(JobMonitor monitor)
		{
			try
			{
				GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelperBt, "-c", "push.default=upstream", "push", "--force-with-lease", "--verbose", "--progress");
				GitRequestResult r = new GitRequest(GitModule).Command(gitCommand).Execute(monitor);
				if (!r.Success)
				{
					return GitCommandResult.Failure(r.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			catch (System.Exception ex)
			{
				return GitCommandResult.Failure(new GitCommandError.CallbackUnknownError(ex.Message));
			}
		}

		/// <summary>Job 内失败时在 UI 线程弹错误窗并刷新 Undo/Redo 状态。</summary>
		private void ShowRestoreFailureAsync(GitCommandError error)
		{
			base.Dispatcher.Async(delegate
			{
				new ErrorWindow(this, error).ShowDialog();
				RaiseUndoRedoStateChanged();
			});
		}

	}
}
