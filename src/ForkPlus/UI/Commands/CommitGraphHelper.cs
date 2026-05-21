using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Commands
{
	public static class CommitGraphHelper
	{
		public static GitCommandResult<Sha?> FindBranchTip(GitModule gitModule, CommitGraphCache commitGraphCache, Sha head, ReferenceStorage references, Sha? shaToSelect)
		{
			if (shaToSelect.HasValue)
			{
				Sha valueOrDefault = shaToSelect.GetValueOrDefault();
				string gitDir = gitModule.GitDir();
				BtOid headOid = head.ToBtOid();
				BtOid btBaseOid = valueOrDefault.ToBtOid();
				List<BtOid> list = new List<BtOid>(references.LocalBranches.Length + references.RemoteBranches.Length + 1);
				list.Add(headOid);
				for (int i = references.LocalBranches.Start; i < references.LocalBranches.End; i++)
				{
					list.Add(references.Shas[i].ToBtOid());
				}
				for (int j = references.RemoteBranches.Start; j < references.RemoteBranches.End; j++)
				{
					if (!references.Refs[j].EndsWith("/HEAD"))
					{
						list.Add(references.Shas[j].ToBtOid());
					}
				}
				BtOid[] btTipsArray = list.ToArray();
				BtCommitGraphCache commitGraphCacheHandle = commitGraphCache.Handle;
				return BtRequest.Run(() => default(BtOid), delegate(ref BtOid x)
				{
					return Bt.bt_find_fartherest_tip(gitDir, ref headOid, btTipsArray, btTipsArray.Length, ref btBaseOid, ref commitGraphCacheHandle, ref x);
				}, delegate(ref BtOid x)
				{
					return GitCommandResult<Sha?>.Success(x.ToSha());
				}, delegate
				{
				});
			}
			return GitCommandResult<Sha?>.Success(null);
		}
	}
}
