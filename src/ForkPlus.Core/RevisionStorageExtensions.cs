using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus
{
	internal static class RevisionStorageExtensions
	{
		[Null]
		public static Revision GetParentRevision(this RevisionStorage revisionStorage, GitModule gitModule, Sha sha)
		{
			RevisionStorage.Handle? handle = revisionStorage.FindRevision(sha);
			if (handle.HasValue)
			{
				RevisionStorage.Handle valueOrDefault = handle.GetValueOrDefault();
				Sha? sha2 = FirstSha(revisionStorage.GetParents(valueOrDefault));
				if (sha2.HasValue)
				{
					Sha valueOrDefault2 = sha2.GetValueOrDefault();
					handle = revisionStorage.FindRevision(valueOrDefault2);
					if (handle.HasValue)
					{
						RevisionStorage.Handle valueOrDefault3 = handle.GetValueOrDefault();
						Sha sha3 = revisionStorage.GetSha(valueOrDefault3);
						GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, new Sha[1] { sha3 });
						if (!gitCommandResult.Succeeded || gitCommandResult.Result.Length != 1)
						{
							return null;
						}
						return gitCommandResult.Result[0];
					}
					return null;
				}
				return null;
			}
			return null;
		}

		public static bool RevisionRangeContainsSha(this RevisionStorage revisionStorage, Sha start, Sha end, Sha targetSha)
		{
			if (targetSha == end)
			{
				return true;
			}
			HashSet<Sha> hashSet = new HashSet<Sha>();
			hashSet.Add(start);
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				Sha sha = revisionStorage.GetSha(current);
				ShaBufferIterator parents = revisionStorage.GetParents(current);
				if (hashSet.Contains(sha))
				{
					if (sha == targetSha)
					{
						return true;
					}
					hashSet.Remove(sha);
					ShaBufferIterator.Enumerator enumerator2 = parents.GetEnumerator();
					while (enumerator2.MoveNext())
					{
						Sha current2 = enumerator2.Current;
						hashSet.Add(current2);
					}
				}
				if (sha == end)
				{
					return false;
				}
			}
			return false;
		}

		private static RevisionStorage.Handle? FindRevision(this RevisionStorage revisionStorage, Sha sha)
		{
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				if (revisionStorage.GetSha(current) == sha)
				{
					return current;
				}
			}
			return null;
		}

		private static Sha? FirstSha(ShaBufferIterator shaIterator)
		{
			if (shaIterator.Length == 0)
			{
				return null;
			}
			return shaIterator.Items[shaIterator.Start];
		}
	}
}
