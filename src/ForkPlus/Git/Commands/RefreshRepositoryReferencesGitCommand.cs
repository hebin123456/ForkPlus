using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RefreshRepositoryReferencesGitCommand
	{
		public GitCommandResult<RepositoryReferences> Execute(GitModule gitModule, GitConfig gitConfig, [Null] RepositoryReferences oldRepositoryReferences, SubdomainsToReload subdomainsToReload, CommitGraphCache commitGraphCache)
		{
			bool hideTags = gitModule.Settings.HideTags;
			GitCommandResult<ReferenceStorage> gitCommandResult = RefreshReferences(gitModule, gitConfig, hideTags, oldRepositoryReferences?.ReferenceStorage, commitGraphCache);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryReferences>.Failure(gitCommandResult.Error);
			}
			ReferenceStorage result = gitCommandResult.Result;
			if (oldRepositoryReferences != null && IsInFilterActiveBranchMode(oldRepositoryReferences))
			{
				int? activeBranchIndex = result.ActiveBranchIndex;
				if (activeBranchIndex.HasValue)
				{
					int valueOrDefault = activeBranchIndex.GetValueOrDefault();
					activeBranchIndex = oldRepositoryReferences.ReferenceStorage.ActiveBranchIndex;
					if (activeBranchIndex.HasValue)
					{
						int valueOrDefault2 = activeBranchIndex.GetValueOrDefault();
						if (oldRepositoryReferences.ReferenceStorage.Refs[valueOrDefault2] != result.Refs[valueOrDefault])
						{
							string text = result.Refs[valueOrDefault];
							string fullName = text.Substring("refs/heads/".Length);
							LocalBranch localBranch = new LocalBranch(Sha.Zero, text, fullName, isActive: false, result.GetLocalBranchUpstream(valueOrDefault), DateTimeHelper.UnixStartTime);
							gitModule.Settings.FilterReferences = CreateLocalBranchFilters(localBranch);
							gitModule.Settings.Save();
							subdomainsToReload.SubDomain |= SubDomain.ReferenceSettings;
						}
					}
				}
			}
			string[] filterReferences = gitModule.Settings.FilterReferences;
			string[] hiddenReferences = gitModule.Settings.HiddenReferences;
			string[] pinnedReferences = gitModule.Settings.PinnedReferences;
			RepositoryReferences repositoryReferences = RepositoryReferences.New(result, filterReferences, hiddenReferences, pinnedReferences, hideTags);
			if (oldRepositoryReferences != null && result == oldRepositoryReferences.ReferenceStorage && Equals(oldRepositoryReferences.FilterReferences, filterReferences) && Equals(oldRepositoryReferences.HiddenReferences, hiddenReferences) && Equals(oldRepositoryReferences.PinnedReferences, pinnedReferences))
			{
				return GitCommandResult<RepositoryReferences>.Success(oldRepositoryReferences);
			}
			if (subdomainsToReload.Contains(SubDomain.RevisionsSlim))
			{
				subdomainsToReload.SubDomain |= SubDomain.Revisions;
			}
			return GitCommandResult<RepositoryReferences>.Success(repositoryReferences);
		}

		private static GitCommandResult ReferencesAreEqual(RepositoryReferences gitRefs, RepositoryReferences btRefs)
		{
			if (!(gitRefs.HeadSha == btRefs.HeadSha))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("head " + (gitRefs.HeadSha?.ToString() ?? "nil") + " != " + (btRefs.HeadSha?.ToString() ?? "nil")));
			}
			List<string> list = gitRefs.ReferenceStorage.Symrefs.Filter((string s) => !s.StartsWith("refs/remotes/"));
			List<string> list2 = btRefs.ReferenceStorage.Symrefs.Filter((string s) => !s.StartsWith("refs/remotes/"));
			if (!SymrefsAreEqual(list, list2))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("symrefs " + string.Join(",", list) + " != " + string.Join(",", list2)));
			}
			for (int i = 0; i < Math.Min(gitRefs.Items.Length, btRefs.Items.Length); i++)
			{
				GitCommandResult gitCommandResult = AreEqual(gitRefs.Items[i], btRefs.Items[i]);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult;
				}
			}
			if (gitRefs.Items.Length != btRefs.Items.Length)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug($"different items count {gitRefs.Items.Length} != {btRefs.Items.Length}"));
			}
			if (!(gitRefs.ActiveBranch?.Sha == btRefs.ActiveBranch?.Sha))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("active branch sha " + (gitRefs.ActiveBranch?.Sha.ToString() ?? "nil") + " != " + (btRefs.ActiveBranch?.Sha.ToString() ?? "nil")));
			}
			return GitCommandResult.Success();
		}

		private static bool SymrefsAreEqual(IReadOnlyList<string> lhs, IReadOnlyList<string> rhs)
		{
			if (lhs.Count != rhs.Count)
			{
				return false;
			}
			for (int i = 0; i < lhs.Count; i++)
			{
				if (lhs[i] != rhs[i])
				{
					return false;
				}
			}
			return true;
		}

		private static GitCommandResult AreEqual(Reference gitRef, Reference btRef)
		{
			if (!(gitRef.FullReference == btRef.FullReference))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid refs: " + gitRef.FullReference + " != " + btRef.FullReference));
			}
			if (!(gitRef is Tag) || !(btRef is Tag) || !(gitRef.CommitterDate == DateTimeHelper.UnixStartTime))
			{
				if (!(gitRef.CommitterDate == btRef.CommitterDate))
				{
					return GitCommandResult.Failure(new GitCommandError.Bug("Invalid ref dates: " + gitRef.FullReference + " " + gitRef.CommitterDate.ToString() + " != " + btRef.FullReference + " " + btRef.CommitterDate.ToString()));
				}
				if (!(gitRef.Sha == btRef.Sha))
				{
					return GitCommandResult.Failure(new GitCommandError.Bug("Invalid ref shas: " + gitRef.FullReference + " " + gitRef.Sha.ToString() + " != " + btRef.FullReference + " " + btRef.Sha));
				}
			}
			if (gitRef is LocalBranch localBranch && btRef is LocalBranch localBranch2 && (localBranch.IsActive != localBranch2.IsActive || !(localBranch.FullReference == localBranch2.FullReference)))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug(string.Format("Invalid local branches: {0} {1} {2} != {3} {4} {5}", gitRef.FullReference, localBranch.IsActive, localBranch.UpstreamFullReference ?? "-", btRef.FullReference, localBranch2.IsActive, localBranch2.UpstreamFullReference ?? "-")));
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult<RepositoryReferences> ExecuteOld(GitModule gitModule, GitConfig gitConfig, [Null] RepositoryReferences oldRepositoryReferences, SubdomainsToReload subdomainsToReload)
		{
			ReferenceStorage referenceStorage;
			if (oldRepositoryReferences != null && !subdomainsToReload.Contains(SubDomain.References))
			{
				referenceStorage = oldRepositoryReferences.ReferenceStorage;
			}
			else
			{
				GitCommandResult<ReferenceStorage> gitCommandResult = new GetReferencesGitCommand().ExecuteOld(gitModule, gitModule.Settings.HideTags);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<RepositoryReferences>.Failure(gitCommandResult.Error);
				}
				referenceStorage = gitCommandResult.Result;
			}
			string[] filterReferences = gitModule.Settings.FilterReferences;
			string[] hiddenReferences = gitModule.Settings.HiddenReferences;
			string[] pinnedReferences = gitModule.Settings.PinnedReferences;
			bool hideTags = gitModule.Settings.HideTags;
			if (oldRepositoryReferences != null && referenceStorage == oldRepositoryReferences.ReferenceStorage && Equals(oldRepositoryReferences.FilterReferences, filterReferences) && Equals(oldRepositoryReferences.HiddenReferences, hiddenReferences) && Equals(oldRepositoryReferences.PinnedReferences, pinnedReferences))
			{
				return GitCommandResult<RepositoryReferences>.Success(oldRepositoryReferences);
			}
			return GitCommandResult<RepositoryReferences>.Success(RepositoryReferences.New(referenceStorage, filterReferences, hiddenReferences, pinnedReferences, hideTags));
		}

		private static GitCommandResult<ReferenceStorage> RefreshReferences(GitModule gitModule, GitConfig gitConfig, bool skipTags, [Null] ReferenceStorage oldReferences, CommitGraphCache commitGraphCache)
		{
			// 空仓库快速路径（v2.1.4 修复）：刚 git init 完毕的仓库没有任何 commit，HEAD 是 symref
			// 指向不存在的 refs/heads/master。此时调用 native biturbo 的行为不确定——
			// bt_get_references 可能返回含 Sha.Zero 的 HEAD ref，bt_get_committer_times 传入
			// 无效 sha 可能永久阻塞，bt_get_commits 对空 tips 数组也可能死循环。
			// v2.1.2 曾在 GetRevisionStorageGitCommand.Execute 加快速路径，但失败发生在更早的
			// RefreshReferences 阶段（这里），根本走不到那段。v2.1.4 把快速路径前移到这里，
			// 在任何 native biturbo 调用之前就检测空仓库并直接返回空 ReferenceStorage。
			//
			// 检测方式：git rev-parse --verify HEAD 失败说明仓库没有任何 commit（git init 完毕状态）。
			// 这是 100% 准确的判定，开销是一次 git 子进程调用（仅首次加载时，可接受）。
			if (IsEmptyRepository(gitModule))
			{
				Log.Info("Empty repository detected (no commits yet), skipping native biturbo calls");
				ReferenceStorage.UpstreamTrackingReference[] emptyUpstreams = gitConfig.ReadUpstreams();
				return GitCommandResult<ReferenceStorage>.Success(ReferenceStorage.New(
					new string[0], new Sha[0], 0UL, new DateTime[0],
					new string[0], new string[0], emptyUpstreams, HashHelper.GetHashCode(emptyUpstreams)));
			}
			ReferenceStorage.UpstreamTrackingReference[] array = gitConfig.ReadUpstreams();
			int hashCode = HashHelper.GetHashCode(array);
			Benchmarker benchmarker = new Benchmarker("bt_get_references");
			BtReferences out_result = default(BtReferences);
			BtResult btResult = Bt.bt_get_references(gitModule.GitDir(), skipTags, ref out_result);
			benchmarker.ReportElapsed();
			if (btResult != 0)
			{
				return GitCommandResult<ReferenceStorage>.Failure(btResult.ToGitCommandError());
			}
			string[] refs;
			Sha[] shas;
			string[] symrefTargets;
			string[] symrefs2;
			DateTime[] committerDates;
			if (oldReferences != null)
			{
				if (oldReferences.RefsHash == out_result.hash && oldReferences.UpstreamsHash == hashCode)
				{
					return GitCommandResult<ReferenceStorage>.Success(oldReferences);
				}
				if (oldReferences.RefsHash == out_result.hash)
				{
					refs = oldReferences.Refs;
					shas = oldReferences.Shas;
					string[] symrefs = oldReferences.Symrefs;
					symrefTargets = oldReferences.SymrefTargets;
					symrefs2 = symrefs;
					committerDates = oldReferences.CommitterDates;
				}
				else
				{
					GitCommandResult<(string[], Sha[])> refs2 = out_result.GetRefs();
					if (!refs2.Succeeded)
					{
						return GitCommandResult<ReferenceStorage>.Failure(refs2.Error);
					}
					(string[], Sha[]) result = refs2.Result;
					refs = result.Item1;
					shas = result.Item2;
					GitCommandResult<(string[], string[])> symrefs3 = out_result.GetSymrefs();
					if (!symrefs3.Succeeded)
					{
						return GitCommandResult<ReferenceStorage>.Failure(symrefs3.Error);
					}
					(string[], string[]) result2 = symrefs3.Result;
					symrefs2 = result2.Item1;
					symrefTargets = result2.Item2;
					GitCommandResult<DateTime[]> committerDates2 = GetCommitterDates(gitModule, shas, commitGraphCache);
					if (!committerDates2.Succeeded)
					{
						return GitCommandResult<ReferenceStorage>.Failure(committerDates2.Error);
					}
					committerDates = committerDates2.Result;
				}
			}
			else
			{
				GitCommandResult<(string[], Sha[])> refs3 = out_result.GetRefs();
				if (!refs3.Succeeded)
				{
					return GitCommandResult<ReferenceStorage>.Failure(refs3.Error);
				}
				(string[], Sha[]) result3 = refs3.Result;
				refs = result3.Item1;
				shas = result3.Item2;
				GitCommandResult<(string[], string[])> symrefs4 = out_result.GetSymrefs();
				if (!symrefs4.Succeeded)
				{
					return GitCommandResult<ReferenceStorage>.Failure(symrefs4.Error);
				}
				(string[], string[]) result4 = symrefs4.Result;
				symrefs2 = result4.Item1;
				symrefTargets = result4.Item2;
				GitCommandResult<DateTime[]> committerDates3 = GetCommitterDates(gitModule, shas, commitGraphCache);
				if (!committerDates3.Succeeded)
				{
					return GitCommandResult<ReferenceStorage>.Failure(committerDates3.Error);
				}
				committerDates = committerDates3.Result;
			}
			ulong hash = out_result.hash;
			Bt.bt_release_references(ref out_result);
			return GitCommandResult<ReferenceStorage>.Success(ReferenceStorage.New(refs, shas, hash, committerDates, symrefs2, symrefTargets, array, hashCode));
		}

		/// <summary>检测仓库是否为空（git init 完毕，没有任何 commit）。
		/// 用 git rev-parse --verify HEAD——失败即说明 HEAD 无法解析为有效 commit，
		/// 这是空仓库的 100% 准确判定（detached HEAD 但有 commit 时也能正确返回成功）。
		/// 开销是一次 git 子进程调用，仅首次加载时执行，可接受。</summary>
		private static bool IsEmptyRepository(GitModule gitModule)
		{
			try
			{
				// silent:true 避免空仓库时 stderr 输出 "fatal: Needed a single revision" 污染日志
				GitRequestResult result = new GitRequest(gitModule)
					.Command("rev-parse", "--verify", "HEAD")
					.Execute(silent: true);
				return !result.Success;
			}
			catch (Exception ex)
			{
				// 检测失败时不阻塞加载，让原流程继续走（保守策略：宁可尝试 native 也不要误判为空）
				Log.Warn("IsEmptyRepository check failed: " + ex.Message);
				return false;
			}
		}

		private static GitCommandResult<DateTime[]> GetCommitterDates(GitModule gitModule, Sha[] shas, CommitGraphCache commitGraphCache)
		{
			Benchmarker benchmarker = new Benchmarker("bt_get_committer_times");
			BtOid[] array = shas.Map((Sha x) => x.ToBtOid());
			BtCommitGraphCache commit_graph_cache_ptr = commitGraphCache.Handle;
			BtCommitterTimes out_result = default(BtCommitterTimes);
			BtResult btResult = Bt.bt_get_committer_times(gitModule.GitDir(), array, array.Length, ref commit_graph_cache_ptr, ref out_result);
			benchmarker.LogElapsed();
			if (btResult != 0)
			{
				return GitCommandResult<DateTime[]>.Failure(btResult.ToGitCommandError());
			}
			DateTime[] structArray = out_result.times.GetStructArray(out_result.times_len, (long unixTime) => DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime);
			Bt.bt_release_committer_times(ref out_result);
			return GitCommandResult<DateTime[]>.Success(structArray);
		}

		private static bool Equals(string[] current, string[] old)
		{
			if (current.Length != old.Length)
			{
				Log.Debug("Detected reference filter change");
				return false;
			}
			for (int i = 0; i < current.Length; i++)
			{
				if (!(current[i] == old[i]))
				{
					Log.Debug("Detected reference filter change");
					return false;
				}
			}
			return true;
		}

		private static string[] CreateLocalBranchFilters([Null] LocalBranch localBranch)
		{
			List<string> list = new List<string>(2);
			if (localBranch != null)
			{
				list.Add(localBranch.FullReference);
				string upstreamFullReference = localBranch.UpstreamFullReference;
				if (upstreamFullReference != null)
				{
					list.Add(upstreamFullReference);
				}
			}
			return list.ToArray();
		}

		private static bool IsInFilterActiveBranchMode(RepositoryReferences self)
		{
			int? activeBranchIndex = self.ReferenceStorage.ActiveBranchIndex;
			if (activeBranchIndex.HasValue)
			{
				int valueOrDefault = activeBranchIndex.GetValueOrDefault();
				string activeBranch = self.ReferenceStorage.Refs[valueOrDefault];
				if (self.FilterReferences.ContainsItem((string x) => x == activeBranch))
				{
					if (self.FilterReferences.Length == 1)
					{
						return true;
					}
					string upstream = self.ReferenceStorage.GetLocalBranchUpstream(valueOrDefault);
					if (upstream != null && self.FilterReferences.Length == 2 && self.FilterReferences.ContainsItem((string x) => x == upstream))
					{
						return true;
					}
				}
				return false;
			}
			return false;
		}

		private static DateTime? GetLastWriteTime(string path)
		{
			try
			{
				return File.GetLastWriteTime(path);
			}
			catch
			{
			}
			return null;
		}
	}
}
