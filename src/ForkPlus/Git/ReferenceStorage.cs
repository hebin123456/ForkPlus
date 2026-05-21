using System;
using System.Collections.Generic;

namespace ForkPlus.Git
{
	public class ReferenceStorage
	{
		public struct UpstreamTrackingReference
		{
			public string LocalBranch { get; }

			public string TargetRef { get; }

			public UpstreamTrackingReference(string localBranch, string targetRef)
			{
				LocalBranch = localBranch;
				TargetRef = targetRef;
			}

			public override int GetHashCode()
			{
				return LocalBranch.GetHashCode() + TargetRef.GetHashCode() * 31;
			}
		}

		public static readonly ReferenceStorage Empty = new ReferenceStorage(new string[0], new Sha[0], 0uL, new DateTime[0], new string[0], new string[0], new Range(0, 0), new Range(0, 0), new Range(0, 0), new string[0], 0, null, null);

		public string[] Refs { get; }

		public Sha[] Shas { get; }

		public ulong RefsHash { get; }

		public DateTime[] CommitterDates { get; }

		public string[] Symrefs { get; }

		public string[] SymrefTargets { get; }

		public Range LocalBranches { get; }

		public Range RemoteBranches { get; }

		public Range Tags { get; }

		private string[] Upstreams { get; }

		public int UpstreamsHash { get; }

		public Sha? HeadSha { get; }

		public int? ActiveBranchIndex { get; }

		public ReferenceStorage(string[] refs, Sha[] shas, ulong refsHash, DateTime[] committerDates, string[] symrefs, string[] symrefTargets, Range localBranches, Range remoteBranches, Range tags, string[] upstreams, int upstreamsHash, Sha? headSha, int? activeBranchIndex)
		{
			Refs = refs;
			Shas = shas;
			RefsHash = refsHash;
			CommitterDates = committerDates;
			Symrefs = symrefs;
			SymrefTargets = symrefTargets;
			LocalBranches = localBranches;
			RemoteBranches = remoteBranches;
			Tags = tags;
			Upstreams = upstreams;
			UpstreamsHash = upstreamsHash;
			HeadSha = headSha;
			ActiveBranchIndex = activeBranchIndex;
		}

		public static ReferenceStorage New(string[] refs, Sha[] shas, ulong refsHash, DateTime[] committerDates, string[] symrefs, string[] symrefTargets, UpstreamTrackingReference[] upstreamPairs, int upstreamsHash)
		{
			string text = null;
			for (int i = 0; i < symrefs.Length; i++)
			{
				if (symrefs[i] == "HEAD")
				{
					text = symrefTargets[i];
					break;
				}
			}
			int? num = null;
			int num2 = 0;
			List<string> list = new List<string>(upstreamPairs.Length);
			int? num3 = null;
			int num4 = 0;
			int? num5 = null;
			int num6 = 0;
			int? activeBranchIndex = null;
			int num7 = 0;
			for (int j = 0; j < refs.Length; j++)
			{
				string text2 = refs[j];
				if (text2.StartsWith("refs/heads/"))
				{
					if (!num.HasValue)
					{
						num = j;
					}
					num2++;
					if (text != null && string.Compare(text, text2, StringComparison.OrdinalIgnoreCase) == 0)
					{
						activeBranchIndex = j;
					}
					while (true)
					{
						if (num7 < upstreamPairs.Length)
						{
							UpstreamTrackingReference upstreamTrackingReference = upstreamPairs[num7];
							int num8 = string.CompareOrdinal(text2, upstreamTrackingReference.LocalBranch);
							if (num8 == 0)
							{
								list.Add(upstreamTrackingReference.TargetRef);
								num7++;
								break;
							}
							if (num8 > 0)
							{
								num7++;
								continue;
							}
							list.Add(null);
							break;
						}
						list.Add(null);
						break;
					}
				}
				else if (text2.StartsWith("refs/remotes/"))
				{
					if (!num3.HasValue)
					{
						num3 = j;
					}
					num4++;
				}
				else if (text2.StartsWith("refs/tags/"))
				{
					if (!num5.HasValue)
					{
						num5 = j;
					}
					num6++;
				}
			}
			num = num.GetValueOrDefault();
			Range localBranches = new Range(num.Value, num.Value + num2);
			num3 = num3.GetValueOrDefault();
			Range remoteBranches = new Range(num3.Value, num3.Value + num4);
			num5 = num5.GetValueOrDefault();
			Range tags = new Range(num5.Value, num5.Value + num6);
			Sha? headSha = null;
			if (activeBranchIndex.HasValue)
			{
				headSha = shas[activeBranchIndex.Value];
			}
			else
			{
				int num9 = Array.IndexOf(refs, "HEAD");
				if (num9 >= 0)
				{
					headSha = shas[num9];
				}
			}
			string[] upstreams = list.ToArray();
			return new ReferenceStorage(refs, shas, refsHash, committerDates, symrefs, symrefTargets, localBranches, remoteBranches, tags, upstreams, upstreamsHash, headSha, activeBranchIndex);
		}

		public ReferenceStorage WithHead(Sha? headSha)
		{
			List<string> list = new List<string>(Symrefs);
			List<string> list2 = new List<string>(SymrefTargets);
			int num = Array.IndexOf(Symrefs, "HEAD");
			if (num >= 0)
			{
				list.RemoveAt(num);
				list2.RemoveAt(num);
			}
			List<string> list3 = new List<string>(Refs);
			List<Sha> list4 = new List<Sha>(Shas);
			List<DateTime> list5 = new List<DateTime>(CommitterDates);
			Range localBranches = LocalBranches;
			Range remoteBranches = RemoteBranches;
			Range tags = Tags;
			int num2 = Array.IndexOf(Refs, "HEAD");
			if (num2 >= 0)
			{
				if (headSha.HasValue)
				{
					list4[num2] = headSha.Value;
				}
				else
				{
					list3.RemoveAt(num2);
					list4.RemoveAt(num2);
					if (list5.Count > 0)
					{
						list5.RemoveAt(num2);
					}
					localBranches = new Range(LocalBranches.Start - 1, LocalBranches.End - 1);
					remoteBranches = new Range(RemoteBranches.Start - 1, RemoteBranches.End - 1);
					tags = new Range(Tags.Start - 1, Tags.End - 1);
				}
			}
			else if (headSha.HasValue)
			{
				list3.Insert(0, "HEAD");
				list4.Insert(0, headSha.Value);
				if (list5.Count > 0)
				{
					list5.Insert(0, DateTimeHelper.UnixStartTime);
				}
				localBranches = new Range(LocalBranches.Start + 1, LocalBranches.End + 1);
				remoteBranches = new Range(RemoteBranches.Start + 1, RemoteBranches.End + 1);
				tags = new Range(Tags.Start + 1, Tags.End + 1);
			}
			return new ReferenceStorage(list3.ToArray(), list4.ToArray(), RefsHash, list5.ToArray(), list.ToArray(), list2.ToArray(), localBranches, remoteBranches, tags, Upstreams, UpstreamsHash, headSha, null);
		}

		public string GetLocalBranchUpstream(int refIndex)
		{
			return Upstreams[refIndex - LocalBranches.Start];
		}

		public string GetLocalBranchName(int refIndex)
		{
			return Refs[refIndex].Substring("refs/heads/".Length);
		}

		public string GetRemoteBranchName(int refIndex)
		{
			return Refs[refIndex].Substring("refs/remotes/".Length);
		}

		public string GetTagName(int refIndex)
		{
			return Refs[refIndex].Substring("refs/tags/".Length);
		}

		public DateTime? GetCommitterDate(int refIndex)
		{
			if (CommitterDates.Length == 0)
			{
				return null;
			}
			return CommitterDates[refIndex];
		}

		public IReadOnlyList<LocalBranch> CreateLocalBranches()
		{
			List<LocalBranch> list = new List<LocalBranch>(LocalBranches.Length);
			for (int i = LocalBranches.Start; i < LocalBranches.End; i++)
			{
				string text = Refs[i];
				string fullName = text.Substring("refs/heads/".Length);
				Sha sha = Shas[i];
				bool isActive = ActiveBranchIndex == i;
				string localBranchUpstream = GetLocalBranchUpstream(i);
				DateTime committerDate = GetCommitterDate(i) ?? DateTimeHelper.UnixStartTime;
				list.Add(new LocalBranch(sha, text, fullName, isActive, localBranchUpstream, committerDate));
			}
			return list;
		}

		public IReadOnlyList<RemoteBranch> CreateRemoteBranches()
		{
			List<RemoteBranch> list = new List<RemoteBranch>(RemoteBranches.Length);
			for (int i = RemoteBranches.Start; i < RemoteBranches.End; i++)
			{
				string text = Refs[i];
				string text2 = text.Substring("refs/remotes/".Length);
				Sha sha = Shas[i];
				DateTime committerDate = GetCommitterDate(i) ?? DateTimeHelper.UnixStartTime;
				int num = text2.IndexOf('/');
				if (num != -1 && num + 1 < text2.Length)
				{
					string remote = text2.Substring(0, num);
					string text3 = text2.Substring(num + 1);
					if (!(text3 == "HEAD"))
					{
						list.Add(new RemoteBranch(sha, text, text2, text3, remote, committerDate));
					}
				}
			}
			return list;
		}
	}
}
