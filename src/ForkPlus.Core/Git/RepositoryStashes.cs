using System;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class RepositoryStashes
	{
		private static readonly Random _random = new Random();

		public static readonly RepositoryStashes Empty = new RepositoryStashes(new StashRevision[0], null);

		public Dictionary<Sha, int[]> StashIndexesByParent;

		public int Count => Items.Length;

		public StashRevision[] Items { get; }

		[Null]
		public DateTime? UpdateTime { get; }

		public Sha[] Parents { get; }

		public byte Gen { get; }

		public RepositoryStashes(StashRevision[] stashes, DateTime? updateTime)
		{
			Items = stashes;
			Parents = stashes.Map((StashRevision x) => x.FirstParent);
			UpdateTime = updateTime;
			StashIndexesByParent = stashes.GroupIndexes((StashRevision stash) => stash.FirstParent);
			Gen = (byte)_random.Next(256);
		}

		public Sha GetSha(RevisionStorage.Handle handle)
		{
			return Items[handle.Index].Sha;
		}

		public ShaBufferIterator GetParents(RevisionStorage.Handle handle)
		{
			int index = handle.Index;
			return new ShaBufferIterator(Parents, index, index + 1);
		}

		public HandleEnumerator GetEnumerator()
		{
			return new HandleEnumerator(0, Items.Length, isStash: true, Gen);
		}
	}
}
