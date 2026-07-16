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

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public static readonly RepositoryUserControlCommands Commands = new RepositoryUserControlCommands();

		public readonly TempFileManager TempFileManager = new TempFileManager();

		public readonly JobQueue JobQueue = new JobQueue();

		private bool _isDirty;

		private bool _layoutInitialized;

		private SubDomain _invalidatedSubdomains = SubDomain.All;

		private RepositoryViewMode _viewMode;

		private Job _activeFetchRevisionsUntilShaJob;

		[Null]
		private Job _activeFetchRevisionsNextPageJob;

		private readonly RefreshRepositoryCommand RefreshRepositoryCommand = new RefreshRepositoryCommand();

		public RepositoryData RepositoryData { get; private set; }

		public RepositoryStatus RepositoryStatus { get; private set; }

		public GitModule GitModule { get; private set; }

		public CommitGraphCache CommitGraphCache { get; private set; }

		public string RepositoryName { get; private set; }

		public string ParentRepositoryName { get; private set; }

		public string RepositoryTitle { get; private set; }

		public bool IsDirty
		{
			get
			{
				if (_isDirty)
				{
					return ForkPlusSettings.Default.AutomaticStatusUpdateInterval > 0;
				}
				return false;
			}
			set
			{
				_isDirty = value;
			}
		}

		public RepositoryColor RepositoryColor { get; private set; }

		public SubDomain InvalidatedSubdomains => _invalidatedSubdomains;

		public RepositoryViewMode ViewMode
		{
			get
			{
				return _viewMode;
			}
			private set
			{
				if (_viewMode != value)
				{
					_viewMode = value;
					Content.SetRepositoryViewMode(_viewMode);
					Sidebar.SetRepositoryViewMode(_viewMode);
					NotificationBar.Refresh();
				}
			}
		}

		public bool ShowReflogInRevisionList { get; set; }

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
			if (gitModule.Type == ModuleType.Submodule)
			{
				string parentRepoPath = gitModule.ParentRepoPath;
				if (parentRepoPath == null)
				{
					return null;
				}
				return RepositoryManager.Instance.FindRepositoryName(parentRepoPath) ?? Path.GetFileName(parentRepoPath);
			}
			if (gitModule.Type == ModuleType.Worktree)
			{
				string commonGitDir = gitModule.CommonGitDir;
				if (commonGitDir == null)
				{
					return null;
				}
				if (Path.GetFileName(commonGitDir) != ".git")
				{
					return Path.GetFileName(commonGitDir);
				}
				string directoryName = Path.GetDirectoryName(commonGitDir);
				return RepositoryManager.Instance.FindRepositoryName(directoryName) ?? Path.GetFileName(directoryName);
			}
			return null;
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
			HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (ChangedFile changedFile in changedFiles)
			{
				paths.Add(changedFile.Path);
			}
			return paths.Count;
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
			_activeFetchRevisionsUntilShaJob?.Monitor.Cancel();
			_activeFetchRevisionsUntilShaJob = null;
			_activeFetchRevisionsNextPageJob?.Monitor.Cancel();
			_activeFetchRevisionsNextPageJob = null;
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
			_invalidatedSubdomains |= subdomains;
		}

		public void ResetSubdomains(SubDomain subdomains)
		{
			_invalidatedSubdomains &= ~subdomains;
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

	}
}
