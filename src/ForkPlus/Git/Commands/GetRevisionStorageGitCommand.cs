using System;
using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionStorageGitCommand
	{
		public GitCommandResult<RevisionStorage> Execute(GitModule gitModule, ReferenceStorage references, bool topoOrder, bool reflog, int skipPages, int minPagesCount, IReadOnlyList<Sha> requiredShas, long timestamp, CommitGraphCache commitGraphCache, JobMonitor monitor)
	{
		// 空仓库快速路径：刚 git init 完毕的仓库没有 commits/branches/tags，HEAD 是 symref
		// 指向不存在的 refs/heads/master。此时 btTipsArray 为空数组，bt_get_commits 对空 tips
		// 的处理行为不确定（可能死循环或永久阻塞），导致 UI 一直转圈无法读取仓库。
		// 直接返回空 RevisionStorage 跳过原生调用，git status 仍能正常显示 untracked 文件。
		if (references.LocalBranches.Length == 0
			&& references.RemoteBranches.Length == 0
			&& references.Tags.Length == 0
			&& !references.HeadSha.HasValue
			&& requiredShas.Count == 0)
		{
			return GitCommandResult<RevisionStorage>.Success(new RevisionStorage(new Sha[0], new Sha[0], new int[0], false, timestamp));
		}
		Benchmarker benchmarker = new Benchmarker("bt_get_commits");
			string path = gitModule.GitDir();
			List<BtOid> list = new List<BtOid>(references.LocalBranches.Length + references.RemoteBranches.Length + references.Tags.Length + 1);
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
			for (int k = references.Tags.Start; k < references.Tags.End; k++)
			{
				list.Add(references.Shas[k].ToBtOid());
			}
			Sha? headSha = references.HeadSha;
			if (headSha.HasValue)
			{
				Sha valueOrDefault = headSha.GetValueOrDefault();
				list.Add(valueOrDefault.ToBtOid());
			}
			if (reflog)
			{
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("reflog", "--all", "--format=%H").ExecuteBt();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult<RevisionStorage>.Failure(gitRequestResult.ToGitCommandError());
				}
				string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
				for (int l = 0; l < array.Length; l++)
				{
					BtOid? btOid = Sha.Parse(array[l])?.ToBtOid();
					if (btOid.HasValue)
					{
						BtOid valueOrDefault2 = btOid.GetValueOrDefault();
						list.Add(valueOrDefault2);
					}
				}
			}
			BtOid[] btTipsArray = list.ToArray();
			BtOid[] btRequiredOids = requiredShas.Map((Sha x) => x.ToBtOid());
			BtCommitGraphCache commitGraphCacheHandle = commitGraphCache.Handle;
			bool dateOrder = !topoOrder;
			int revisionPageSize = 10000;
			_ = commitGraphCache.Handle;
			BtCancellationToken btCancellationToken = Bt.bt_new_cancellation_token();
			monitor.SetCancellationAction(delegate
			{
				Bt.bt_cancel_cancellation_token(ref btCancellationToken);
			});
			GitCommandResult<RevisionStorage> result = BtRequest.Run(() => default(BtCommitStorage), delegate(ref BtCommitStorage x)
			{
				return Bt.bt_get_commits(path, btTipsArray, btTipsArray.Length, dateOrder, revisionPageSize, skipPages, minPagesCount, btRequiredOids, btRequiredOids.Length, ref commitGraphCacheHandle, ref btCancellationToken, ref x);
			}, delegate(ref BtCommitStorage x)
			{
				return IntoRevisionStorage(ref x, timestamp);
			}, delegate(ref BtCommitStorage x)
			{
				Bt.bt_release_commit_storage(ref x);
			});
			monitor.SetCancellationAction(null);
			Bt.bt_release_cancellation_token(ref btCancellationToken);
			benchmarker.ReportElapsed();
			return result;
		}

		public GitCommandResult<RevisionStorage> FetchUntil(GitModule gitModule, RevisionStorage revisionStorage, ReferenceStorage references, bool topoOrder, bool reflog, Sha[] requiredShas, CommitGraphCache commitGraphCache, JobMonitor monitor)
		{
			if (!revisionStorage.HasMore)
			{
				return GitCommandResult<RevisionStorage>.Failure(new GitCommandError.Bug("fetch exhausted revisions"));
			}
			GitCommandResult<RevisionStorage> gitCommandResult = Execute(gitModule, references, topoOrder, reflog, revisionStorage.PageCount(), 1, requiredShas, revisionStorage.Timestamp, commitGraphCache, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			return revisionStorage.Extend(gitCommandResult.Result);
		}

		public GitCommandResult<RevisionStorage> FetchNextPage(GitModule gitModule, RevisionStorage revisionStorage, ReferenceStorage references, bool topoOrder, bool reflog, CommitGraphCache commitGraphCache, JobMonitor monitor)
		{
			if (!revisionStorage.HasMore)
			{
				return GitCommandResult<RevisionStorage>.Failure(new GitCommandError.Bug("fetch exhausted revisions"));
			}
			GitCommandResult<RevisionStorage> gitCommandResult = Execute(gitModule, references, topoOrder, reflog, revisionStorage.PageCount(), 1, new Sha[0], revisionStorage.Timestamp, commitGraphCache, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			return revisionStorage.Extend(gitCommandResult.Result);
		}

		public GitCommandResult<RevisionStorage> Execute(GitModule gitModule, Sha sha)
		{
			string path = gitModule.GitDir();
			BtOid btOid = sha.ToBtOid();
			long timestamp = DateTime.UtcNow.MillisecondsSince1970();
			using CommitGraphCache commitGraphCache = new CommitGraphCache(gitModule);
			BtCommitGraphCache btCommitGraphCache = commitGraphCache.Handle;
			return BtRequest.Run(() => default(BtCommitStorage), delegate(ref BtCommitStorage x)
			{
				return Bt.bt_get_commit_subgraph(path, ref btOid, ref btCommitGraphCache, ref x);
			}, delegate(ref BtCommitStorage x)
			{
				return IntoRevisionStorage(ref x, timestamp);
			}, delegate(ref BtCommitStorage x)
			{
				Bt.bt_release_commit_storage(ref x);
			});
		}

		public GitCommandResult<RevisionStorage> Execute(GitModule gitModule, CommitGraphCache commitGraphCache, Sha src, Sha dst)
		{
			string path = gitModule.GitDir();
			BtOid srcBtOid = src.ToBtOid();
			BtOid dstBtOid = dst.ToBtOid();
			BtCommitGraphCache btCommitGraphCache = commitGraphCache.Handle;
			long timestamp = DateTime.UtcNow.MillisecondsSince1970();
			return BtRequest.Run(() => default(BtCommitStorage), delegate(ref BtCommitStorage x)
			{
				return Bt.bt_get_commit_subgraph_2(path, ref srcBtOid, ref dstBtOid, ref btCommitGraphCache, ref x);
			}, delegate(ref BtCommitStorage x)
			{
				return IntoRevisionStorage(ref x, timestamp);
			}, delegate(ref BtCommitStorage x)
			{
				Bt.bt_release_commit_storage(ref x);
			});
		}

		private static GitCommandResult<RevisionStorage> IntoRevisionStorage(ref BtCommitStorage btCommitStorage, long timestamp)
		{
			Sha[] structArray = btCommitStorage.oids.GetStructArray(btCommitStorage.oids_len, (BtOid btOid) => btOid.ToSha());
			uint[] uInt32Array = btCommitStorage.indexes.GetUInt32Array(btCommitStorage.indexes_len);
			bool hasMore = btCommitStorage.has_more != 0;
			List<Sha> list = new List<Sha>(uInt32Array.Length);
			List<Sha> list2 = new List<Sha>((int)((double)list.Count * 1.2));
			List<int> list3 = new List<int>(uInt32Array.Length);
			for (int i = 0; i < uInt32Array.Length; i++)
			{
				list.Add(structArray[uInt32Array[i]]);
				uint num = uInt32Array[i] + 1;
				uint num2 = ((i == uInt32Array.Length - 1) ? ((uint)structArray.Length) : uInt32Array[i + 1]);
				list3.Add(list2.Count);
				for (uint num3 = num; num3 < num2; num3++)
				{
					list2.Add(structArray[num3]);
				}
			}
			return GitCommandResult<RevisionStorage>.Success(new RevisionStorage(list.ToArray(), list2.ToArray(), list3.ToArray(), hasMore, timestamp));
		}
	}
}
