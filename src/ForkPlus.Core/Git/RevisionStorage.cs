using System;
using System.Collections.Generic;
using ForkPlus.Git.Commands;

namespace ForkPlus.Git
{
	public class RevisionStorage
	{
		public struct Handle
		{
			private const uint StashMask = 2147483648u;

			private const uint GenMask = 2139095040u;

			private const uint IndexMask = 8388607u;

			private readonly uint _val;

			public int Index => (int)(_val & 0x7FFFFF);

			public bool IsStash => (_val & 0x80000000u) != 0;

			public byte Gen => (byte)((_val & 0x7F800000) >> 23);

			public Handle(int index, bool isStash, byte gen)
			{
				if (isStash)
				{
					_val = 0x80000000u | (uint)(gen << 23) | (uint)index;
				}
				else
				{
					_val = (uint)((gen << 23) | index);
				}
			}
		}

		private static readonly Random _random = new Random();

		public static readonly RevisionStorage Empty = new RevisionStorage(new Sha[0], new Sha[0], new int[0], hasMore: false, 0L);

		public int Count => Shas.Length;

		private Sha[] Shas { get; }

		private Sha[] Parents { get; }

		private int[] ParentsIndexes { get; }

		public bool HasMore { get; }

		public byte Gen { get; }

		public long Timestamp { get; }

		public int PageCount()
		{
			if (Count != 0)
			{
				return (Count - 1) / 10000 + 1;
			}
			return 0;
		}

		public RevisionStorage(Sha[] shas, Sha[] parents, int[] parentsIndexes, bool hasMore, long timestamp)
		{
			Shas = shas;
			Parents = parents;
			ParentsIndexes = parentsIndexes;
			HasMore = hasMore;
			Timestamp = timestamp;
			Gen = (byte)_random.Next(256);
		}

		public HandleEnumerator GetEnumerator()
		{
			return new HandleEnumerator(0, Shas.Length, isStash: false, Gen);
		}

		public Handle? First()
		{
			if (Shas.Length != 0)
			{
				return new Handle(0, isStash: false, Gen);
			}
			return null;
		}

		public Sha GetSha(Handle handle)
		{
			return Shas[handle.Index];
		}

		public ShaBufferIterator GetParents(Handle handle)
		{
			int parentsCount = GetParentsCount(handle.Index);
			return new ShaBufferIterator(Parents, ParentsIndexes[handle.Index], ParentsIndexes[handle.Index] + parentsCount);
		}

		private int GetParentsCount(int index)
		{
			int num = ParentsIndexes[index];
			int num2 = ((index + 1 >= Shas.Length) ? Parents.Length : ParentsIndexes[index + 1]);
			return num2 - num;
		}

		public GitCommandResult<RevisionStorage> Extend(RevisionStorage additionalRevisionStorage)
		{
			List<Sha> list = new List<Sha>(Shas.Length + additionalRevisionStorage.Shas.Length);
			List<Sha> list2 = new List<Sha>(Parents.Length + additionalRevisionStorage.Parents.Length);
			List<int> list3 = new List<int>(ParentsIndexes.Length + additionalRevisionStorage.ParentsIndexes.Length);
			HandleEnumerator enumerator = GetEnumerator();
			while (enumerator.MoveNext())
			{
				Handle current = enumerator.Current;
				list.Add(GetSha(current));
				list3.Add(list2.Count);
				ShaBufferIterator parents = GetParents(current);
				Sha[] items = parents.Items;
				for (int i = parents.Start; i < parents.End; i++)
				{
					list2.Add(items[i]);
				}
			}
			enumerator = additionalRevisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				Handle current2 = enumerator.Current;
				list.Add(additionalRevisionStorage.GetSha(current2));
				list3.Add(list2.Count);
				ShaBufferIterator parents2 = additionalRevisionStorage.GetParents(current2);
				Sha[] items2 = parents2.Items;
				for (int j = parents2.Start; j < parents2.End; j++)
				{
					list2.Add(items2[j]);
				}
			}
			RevisionStorage extendedRevisionStorage = new RevisionStorage(list.ToArray(), list2.ToArray(), list3.ToArray(), additionalRevisionStorage.HasMore, Timestamp);
			return Validate(this, extendedRevisionStorage);
		}

		private static GitCommandResult<RevisionStorage> Validate(RevisionStorage revisionStorage, RevisionStorage extendedRevisionStorage)
		{
			if (!extendedRevisionStorage.HasMore)
			{
				_ = revisionStorage.Count;
				_ = extendedRevisionStorage.Count;
			}
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			HandleEnumerator enumerator2 = extendedRevisionStorage.GetEnumerator();
			while (enumerator.MoveNext() && enumerator2.MoveNext())
			{
				revisionStorage.GetSha(enumerator.Current);
				extendedRevisionStorage.GetSha(enumerator2.Current);
			}
			return GitCommandResult<RevisionStorage>.Success(extendedRevisionStorage);
		}
	}
}
