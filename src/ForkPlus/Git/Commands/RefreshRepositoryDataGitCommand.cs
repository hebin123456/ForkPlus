using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ForkPlus.Biturbo;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Git.Commands
{
	internal sealed class RefreshRepositoryDataGitCommand
	{
		public GitCommandResult<RepositoryData> Execute(GitModule gitModule, bool showReflogInRevisionList, RepositoryData oldRepositoryData, IReadOnlyList<Sha> requiredShas, SubDomain invalidatedEntities, JobMonitor cancellationToken, CommitGraphCache commitGraphCache)
		{
			Log.Debug($"{gitModule.RepositoryName} RefreshRepositoryDataGitCommand {invalidatedEntities}");
			using (new Benchmarker("RefreshRepositoryDataGitCommand"))
			{
				SubdomainsToReload subdomainsToReload = new SubdomainsToReload(invalidatedEntities);
				RevisionSortOrder revisionSortOrder = ForkPlusSettings.Default.RevisionSortOrder;
				DateTime? lastWriteTime = GetLastWriteTime(gitModule.ConfigFilePath());
				GitConfig gitConfig;
				DateTime? gitConfigUpdateTime;
				if (oldRepositoryData != null)
				{
					if (lastWriteTime != oldRepositoryData.GitConfigUpdateTime)
					{
						GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
						if (!gitCommandResult.Succeeded)
						{
							return GitCommandResult<RepositoryData>.Failure(gitCommandResult.Error);
						}
						gitConfig = (gitCommandResult.Result.GitConfigEquals(oldRepositoryData.GitConfig) ? oldRepositoryData.GitConfig : gitCommandResult.Result);
						gitConfigUpdateTime = lastWriteTime;
					}
					else
					{
						gitConfig = oldRepositoryData.GitConfig;
						gitConfigUpdateTime = oldRepositoryData.GitConfigUpdateTime;
					}
				}
				else
				{
					GitCommandResult<GitConfig> gitCommandResult2 = new GetGitConfigGitCommand().Execute(gitModule);
					if (!gitCommandResult2.Succeeded)
					{
						return GitCommandResult<RepositoryData>.Failure(gitCommandResult2.Error);
					}
					gitConfig = gitCommandResult2.Result;
					gitConfigUpdateTime = lastWriteTime;
				}
				bool flag = !gitModule.Settings.HideStashesInRevisionList;
				bool collapseAllMergeRevisions = gitModule.Settings.CollapseAllMergeRevisions;
				CollapseState collapseState = oldRepositoryData?.CollapseState ?? new CollapseState(collapseAllMergeRevisions, new HashSet<Sha>());
				GitCommandResult<RepositoryReferences> gitCommandResult3 = new RefreshRepositoryReferencesGitCommand().Execute(gitModule, gitConfig, oldRepositoryData?.References, subdomainsToReload, commitGraphCache);
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult3.Error);
				}
				RepositoryReferences result = gitCommandResult3.Result;
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryData>.Failure(new GitCommandError.Cancelled());
				}
				Task<GitCommandResult<RevisionStorage>> task = RefreshRevisions(gitModule, result, revisionSortOrder, showReflogInRevisionList, oldRepositoryData?.RevisionStorage, requiredShas, subdomainsToReload, commitGraphCache, cancellationToken);
				task.Start();
				Task<GitCommandResult<RepositoryStashes>> task2 = RefreshStashes(gitModule, flag, oldRepositoryData, subdomainsToReload, cancellationToken);
				task2.Start();
				Task<GitCommandResult<RepositorySubmodules>> task3 = RefreshSubmodules(gitModule, oldRepositoryData?.Submodules, subdomainsToReload, cancellationToken);
				task3.Start();
				GitCommandResult<RepositoryWorktrees> gitCommandResult4 = RefreshWorktrees(gitModule, oldRepositoryData?.Worktrees, subdomainsToReload);
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryData>.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult<UserColors> gitCommandResult5 = RefreshUserColors(gitModule, oldRepositoryData, subdomainsToReload, cancellationToken);
				GitCommandResult<BugtrackerLinkDefinition[]> gitCommandResult6 = RefreshBugtrackers(gitModule, oldRepositoryData, subdomainsToReload, cancellationToken);
				GitCommandResult<CustomCommand[]> gitCommandResult7 = RefreshCustomCommands(gitModule, oldRepositoryData, subdomainsToReload, cancellationToken);
				GitCommandResult<GitFlowSettings> gitCommandResult8 = RefreshGitFlow(gitConfig, oldRepositoryData, gitConfigUpdateTime, subdomainsToReload, cancellationToken);
				GitCommandResult<(bool, DateTime?)> gitCommandResult9 = RefreshLfsSettings(gitModule, oldRepositoryData, cancellationToken);
				task3.Wait();
				task.Wait();
				task2.Wait();
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryData>.Failure(new GitCommandError.Cancelled());
				}
				if (!gitCommandResult6.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult6.Error);
				}
				BugtrackerLinkDefinition[] result2 = gitCommandResult6.Result;
				if (!gitCommandResult7.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult7.Error);
				}
				CustomCommand[] result3 = gitCommandResult7.Result;
				if (!gitCommandResult5.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult5.Error);
				}
				UserColors result4 = gitCommandResult5.Result;
				if (!gitCommandResult8.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult8.Error);
				}
				GitFlowSettings result5 = gitCommandResult8.Result;
				if (!gitCommandResult9.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult9.Error);
				}
				bool item = gitCommandResult9.Result.Item1;
				DateTime? item2 = gitCommandResult9.Result.Item2;
				GitCommandResult<RepositoryRemotes> gitCommandResult10 = new GetRemotesGitCommand().Execute(gitConfig);
				if (!gitCommandResult10.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult10.Error);
				}
				RepositoryRemotes repositoryRemotes = ((oldRepositoryData == null || !gitCommandResult10.Result.DataEquals(oldRepositoryData.Remotes)) ? gitCommandResult10.Result : oldRepositoryData.Remotes);
				if (!task3.Result.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(task3.Result.Error);
				}
				RepositorySubmodules result6 = task3.Result.Result;
				if (!task.Result.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(task.Result.Error);
				}
				RevisionStorage result7 = task.Result.Result;
				if (!task2.Result.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(task2.Result.Error);
				}
				RepositoryStashes result8 = task2.Result.Result;
				if (!gitCommandResult4.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(gitCommandResult4.Error);
				}
				RepositoryWorktrees result9 = gitCommandResult4.Result;
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryData>.Failure(new GitCommandError.Cancelled());
				}
				if (oldRepositoryData != null && gitConfig == oldRepositoryData.GitConfig && result == oldRepositoryData.References && revisionSortOrder == oldRepositoryData.SortOrder && showReflogInRevisionList == oldRepositoryData.Reflog && GitFlowSettingsAreEqual(result5, oldRepositoryData.GitFlowSettings) && item == oldRepositoryData.GitLfsInitialized && UserColors.AreEqual(result4, oldRepositoryData.UserColors) && BugtrackersAreEqual(result2, oldRepositoryData.Bugtrackers) && CustomCommandsAreEqual(result3, oldRepositoryData.CustomCommands) && SubmodulesAreEqual(result6, oldRepositoryData.Submodules) && result9 == oldRepositoryData.Worktrees && repositoryRemotes.DataEquals(oldRepositoryData.Remotes) && StashesAreEqual(result8, oldRepositoryData.Stashes) && flag == oldRepositoryData.ShowStashesInRevisionList)
				{
					return GitCommandResult<RepositoryData>.Success(oldRepositoryData);
				}
				GitCommandResult<UpstreamStatusCache> upstreamStatus = GetUpstreamStatus(gitModule, result, commitGraphCache);
				if (!upstreamStatus.Succeeded)
				{
					return GitCommandResult<RepositoryData>.Failure(upstreamStatus.Error);
				}
				UpstreamStatusCache upstreamStatus2 = ((oldRepositoryData == null || !oldRepositoryData.UpstreamStatus.DataEquals(upstreamStatus.Result)) ? upstreamStatus.Result : oldRepositoryData.UpstreamStatus);
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryData>.Failure(new GitCommandError.Cancelled());
				}
				return GitCommandResult<RepositoryData>.Success(new RepositoryData(gitConfig, gitConfigUpdateTime, result, result7, revisionSortOrder, showReflogInRevisionList, collapseState, upstreamStatus2, repositoryRemotes, result6, result8, flag, result9, result2, result3, result5, result4, item2, item));
			}
		}

		private static GitCommandResult<UpstreamStatusCache> GetUpstreamStatus(GitModule gitModule, RepositoryReferences references, CommitGraphCache commitGraphCache)
		{
			Benchmarker benchmarker = new Benchmarker("GetUpstreamStatus");
			Dictionary<Sha, ActiveBranchCommitStatus> activeBranchCommitsStatus = new Dictionary<Sha, ActiveBranchCommitStatus>();
			ComposeUpstreams(references, out var pairs, out var resultDictionary);
			List<string> list = new List<string>();
			List<BtOidPair> list2 = new List<BtOidPair>();
			foreach (KeyValuePair<LocalBranch, RemoteBranch> item in pairs)
			{
				if (item.Key.Sha == item.Value.Sha)
				{
					resultDictionary[item.Key.FullReference] = new UpstreamStatus(0, 0);
					continue;
				}
				list.Add(item.Key.FullReference);
				list2.Add(new BtOidPair
				{
					left = item.Key.Sha.ToBtOid(),
					right = item.Value.Sha.ToBtOid()
				});
			}
			BtOidPair[] array = list2.ToArray();
			BtCommitGraphCache commit_graph_cache_ptr = commitGraphCache.Handle;
			BtBehindAheadCounts out_result = default(BtBehindAheadCounts);
			BtResult btResult = Bt.bt_get_behind_ahead_counts(gitModule.GitDir(), array, array.Length, ref commit_graph_cache_ptr, ref out_result);
			if (btResult != 0)
			{
				return GitCommandResult<UpstreamStatusCache>.Failure(btResult.ToGitCommandError());
			}
			UpstreamStatus[] structArray = out_result.items.GetStructArray(out_result.items_len, (BtBehindAheadCount btBehindAheadCount) => new UpstreamStatus((int)btBehindAheadCount.right, (int)btBehindAheadCount.left));
			for (int i = 0; i < structArray.Length; i++)
			{
				resultDictionary[list[i]] = structArray[i];
			}
			Bt.bt_release_behind_ahead_counts(ref out_result);
			benchmarker.ReportElapsed();
			return GitCommandResult<UpstreamStatusCache>.Success(new UpstreamStatusCache(resultDictionary, activeBranchCommitsStatus));
		}

		private static void ComposeUpstreams(RepositoryReferences references, out List<KeyValuePair<LocalBranch, RemoteBranch>> pairs, out Dictionary<string, UpstreamStatus> resultDictionary)
		{
			List<LocalBranch> list = references.LocalBranches.Filter((LocalBranch x) => x.UpstreamFullReference != null);
			list.Sort((LocalBranch x, LocalBranch y) => x.UpstreamFullReference.CompareTo(y.UpstreamFullReference));
			RemoteBranch[] array = new RemoteBranch[references.RemoteBranches.Length];
			Array.Copy(references.RemoteBranches, array, references.RemoteBranches.Length);
			Array.Sort(array, (RemoteBranch x, RemoteBranch y) => x.FullReference.CompareTo(y.FullReference));
			pairs = new List<KeyValuePair<LocalBranch, RemoteBranch>>(list.Count);
			resultDictionary = new Dictionary<string, UpstreamStatus>(list.Count);
			int i = 0;
			int j = 0;
			for (; i < list.Count; i++)
			{
				LocalBranch localBranch = list[i];
				RemoteBranch remoteBranch = null;
				for (; j < array.Length; j++)
				{
					int num = array[j].FullReference.CompareTo(localBranch.UpstreamFullReference);
					if (num >= 0)
					{
						if (num == 0)
						{
							remoteBranch = array[j];
							pairs.Add(new KeyValuePair<LocalBranch, RemoteBranch>(localBranch, remoteBranch));
						}
						break;
					}
				}
				if (remoteBranch == null)
				{
					resultDictionary.Add(localBranch.FullReference, UpstreamStatus.Invalid);
				}
			}
		}

		private static GitCommandResult<CustomCommand[]> RefreshCustomCommands(GitModule gitModule, [Null] RepositoryData oldRepositoryData, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<CustomCommand[]>.Failure(new GitCommandError.Cancelled());
			}
			if (oldRepositoryData != null && !subdomainsToReload.Contains(SubDomain.CustomCommands))
			{
				return GitCommandResult<CustomCommand[]>.Success(oldRepositoryData.CustomCommands);
			}
			return GitCommandResult<CustomCommand[]>.Success(new GetCustomCommandsGitCommand().Execute(gitModule));
		}

		private static GitCommandResult<BugtrackerLinkDefinition[]> RefreshBugtrackers(GitModule gitModule, [Null] RepositoryData oldRepositoryData, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<BugtrackerLinkDefinition[]>.Failure(new GitCommandError.Cancelled());
			}
			if (oldRepositoryData != null && !subdomainsToReload.Contains(SubDomain.BugtrackerSettings))
			{
				return GitCommandResult<BugtrackerLinkDefinition[]>.Success(oldRepositoryData.Bugtrackers);
			}
			return GitCommandResult<BugtrackerLinkDefinition[]>.Success(new GetBugtrackerRulesGitCommand().Execute(gitModule));
		}

		private static GitCommandResult<UserColors> RefreshUserColors(GitModule gitModule, [Null] RepositoryData oldRepositoryData, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<UserColors>.Failure(new GitCommandError.Cancelled());
			}
			if (oldRepositoryData != null && !subdomainsToReload.Contains(SubDomain.UserColors))
			{
				return GitCommandResult<UserColors>.Success(oldRepositoryData.UserColors);
			}
			return GitCommandResult<UserColors>.Success(new UserColors(new GetUserColorsGitCommand().Execute(gitModule)));
		}

		private static GitCommandResult<(bool, DateTime?)> RefreshLfsSettings(GitModule gitModule, [Null] RepositoryData oldRepositoryData, JobMonitor cancellationToken)
		{
			DateTime? lastWriteTime = GetLastWriteTime(gitModule.HookPath(IsGitLfsInitializedGitCommand.LfsPrePushHook));
			if (oldRepositoryData != null && lastWriteTime == oldRepositoryData.GitLfsUpdateTime)
			{
				return GitCommandResult<(bool, DateTime?)>.Success((oldRepositoryData.GitLfsInitialized, oldRepositoryData.GitLfsUpdateTime));
			}
			return GitCommandResult<(bool, DateTime?)>.Success((new IsGitLfsInitializedGitCommand().Execute(gitModule), lastWriteTime));
		}

		private static GitCommandResult<GitFlowSettings> RefreshGitFlow(GitConfig gitConfig, [Null] RepositoryData oldRepositoryData, DateTime? gitConfigUpdateTime, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			if (cancellationToken.IsCanceled)
			{
				return GitCommandResult<GitFlowSettings>.Failure(new GitCommandError.Cancelled());
			}
			if (oldRepositoryData != null && (!subdomainsToReload.Contains(SubDomain.GitFlowSettings) || gitConfigUpdateTime == oldRepositoryData.GitConfigUpdateTime))
			{
				return GitCommandResult<GitFlowSettings>.Success(oldRepositoryData.GitFlowSettings);
			}
			return new GetGitFlowSettingsGitCommand().Execute(gitConfig);
		}

		private static Task<GitCommandResult<RepositorySubmodules>> RefreshSubmodules(GitModule gitModule, [Null] RepositorySubmodules oldSubmodules, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			return new Task<GitCommandResult<RepositorySubmodules>>(delegate
			{
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositorySubmodules>.Failure(new GitCommandError.Cancelled());
				}
				DateTime? lastWriteTime = GetLastWriteTime(gitModule.GitModulesFilePath);
				if (oldSubmodules != null && (!subdomainsToReload.Contains(SubDomain.Submodules) || lastWriteTime == oldSubmodules.UpdateTime))
				{
					return GitCommandResult<RepositorySubmodules>.Success(oldSubmodules);
				}
				GitCommandResult<Submodule[]> gitCommandResult = new GetSubmodulesGitCommand().Execute(gitModule);
				return (!gitCommandResult.Succeeded) ? GitCommandResult<RepositorySubmodules>.Failure(gitCommandResult.Error) : GitCommandResult<RepositorySubmodules>.Success(new RepositorySubmodules(gitCommandResult.Result, lastWriteTime));
			});
		}

		private static GitCommandResult<RepositoryWorktrees> RefreshWorktrees(GitModule gitModule, [Null] RepositoryWorktrees oldWorktrees, SubdomainsToReload subdomainsToReload)
		{
			if (oldWorktrees != null && !subdomainsToReload.Contains(SubDomain.Worktrees))
			{
				return GitCommandResult<RepositoryWorktrees>.Success(oldWorktrees);
			}
			GitCommandResult<RepositoryWorktrees> gitCommandResult = new GetWorktreesGitCommand().Execute(gitModule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<RepositoryWorktrees>.Failure(gitCommandResult.Error);
			}
			if (oldWorktrees != null && gitCommandResult.Result.DataEquals(oldWorktrees))
			{
				return GitCommandResult<RepositoryWorktrees>.Success(oldWorktrees);
			}
			RepositoryWorktrees result = gitCommandResult.Result;
			if (result.MainWorktree.HasValue || result.Items.Length != 0)
			{
				ForkPlusSettings.Default.ShowWorktrees = true;
			}
			return GitCommandResult<RepositoryWorktrees>.Success(result);
		}

		private static Task<GitCommandResult<RepositoryStashes>> RefreshStashes(GitModule gitModule, bool showStashesInRevisionList, [Null] RepositoryData oldRepositoryData, SubdomainsToReload subdomainsToReload, JobMonitor cancellationToken)
		{
			return new Task<GitCommandResult<RepositoryStashes>>(delegate
			{
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RepositoryStashes>.Failure(new GitCommandError.Cancelled());
				}
				DateTime? lastWriteTime = GetLastWriteTime(gitModule.StashFilePath());
				if (oldRepositoryData != null && (!subdomainsToReload.Contains(SubDomain.Stashes) || lastWriteTime == oldRepositoryData.Stashes.UpdateTime))
				{
					RepositoryStashes stashes = oldRepositoryData.Stashes;
					if (showStashesInRevisionList != oldRepositoryData.ShowStashesInRevisionList)
					{
						return GitCommandResult<RepositoryStashes>.Success(new RepositoryStashes(stashes.Items, stashes.UpdateTime));
					}
					return GitCommandResult<RepositoryStashes>.Success(stashes);
				}
				GitCommandResult<StashRevision[]> gitCommandResult = new GetStashesGitCommand().Execute(gitModule);
				if (!gitCommandResult.Succeeded)
				{
					Log.Error(gitCommandResult.Error.FriendlyDescription);
					return GitCommandResult<RepositoryStashes>.Success(new RepositoryStashes(new StashRevision[0], lastWriteTime));
				}
				return GitCommandResult<RepositoryStashes>.Success(new RepositoryStashes(gitCommandResult.Result, lastWriteTime));
			});
		}

		private static Task<GitCommandResult<RevisionStorage>> RefreshRevisions(GitModule gitModule, RepositoryReferences references, RevisionSortOrder sortOrder, bool reflog, [Null] RevisionStorage oldRevisions, IReadOnlyList<Sha> requiredShas, SubdomainsToReload subdomainsToReload, CommitGraphCache commitGraphCache, JobMonitor cancellationToken)
		{
			return new Task<GitCommandResult<RevisionStorage>>(delegate
			{
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RevisionStorage>.Failure(new GitCommandError.Cancelled());
				}
				if (oldRevisions != null && !subdomainsToReload.Contains(SubDomain.Revisions))
				{
					return GitCommandResult<RevisionStorage>.Success(oldRevisions);
				}
				List<Sha> list = new List<Sha>(requiredShas);
				Sha? headSha = references.ReferenceStorage.HeadSha;
				if (headSha.HasValue)
				{
					Sha valueOrDefault = headSha.GetValueOrDefault();
					list.Add(valueOrDefault);
				}
				long timestamp = DateTime.UtcNow.MillisecondsSince1970();
				GitCommandResult<RevisionStorage> gitCommandResult = new GetRevisionStorageGitCommand().Execute(gitModule, references.ReferenceStorage, sortOrder == RevisionSortOrder.Topo, reflog, 0, ForkPlusSettings.Default.MinPagesCount, list, timestamp, commitGraphCache, cancellationToken);
				if (cancellationToken.IsCanceled)
				{
					return GitCommandResult<RevisionStorage>.Failure(new GitCommandError.Cancelled());
				}
				return (!gitCommandResult.Succeeded) ? GitCommandResult<RevisionStorage>.Failure(gitCommandResult.Error) : GitCommandResult<RevisionStorage>.Success(gitCommandResult.Result);
			});
		}

		private static bool GitFlowSettingsAreEqual(GitFlowSettings current, GitFlowSettings old)
		{
			if (current != null)
			{
				if (old == null)
				{
					return false;
				}
				if (current.MasterBranch == old.MasterBranch && current.DevelopBranch == old.DevelopBranch && current.FeaturePrefix == old.FeaturePrefix && current.ReleasePrefix == old.ReleasePrefix && current.HotfixPrefix == old.HotfixPrefix)
				{
					return current.VersionTag == old.VersionTag;
				}
				return false;
			}
			return old == null;
		}

		private static bool ReferencesAreEqual(string[] current, string[] old)
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

		private bool StashesAreEqual(RepositoryStashes current, RepositoryStashes old)
		{
			if (current.Items.Length != old.Items.Length)
			{
				Log.Debug("Detected change in stashes count");
				return false;
			}
			for (int i = 0; i < current.Items.Length; i++)
			{
				if (!(current.Items[i].Sha == old.Items[i].Sha))
				{
					Log.Debug($"Detected change in stash {i} {current.Items[i].Sha.ToString()}");
					return false;
				}
			}
			return true;
		}

		private static bool BugtrackersAreEqual(BugtrackerLinkDefinition[] current, BugtrackerLinkDefinition[] old)
		{
			if (current.Length != old.Length)
			{
				Log.Debug("Detected bugtrackers change");
				return false;
			}
			for (int i = 0; i < current.Length; i++)
			{
				if (!current[i].BugtrackerEquals(old[i]))
				{
					Log.Debug("Detected bugtracker change");
					return false;
				}
			}
			return true;
		}

		private static bool CustomCommandsAreEqual(CustomCommand[] current, CustomCommand[] old)
		{
			if (current.Length != old.Length)
			{
				Log.Debug("Detected custom commands change");
				return false;
			}
			for (int i = 0; i < current.Length; i++)
			{
				if (!current[i].CustomCommandEquals(old[i]))
				{
					Log.Debug("Detected custom command change");
					return false;
				}
			}
			return true;
		}

		private static bool SubmodulesAreEqual(RepositorySubmodules current, RepositorySubmodules old)
		{
			if (current.Items.Length != old.Items.Length)
			{
				Log.Debug("Detected submodules change");
				return false;
			}
			for (int i = 0; i < current.Items.Length; i++)
			{
				if (!current.Items[i].SubmoduleEquals(old.Items[i]))
				{
					Log.Debug("Detected submodule change");
					return false;
				}
			}
			return true;
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
