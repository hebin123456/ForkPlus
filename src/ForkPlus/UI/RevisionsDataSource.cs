using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	public class RevisionsDataSource : IList, ICollection, IEnumerable, INotifyCollectionChanged
	{
		private struct Line
		{
			public Sha NextSha;

			public byte Id;

			public Line(Sha nextSha, byte id)
			{
				NextSha = nextSha;
				Id = id;
			}
		}

		private struct Page
		{
			public static readonly int PageSize = 100;

			public Range Rows => new Range(Index * PageSize, Math.Min((Index + 1) * PageSize, TotalRows));

			public Page? NextPage
			{
				get
				{
					int num = Index + 1;
					int num2 = TotalRows / PageSize + 1;
					if (num >= num2)
					{
						return null;
					}
					return FromPage(num, TotalRows);
				}
			}

			public int Index { get; }

			public int TotalRows { get; }

			private Page(int index, int totalRows)
			{
				Index = index;
				TotalRows = totalRows;
			}

			public static Page FromRow(int row, int totalRows)
			{
				return new Page(row / PageSize, totalRows);
			}

			public static Page FromPage(int page, int totalRows)
			{
				return new Page(page, totalRows);
			}

			public Page? RevisionHeadersPreloadPossible(int row, List<DecoratedRevision> decoratedRevisions)
			{
				if (!UpperHalf(Rows).Contains(row))
				{
					return null;
				}
				Page? nextPage = NextPage;
				if (nextPage.HasValue)
				{
					Page valueOrDefault = nextPage.GetValueOrDefault();
					int num = decoratedRevisions.Count / PageSize - 1;
					if (valueOrDefault.Index > num || !decoratedRevisions[valueOrDefault.Rows.Start].IsRevisionHeaderLoaded)
					{
						return valueOrDefault;
					}
					return null;
				}
				return null;
			}

			private static Range UpperHalf(Range range)
			{
				return new Range(range.Start + range.Length / 2, range.End);
			}
		}

		public Action OnFetchRevisionsNeeded;

		private JobQueue _jobQueue;

		private RepositoryStashes _stashes = RepositoryStashes.Empty;

		private RepositoryReferences _references = RepositoryReferences.Empty;

		private RepositoryRemotes _remotes = RepositoryRemotes.Empty;

		private RepositoryWorktrees _worktrees = RepositoryWorktrees.Empty;

		private bool _showStashesInRevisionList = true;

		private bool _reflog;

		private UserColors _userColors = UserColors.Empty;

		private GitModule _gitModule;

		private RevisionVisualGraph _visualGraph = RevisionVisualGraph.Empty;

		private RevisionContextSearch? _contextSearch;

		[Null]
		private RevisionSidebarSearch _sidebarSearch;

		private List<DecoratedRevision> _decoratedRevisions = new List<DecoratedRevision>();

		private bool _filterBranchesContainsActiveBranch;

		private HashSet<Sha> _reachableChildren;

		private HashSet<Sha> _aheadChildren = new HashSet<Sha>();

		private HashSet<Sha> _behindChildren = new HashSet<Sha>();

		private LruCache<Sha, RevisionHeader> _headerCache = new LruCache<Sha, RevisionHeader>(Page.PageSize * 2);

		private List<int> _activePageRequests = new List<int>();

		private List<Line> _lines1 = new List<Line>(128);

		private List<Line> _lines2 = new List<Line>(128);

		private static readonly byte _maxLineCount = 200;

		public int Count => _visualGraph.Count;

		public DecoratedRevision HeadRevision
		{
			get
			{
				int? headRow = HeadRow;
				if (headRow.HasValue)
				{
					int valueOrDefault = headRow.GetValueOrDefault();
					return GetDecoratedRevisionAtRow(valueOrDefault);
				}
				return null;
			}
		}

		public int? HeadRow
		{
			get
			{
				Sha? headSha = _references.HeadSha;
				if (headSha.HasValue)
				{
					Sha valueOrDefault = headSha.GetValueOrDefault();
					return FindRowBySha(valueOrDefault);
				}
				return null;
			}
		}

		public CollapseState CollapseState => _visualGraph.CollapseState;

		public RevisionStorage RevisionStorage => _visualGraph.RevisionStorage;

		public GitModule GitModule => _gitModule;

		public RevisionContextSearch? ContextSearch => _contextSearch;

		public int? ContextSearchCount => _contextSearch?.MatchCount;

		bool IList.IsReadOnly
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		bool IList.IsFixedSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		int ICollection.Count => Count;

		object ICollection.SyncRoot
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		bool ICollection.IsSynchronized
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		object IList.this[int index]
		{
			get
			{
				return GetDecoratedRevisionAtRow(index);
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public void Reload(JobQueue jobQueue, RevisionStorage revisionStorage, RepositoryStashes stashes, RepositoryReferences references, RepositoryRemotes remotes, RepositoryWorktrees worktrees, bool showStashesInRevisionList, bool reflog, CollapseState collapseState, UserColors userColors, GitModule gitModule)
		{
			_jobQueue = jobQueue;
			_stashes = stashes;
			_references = references;
			_remotes = remotes;
			_worktrees = worktrees;
			_showStashesInRevisionList = showStashesInRevisionList;
			_reflog = reflog;
			_userColors = userColors;
			_gitModule = gitModule;
			_visualGraph = RevisionVisualGraph.Create(revisionStorage, references, stashes, showStashesInRevisionList, reflog, collapseState);
			_activePageRequests.Clear();
			_decoratedRevisions = new List<DecoratedRevision>(Count);
			_reachableChildren = null;
			_aheadChildren = new HashSet<Sha>();
			_behindChildren = new HashSet<Sha>();
			_lines1.Clear();
			_lines2.Clear();
			LocalBranch activeBranch = references.ActiveBranch;
			if (activeBranch != null)
			{
				string upstreamFullReference = activeBranch.UpstreamFullReference;
				if (upstreamFullReference != null)
				{
					RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(references.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
					if (remoteBranch != null)
					{
						_aheadChildren.Add(activeBranch.Sha);
						_behindChildren.Add(remoteBranch.Sha);
					}
				}
				_filterBranchesContainsActiveBranch = references.FilterReferences.ContainsItem((string x) => x == activeBranch.FullReference);
			}
			else
			{
				_filterBranchesContainsActiveBranch = false;
			}
			this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		public void Extend(RevisionStorage newRevisionStorage, RevisionContextSearch? contextSearch = null)
		{
			RevisionVisualGraph revisionVisualGraph = RevisionVisualGraph.Create(newRevisionStorage, _references, _stashes, _showStashesInRevisionList, _reflog, _visualGraph.CollapseState);
			if (_visualGraph.RevisionStorage.Count != revisionVisualGraph.RevisionStorage.Count)
			{
				int count = _visualGraph.Count;
				_visualGraph = revisionVisualGraph;
				_contextSearch = contextSearch;
				int newCount = _visualGraph.Count;
				if (newCount > count)
				{
					// 性能修复（大仓库分页加载卡顿/崩溃）：原实现对每个新增行单独触发一次
					// CollectionChanged(Add) —— 一页 10000 条提交就是 10000 次 UI 通知，
					// 每次通知都会穿透 ItemsSourceView/SelectionModel/虚拟化面板，极易卡死或崩溃。
					// 改为：按行序批量物化新增行（图轨道计算必须按行序，无法省略），
					// 然后只发一次批量 Add 通知。
					// 注意：_decoratedRevisions 是惰性物化的，其 Count 可能小于 count，
					// DecorateRows 采用追加语义，必须从 _decoratedRevisions.Count 开始补齐，
					// 否则列表索引与行号错位（IndexOf 依赖 Row == 列表索引）。
					DecorateRows(new Range(_decoratedRevisions.Count, newCount));
					List<DecoratedRevision> added = _decoratedRevisions.GetRange(count, newCount - count);
					this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added, count));
				}
			}
		}

		public (IReadOnlyList<int>, HashSet<Sha>) FindRowsBySha(IReadOnlyList<Sha> shas)
		{
			return _visualGraph.FindRowsBySha(shas);
		}

		public bool IsRowCollapsed(int row)
		{
			return _visualGraph.IsRowCollapsed(row);
		}

		public DecoratedRevision GetDecoratedRevisionAtRow(int row)
		{
			EnsureRevisionLoaded(row);
			return _decoratedRevisions[row];
		}

		public Sha ShaAtRow(int row)
		{
			return _visualGraph.GetShaAtRow(row);
		}

		public int? FindRowBySha(Sha sha)
		{
			return _visualGraph.FindRowsBySha(new Sha[1] { sha }).Item1.FirstItemStruct();
		}

		public void RefreshTheme()
		{
			foreach (DecoratedRevision decoratedRevision in _decoratedRevisions)
			{
				decoratedRevision?.RefreshTheme();
			}
		}

		public void SetContextSearch(RevisionContextSearch? contextSearch)
		{
			bool flag = _contextSearch?.SearchString != contextSearch?.SearchString;
			List<int> list = new List<int>();
			for (int i = 0; i < _decoratedRevisions.Count; i++)
			{
				Sha shaAtRow = _visualGraph.GetShaAtRow(i);
				if (flag)
				{
					ref RevisionContextSearch? contextSearch2 = ref _contextSearch;
					if ((contextSearch2.HasValue && contextSearch2.GetValueOrDefault().IsMatch(shaAtRow)) || (contextSearch.HasValue && contextSearch.GetValueOrDefault().IsMatch(shaAtRow)))
					{
						list.Add(i);
					}
				}
				else if (_contextSearch?.IsMatch(shaAtRow) != contextSearch?.IsMatch(shaAtRow))
				{
					list.Add(i);
				}
			}
			_contextSearch = contextSearch;
			foreach (int item in list)
			{
				RefreshSearchHighlighting(item);
			}
		}

		public void SetSidebarSearch([Null] RevisionSearchQuery query)
		{
			if (string.IsNullOrEmpty(query?.SearchString))
			{
				_sidebarSearch = null;
			}
			else
			{
				_sidebarSearch = new RevisionSidebarSearch(_visualGraph, query);
			}
			RefreshSearchHighlighting();
		}

		private void RefreshSearchHighlighting(int row)
		{
			DecoratedRevision decoratedRevision = _decoratedRevisions[row];
			RevisionContextSearch? contextSearch = _contextSearch;
			if (contextSearch.HasValue)
			{
				RevisionContextSearch valueOrDefault = contextSearch.GetValueOrDefault();
				string searchString = valueOrDefault.SearchString;
				decoratedRevision.IsSearchMatch = valueOrDefault.IsMatch(decoratedRevision.Sha);
				decoratedRevision.SubjectSearchString = searchString;
				decoratedRevision.SearchString = searchString;
			}
			else if (_sidebarSearch != null)
			{
				decoratedRevision.IsSearchMatch = _sidebarSearch.Match(decoratedRevision.Sha);
				if (_sidebarSearch.Query.Type == RevisionSearchType.Message)
				{
					decoratedRevision.SubjectSearchString = _sidebarSearch.Query.SearchString;
				}
				else
				{
					decoratedRevision.SubjectSearchString = null;
				}
				decoratedRevision.SearchString = null;
			}
			else
			{
				decoratedRevision.SubjectSearchString = null;
				decoratedRevision.SearchString = null;
				decoratedRevision.IsSearchMatch = false;
			}
		}

		private void RefreshSearchHighlighting()
		{
			for (int i = 0; i < _decoratedRevisions.Count; i++)
			{
				RefreshSearchHighlighting(i);
			}
		}

		public int? NextContextSearchMatch(int start, bool initialJump)
		{
			RevisionContextSearch? contextSearch = _contextSearch;
			if (contextSearch.HasValue)
			{
				RevisionContextSearch valueOrDefault = contextSearch.GetValueOrDefault();
				for (int i = ((start >= 0) ? start : 0); i < _visualGraph.Count; i++)
				{
					if (valueOrDefault.IsMatch(_visualGraph.GetShaAtRow(i)) && (initialJump || i != start))
					{
						return i;
					}
				}
				if (!initialJump)
				{
					return null;
				}
				for (int i = 0; i < start; i++)
				{
					if (valueOrDefault.IsMatch(_visualGraph.GetShaAtRow(i)))
					{
						return i;
					}
				}
				return null;
			}
			return null;
		}

		public int? PreviousContextSearchMatch(int start)
		{
			RevisionContextSearch? contextSearch = _contextSearch;
			if (contextSearch.HasValue)
			{
				RevisionContextSearch valueOrDefault = contextSearch.GetValueOrDefault();
				for (int num = start - 1; num >= 0; num--)
				{
					if (valueOrDefault.IsMatch(_visualGraph.GetShaAtRow(num)))
					{
						return num;
					}
				}
				return null;
			}
			return null;
		}

		public void AddSidebarSearchMatch(string searchQuery, Sha sha)
		{
			if (_sidebarSearch == null)
			{
				return;
			}
			int num = _sidebarSearch.AddSearchMatch(sha);
			if (num >= 0 && num < _decoratedRevisions.Count)
			{
				_decoratedRevisions[num].IsSearchMatch = true;
				if (_sidebarSearch.Query.Type == RevisionSearchType.Message)
				{
					_decoratedRevisions[num].SubjectSearchString = searchQuery;
				}
			}
		}

		private void FetchRevisionsIfNeeded(int row)
		{
			if (_visualGraph.RevisionStorage.HasMore && _visualGraph.Count - row <= 5000)
			{
				OnFetchRevisionsNeeded?.Invoke();
			}
		}

		private void EnsureRevisionLoaded(int row)
		{
			FetchRevisionsIfNeeded(row);
			Page requestedPage = Page.FromRow(row, _visualGraph.Count);
			if (row < _decoratedRevisions.Count && _decoratedRevisions[row].IsRevisionHeaderLoaded)
			{
				Page? page = requestedPage.RevisionHeadersPreloadPossible(row, _decoratedRevisions);
				if (!page.HasValue)
				{
					return;
				}
				Page valueOrDefault = page.GetValueOrDefault();
				requestedPage = valueOrDefault;
			}
			if (requestedPage.Rows.End > _decoratedRevisions.Count)
			{
				DecorateRows(new Range(_decoratedRevisions.Count, requestedPage.Rows.End));
			}
			if ((row == 0 && _visualGraph.Count > 1) || _activePageRequests.Contains(requestedPage.Index))
			{
				return;
			}
			_activePageRequests.Add(requestedPage.Index);
			Range rows = requestedPage.Rows;
			List<int> uncachedRows = new List<int>();
			List<Sha> uncachedShas = new List<Sha>();
			for (int i = rows.Start; i < rows.End; i++)
			{
				Sha shaAtRow = _visualGraph.GetShaAtRow(i);
				if (_headerCache.TryGet(shaAtRow, out var value))
				{
					byte colorId = _userColors.GetColorId(value.Author.Email);
					_decoratedRevisions[i].SetRevisionHeader(value, colorId);
				}
				else
				{
					uncachedRows.Add(i);
					uncachedShas.Add(shaAtRow);
				}
			}
			RevisionVisualGraph revisionVisualGraph = _visualGraph;
			_jobQueue.Add(PreferencesLocalization.Current("Load revision headers"), delegate
			{
				GitCommandResult<RevisionHeader[]> gitCommandResult = new GetRevisionHeadersGitCommand().Execute(_gitModule, uncachedShas.ToArray());
				if (!gitCommandResult.Succeeded)
				{
					Log.Error(gitCommandResult.Error.FriendlyDescription);
				}
				else
				{
					RevisionHeader[] revisionHeaders = gitCommandResult.Result;
					global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
					{
						if (revisionVisualGraph != _visualGraph)
						{
							_activePageRequests.Remove(requestedPage.Index);
						}
						else
						{
							for (int j = 0; j < uncachedRows.Count; j++)
							{
								Sha key = uncachedShas[j];
								RevisionHeader revisionHeader = revisionHeaders[j];
								byte colorId2 = _userColors.GetColorId(revisionHeader.Author.Email);
								_headerCache.Put(key, revisionHeader);
								_decoratedRevisions[uncachedRows[j]].SetRevisionHeader(revisionHeader, colorId2);
							}
							_activePageRequests.Remove(requestedPage.Index);
						}
					});
				}
			}, JobFlags.Hidden);
		}

		private void DecorateRows(Range rowRange)
		{
			for (int i = rowRange.Start; i < rowRange.End; i++)
			{
				DecoratedRevision item = DecorateRevision(i);
				_decoratedRevisions.Add(item);
			}
		}

		private DecoratedRevision DecorateRevision(int row)
		{
			Sha shaAtRow = _visualGraph.GetShaAtRow(row);
			Sha value = shaAtRow;
			Sha? headSha = _references.HeadSha;
			bool flag = value == headSha;
			bool isReachable = false;
			if (_references.FilterReferences.Length == 0 || _filterBranchesContainsActiveBranch)
			{
				if (flag)
				{
					isReachable = true;
					_reachableChildren = new HashSet<Sha>();
					ShaBufferIterator visualParentsAtRow = _visualGraph.GetVisualParentsAtRow(row);
					Sha[] items = visualParentsAtRow.Items;
					for (int i = visualParentsAtRow.Start; i < visualParentsAtRow.End; i++)
					{
						_reachableChildren.Add(items[i]);
					}
				}
				else if (_reachableChildren != null && _reachableChildren.Remove(shaAtRow))
				{
					isReachable = true;
					ShaBufferIterator visualParentsAtRow2 = _visualGraph.GetVisualParentsAtRow(row);
					Sha[] items2 = visualParentsAtRow2.Items;
					for (int j = visualParentsAtRow2.Start; j < visualParentsAtRow2.End; j++)
					{
						_reachableChildren.Add(items2[j]);
					}
				}
			}
			else
			{
				isReachable = true;
			}
			ShaBufferIterator visualParentsAtRow3 = _visualGraph.GetVisualParentsAtRow(row);
			GraphInfo graphInfo = ((row % 2 == 0) ? CreateGraph(_lines1, _lines2, visualParentsAtRow3, shaAtRow) : CreateGraph(_lines2, _lines1, visualParentsAtRow3, shaAtRow));
			string subjectSearchString = null;
			string searchString = null;
			bool searchMatch = false;
			if (_sidebarSearch != null && _sidebarSearch.Match(shaAtRow))
			{
				searchMatch = true;
				if (_sidebarSearch.Query.Type == RevisionSearchType.Message)
				{
					subjectSearchString = _sidebarSearch.Query.SearchString;
				}
			}
			RevisionContextSearch? contextSearch = _contextSearch;
			if (contextSearch.HasValue)
			{
				RevisionContextSearch valueOrDefault = contextSearch.GetValueOrDefault();
				searchMatch = valueOrDefault.IsMatch(shaAtRow);
				subjectSearchString = valueOrDefault.SearchString;
				searchString = valueOrDefault.SearchString;
			}
			ReferenceViewModel[] references;
			if (_visualGraph.IsStash(row))
			{
				ReferenceViewModel[] array = new StashReferenceViewModel[1]
				{
					new StashReferenceViewModel(graphInfo.CurrentCommitColumn, _visualGraph.GetStashRevisionAtRow(row))
				};
				references = array;
			}
			else
			{
				references = CreateReferenceViewModels(shaAtRow, graphInfo.CurrentCommitColumn, _references, _remotes, _worktrees);
			}
			ActiveBranchCommitStatus upstreamStatus = ActiveBranchCommitStatus.None;
			if (_aheadChildren.Count > 0 && _behindChildren.Count > 0)
			{
				bool flag2 = _aheadChildren.Remove(shaAtRow);
				bool flag3 = _behindChildren.Remove(shaAtRow);
				if (!(flag2 && flag3))
				{
					if (flag2)
					{
						upstreamStatus = ActiveBranchCommitStatus.Ahead;
						ShaBufferIterator.Enumerator enumerator = visualParentsAtRow3.GetEnumerator();
						while (enumerator.MoveNext())
						{
							Sha current = enumerator.Current;
							_aheadChildren.Add(current);
						}
					}
					else if (flag3)
					{
						upstreamStatus = ActiveBranchCommitStatus.Behind;
						ShaBufferIterator.Enumerator enumerator = visualParentsAtRow3.GetEnumerator();
						while (enumerator.MoveNext())
						{
							Sha current2 = enumerator.Current;
							_behindChildren.Add(current2);
						}
					}
				}
			}
			return new DecoratedRevision(row, _visualGraph, flag, references, isReachable, upstreamStatus, graphInfo, searchMatch, subjectSearchString, searchString);
		}

		[Null]
		private static ReferenceViewModel[] CreateReferenceViewModels(Sha sha, int graphColumn, RepositoryReferences repositoryReferences, RepositoryRemotes remotes, RepositoryWorktrees worktrees)
		{
			if (!repositoryReferences.ReferencesBySha.TryGetValue(sha, out var value))
			{
				return null;
			}
			Reference[] items = repositoryReferences.Items;
			List<ReferenceViewModel> list = new List<ReferenceViewModel>(value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				Reference reference = items[value[i]];
				if (repositoryReferences.IsHidden(reference))
				{
					continue;
				}
				if (!(reference is LocalBranch localBranch))
				{
					RemoteBranch remoteBranch = reference as RemoteBranch;
					if (remoteBranch == null)
					{
						if (!(reference is Tag tag))
						{
							if (reference is BisectMark bisectMark)
							{
								list.Add(new BisectMarkViewModel(graphColumn, bisectMark));
							}
						}
						else
						{
							list.Add(new TagViewModel(graphColumn, tag));
						}
					}
					else
					{
						global::Avalonia.Media.IImage remoteIcon = IReadOnlyListExtensions.FirstItem(remotes.Items, (Remote x) => x.Name == remoteBranch.Remote)?.Icon;
						list.Add(new RemoteBranchViewModel(graphColumn, remoteBranch, remoteIcon));
					}
				}
				else
				{
					bool isWorktree = worktrees.WorktreesByFullReference.ContainsKey(localBranch.FullReference);
					list.Add(new LocalBranchViewModel(graphColumn, localBranch, isWorktree));
				}
			}
			ReferenceViewModel[] array = list.ToArray();
			SortReferenceViewModels(array);
			return array;
		}

		private static void SortReferenceViewModels(ReferenceViewModel[] items)
		{
			Array.Sort(items, (ReferenceViewModel x, ReferenceViewModel y) => NaturalStringComparer.Instance.Compare(x.Reference.FullReference, y.Reference.FullReference));
			if (!ForkPlusSettings.Default.CompactBranchLabels)
			{
				return;
			}
			for (int i = 0; i < items.Length && items[i].Reference is LocalBranch { UpstreamFullReference: var upstreamFullReference }; i++)
			{
				if (upstreamFullReference == null)
				{
					continue;
				}
				ReferenceViewModel referenceViewModel = items[i];
				for (int j = i + 1; j < items.Length; j++)
				{
					if (items[j] is RemoteBranchViewModel remoteBranchViewModel && remoteBranchViewModel.RemoteBranch.FullReference == upstreamFullReference)
					{
						remoteBranchViewModel.HasDownstream = true;
						for (int k = i; k < j; k++)
						{
							items[k] = items[k + 1];
						}
						items[j] = referenceViewModel;
						i--;
						break;
					}
				}
			}
		}

		private static GraphInfo CreateGraph(List<Line> previousLines, List<Line> currentLines, ShaBufferIterator parents, Sha sha)
		{
			byte b = byte.MaxValue;
			byte laneIndex = byte.MaxValue;
			bool flag = false;
			List<GraphLine> list = new List<GraphLine>();
			currentLines.Clear();
			ShaBufferIterator.Enumerator enumerator = parents.GetEnumerator();
			Sha? sha2 = ((!enumerator.MoveNext()) ? null : new Sha?(enumerator.Current));
			for (int i = 0; i < previousLines.Count; i++)
			{
				Line item = previousLines[i];
				if (item.NextSha != sha)
				{
					list.Add(new GraphLine(item.Id, (byte)i, (byte)currentLines.Count, (byte)currentLines.Count));
					currentLines.Add(item);
				}
				else if (!flag)
				{
					flag = true;
					b = (byte)currentLines.Count;
					laneIndex = item.Id;
					if (!sha2.HasValue)
					{
						list.Add(new GraphLine(item.Id, (byte)i, b, byte.MaxValue));
						continue;
					}
					currentLines.Add(new Line(sha2.Value, item.Id));
					list.Add(new GraphLine(item.Id, (byte)i, b, b));
				}
				else
				{
					list.Add(new GraphLine(item.Id, (byte)i, b, byte.MaxValue));
				}
			}
			if (currentLines.Count < _maxLineCount)
			{
				if (!flag && sha2.HasValue)
				{
					b = (byte)currentLines.Count;
					Line item2 = new Line(sha2.Value, b);
					currentLines.Add(item2);
					laneIndex = item2.Id;
					list.Add(new GraphLine(item2.Id, byte.MaxValue, b, b));
				}
				Sha[] items = parents.Items;
				for (int j = parents.Start + 1; j < parents.End; j++)
				{
					Sha sha3 = items[j];
					byte b2 = 0;
					bool flag2 = false;
					foreach (Line currentLine in currentLines)
					{
						if (currentLine.NextSha == sha3)
						{
							list.Add(new GraphLine(currentLine.Id, byte.MaxValue, b, b2));
							flag2 = true;
							break;
						}
						b2++;
					}
					if (!flag2)
					{
						Line item3 = new Line(sha3, (byte)currentLines.Count);
						list.Add(new GraphLine(item3.Id, byte.MaxValue, b, (byte)currentLines.Count));
						currentLines.Add(item3);
					}
				}
			}
			return new GraphInfo(list.ToArray(), b, laneIndex);
		}

		int IList.Add(object value)
		{
			throw new NotImplementedException();
		}

		bool IList.Contains(object value)
		{
			// 与 IndexOf 语义对齐（引用匹配当前已物化行）。WPF 版遍历 _decoratedRevisions
			// 引用比较，行为等价；改为复用 IndexOf 的 O(1) 快路径。
			return ((IList)this).IndexOf(value) >= 0;
		}

		void IList.Clear()
		{
			throw new NotImplementedException();
		}

		int IList.IndexOf(object value)
		{
			// v3.12 修复（启动后底部 tab/右键菜单状态错乱）：原先恒返回 -1，违反 IList 契约。
			// Avalonia SelectionModel 的 item→index 解析（SelectedItems.Add / SelectedItem /
			// ScrollIntoView 等）全部依赖 IndexOf：返回 -1 时 SelectedItems.Add(item) 会产生
			// 无法解析索引的"孤儿选中项"，后续按索引的选中（如 ApplyContainerSelection 的
			// container.IsSelected=true）再叠加一次 → 同一项在 SelectedItems 里出现两次 →
			// 被误判为双选（Range）→ 底部"提交/文件树"tab 变灰、右键菜单变成多选菜单，
			// 直到用户手动点击其他行再点回才恢复。
			// DecoratedRevision.Row 与本数据源索引 1:1（DecorateRows 按 row 递增 Add），
			// 用 Row + 引用校验做 O(1) 精确解析；非当前实例（如 Reload 后的旧实例）返回 -1。
			if (value is DecoratedRevision decoratedRevision)
			{
				int row = decoratedRevision.Row;
				if (row >= 0 && row < _decoratedRevisions.Count && ReferenceEquals(_decoratedRevisions[row], decoratedRevision))
				{
					return row;
				}
			}
			return -1;
		}

		void IList.Insert(int index, object value)
		{
			throw new NotImplementedException();
		}

		void IList.Remove(object value)
		{
			throw new NotImplementedException();
		}

		void IList.RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		void ICollection.CopyTo(Array array, int index)
		{
			throw new NotImplementedException();
		}

		// Migration note：WPF 版返回 _decoratedRevisions.GetEnumerator()（仅"已物化"行，Reload 后为空）。
		// WPF ItemContainerGenerator 生成容器走 Count + IList 索引器（GetDecoratedRevisionAtRow 惰性物化），
		// 枚举器从不参与容器生成 → 空枚举器在 WPF 下无影响（潜伏契约违背：Count=1 但枚举 0 项）。
		// Avalonia 12 PanelContainerGenerator.OnItemsChanged 的 Reset 分支改用 foreach 枚举 ItemsView
		// （ItemCollection.GetEnumerator → Source.GetEnumerator）生成容器 → 枚举空列表 = 0 容器 = 列表空白。
		// 修正：按行枚举（与索引器/Count 语义一致，逐行惰性物化），[probe] 迁移期实证。
		// 注：当前 ItemsPanel 是非虚拟化 StackPanel，大仓库全量物化有性能代价，后续切 VirtualizingStackPanel 优化。
		IEnumerator IEnumerable.GetEnumerator()
		{
			int count = Count;
			for (int i = 0; i < count; i++)
			{
				yield return GetDecoratedRevisionAtRow(i);
			}
		}
	}
}
