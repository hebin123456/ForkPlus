using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class RevisionVisualGraph
	{
		public static readonly RevisionVisualGraph Empty = new RevisionVisualGraph(RevisionStorage.Empty, RepositoryStashes.Empty, CollapseState.Empty, new List<RevisionStorage.Handle>());

		private readonly RevisionStorage _revisionStorage;

		private readonly RepositoryStashes _stashes;

		private readonly List<RevisionStorage.Handle> _visibleItems;

		public int Count => _visibleItems.Count;

		public CollapseState CollapseState { get; }

		public RevisionStorage RevisionStorage => _revisionStorage;

		private RevisionVisualGraph(RevisionStorage revisions, RepositoryStashes stashes, CollapseState collapseState, List<RevisionStorage.Handle> visibleItems)
		{
			_revisionStorage = revisions;
			_stashes = stashes;
			CollapseState = collapseState;
			_visibleItems = visibleItems;
		}

		public static RevisionVisualGraph Create(RevisionStorage revisions, RepositoryReferences references, RepositoryStashes stashes, bool showStashes, bool reflog, CollapseState collapseState)
		{
			List<RevisionStorage.Handle> list = new List<RevisionStorage.Handle>(revisions.Count);
			HashSet<Sha> hashSet = new HashSet<Sha>();
			if (!reflog)
			{
				if (references.IsFilterEnabled || references.IsHideEnabled)
				{
					if (references.ActiveBranch == null)
					{
						Sha? headSha = references.HeadSha;
						if (headSha.HasValue)
						{
							Sha valueOrDefault = headSha.GetValueOrDefault();
							hashSet.Add(valueOrDefault);
						}
					}
					Reference[] items = references.Items;
					foreach (Reference reference in items)
					{
						if (!references.IsHidden(reference.FullReference) && (!references.IsFilterEnabled || references.IsFiltered(reference.FullReference)))
						{
							hashSet.Add(reference.Sha);
						}
					}
				}
				else
				{
					bool hideTags = references.HideTags;
					if (hideTags || collapseState.CollapseAllMode || collapseState.ToggledShas.Count > 0)
					{
						if (references.ActiveBranch == null)
						{
							Sha? headSha = references.HeadSha;
							if (headSha.HasValue)
							{
								Sha valueOrDefault2 = headSha.GetValueOrDefault();
								hashSet.Add(valueOrDefault2);
							}
						}
						Reference[] items = references.Items;
						foreach (Reference reference2 in items)
						{
							if (!hideTags || !(reference2 is Tag))
							{
								hashSet.Add(reference2.Sha);
							}
						}
					}
				}
			}
			bool num = hashSet.Count == 0;
			HashSet<Sha> hashSet2 = new HashSet<Sha>();
			HashSet<Sha> hashSet3 = new HashSet<Sha>();
			if (num)
			{
				HandleEnumerator enumerator = revisions.GetEnumerator();
				while (enumerator.MoveNext())
				{
					RevisionStorage.Handle current = enumerator.Current;
					Sha sha = revisions.GetSha(current);
					if (showStashes && stashes.StashIndexesByParent.TryGetValue(sha, out var value))
					{
						int[] array = value;
						foreach (int index in array)
						{
							list.Add(new RevisionStorage.Handle(index, isStash: true, stashes.Gen));
						}
					}
					list.Add(current);
				}
			}
			else
			{
				HandleEnumerator enumerator = revisions.GetEnumerator();
				while (enumerator.MoveNext())
				{
					RevisionStorage.Handle current2 = enumerator.Current;
					Sha sha2 = revisions.GetSha(current2);
					bool flag = hashSet3.Remove(sha2);
					bool flag2 = hashSet2.Remove(sha2);
					bool flag3 = hashSet.Remove(sha2);
					if (flag && !flag2)
					{
						ShaBufferIterator parents = revisions.GetParents(current2);
						for (int j = parents.Start; j < parents.End; j++)
						{
							Sha item = parents.Items[j];
							if (!hashSet2.Contains(item))
							{
								hashSet3.Add(item);
							}
						}
					}
					if (!flag2 && (!flag3 || flag))
					{
						continue;
					}
					if (showStashes && stashes.StashIndexesByParent.TryGetValue(sha2, out var value2))
					{
						int[] array = value2;
						foreach (int index2 in array)
						{
							list.Add(new RevisionStorage.Handle(index2, isStash: true, stashes.Gen));
						}
					}
					list.Add(current2);
					bool flag4 = collapseState.IsCollapsed(sha2);
					ShaBufferIterator parents2 = revisions.GetParents(current2);
					for (int k = parents2.Start; k < parents2.End; k++)
					{
						Sha item2 = parents2.Items[k];
						if (k == parents2.Start)
						{
							hashSet2.Add(item2);
						}
						else if (flag4)
						{
							hashSet3.Add(item2);
						}
						else
						{
							hashSet2.Add(item2);
						}
					}
				}
			}
			return new RevisionVisualGraph(revisions, stashes, collapseState, list);
		}

		public bool IsStash(int row)
		{
			return _visibleItems[row].IsStash;
		}

		public StashRevision GetStashRevisionAtRow(int row)
		{
			RevisionStorage.Handle handle = _visibleItems[row];
			return _stashes.Items[handle.Index];
		}

		public Sha GetShaAtRow(int row)
		{
			RevisionStorage.Handle handle = _visibleItems[row];
			if (handle.IsStash)
			{
				return _stashes.GetSha(handle);
			}
			return _revisionStorage.GetSha(handle);
		}

		public ShaBufferIterator GetParentsAtRow(int row)
		{
			RevisionStorage.Handle handle = _visibleItems[row];
			if (handle.IsStash)
			{
				return _stashes.GetParents(handle);
			}
			return _revisionStorage.GetParents(handle);
		}

		public ShaBufferIterator GetVisualParentsAtRow(int row)
		{
			RevisionStorage.Handle handle = _visibleItems[row];
			if (handle.IsStash)
			{
				return _stashes.GetParents(handle);
			}
			if (IsRowCollapsed(row))
			{
				return _revisionStorage.GetParents(handle).FirstOnly();
			}
			return _revisionStorage.GetParents(handle);
		}

		public (IReadOnlyList<int>, HashSet<Sha>) FindRowsBySha(IReadOnlyList<Sha> shas)
		{
			if (shas.Count == 0)
			{
				return (new int[0], new HashSet<Sha>());
			}
			HashSet<Sha> hashSet = new HashSet<Sha>(shas);
			List<int> list = new List<int>(shas.Count);
			for (int i = 0; i < _visibleItems.Count; i++)
			{
				RevisionStorage.Handle handle = _visibleItems[i];
				Sha item = (handle.IsStash ? _stashes.GetSha(handle) : _revisionStorage.GetSha(handle));
				if (hashSet.Remove(item))
				{
					list.Add(i);
					if (hashSet.Count == 0)
					{
						break;
					}
				}
			}
			return (list, hashSet);
		}

		public bool IsRowCollapsed(int row)
		{
			return CollapseState.IsCollapsed(GetShaAtRow(row));
		}
	}
}
