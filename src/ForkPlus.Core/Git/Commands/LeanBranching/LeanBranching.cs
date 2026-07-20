using ForkPlus.Services;
using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Biturbo;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands.LeanBranching
{
	public static class LeanBranching
	{
		private static class SyncFile
		{
			public const string NextStep = "nextStep";

			public const string LocalMain = "localMain";

			public const string Main = "main";

			public const string Origin = "orig";

			public const string OriginSha = "orig-head";

			public const string Stash = "stash";

			public const string UpdateSubmodules = "update-submodules";

			public const string Upstream = "upstream";

			public const string UpstreamPosition = "upstreamPosition";
		}

		private static class SyncStep
		{
			public const string Step0Stash = "0";

			public const string Step1SyncWithUpstream = "1";

			public const string Step2SyncWithMain = "2";

			public const string Step3MoveUpstream = "3";

			public const string Step4UpdateSubmodules = "4";

			public const string Step5ApplyStash = "5";
		}

		public static bool IsSyncInProgress(GitModule gitModule)
		{
			return SyncDirectoryExists(gitModule);
		}

		public static GitCommandResult StartSync(GitModule gitModule, string localMainFullReference, string mainBranchFullReference, string activeBranchFullReference, string activeBranchSha, [Null] string upstreamFullReference, SubmodulesToUpdate submodulesToUpdate, bool stashAndReapply, JobMonitor monitor)
		{
			if (!CreateSyncDirectory(gitModule))
			{
				string text = "Cannot create .git/fork/sync directory";
				monitor.AppendOutputLine(text);
				monitor.Update(0.0, text, JobMonitorState.Failed);
				return GitCommandResult.Failure(new GitCommandError.Bug(text));
			}
			WriteSyncFile(gitModule, "localMain", localMainFullReference);
			WriteSyncFile(gitModule, "main", mainBranchFullReference);
			WriteSyncFile(gitModule, "orig", activeBranchFullReference);
			WriteSyncFile(gitModule, "orig-head", activeBranchSha);
			if (submodulesToUpdate.Length > 0)
			{
				WriteSyncFile(gitModule, "update-submodules", "");
			}
			if (stashAndReapply)
			{
				WriteSyncFile(gitModule, "stash", "");
			}
			if (upstreamFullReference != null)
			{
				WriteSyncFile(gitModule, "upstream", upstreamFullReference);
			}
			WriteSyncFile(gitModule, "nextStep", "0");
			return GitCommandResult.Success();
		}

		public static void AbortSync(GitModule gitModule)
		{
			RemoveSyncDirectory(gitModule);
		}

		public static GitCommandResult NextSyncStep(GitModule gitModule, CommitGraphCache commitGraphCache, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			string text = ReadSyncFile(gitModule, "nextStep");
			return text switch
			{
				"0" => Step0Stash(gitModule, monitor), 
				"1" => Step1SyncWithUpstream(gitModule, commitGraphCache, monitor), 
				"2" => Step2SyncWithMain(gitModule, monitor), 
				"3" => Step3MoveUpstream(gitModule, commitGraphCache, monitor), 
				"4" => Step4UpdateSubmodules(gitModule, submodulesToUpdate, monitor), 
				"5" => Step5ApplyStash(gitModule, monitor), 
				_ => GitCommandResult.Failure(new GitCommandError.Bug("Internal LeanBranching error: Invalid sync step '" + text + "'.\nRemove '.git/fork/sync' directory manually and start again.")), 
			};
		}

		private static GitCommandResult Step0Stash(GitModule gitModule, JobMonitor monitor)
		{
			if (ReadSyncFile(gitModule, "stash") == null)
			{
				WriteSyncFile(gitModule, "nextStep", "1");
				return GitCommandResult.Success();
			}
			monitor.AppendOutputLine(ServiceLocator.Localization.Current("# Stash uncommitted changes:\n"));
			GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Fork sync autostash {DateTime.Now}", stageNewFiles: true, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult.Error);
			}
			if (!gitCommandResult.Result)
			{
				WriteSyncFile(gitModule, "nextStep", "1");
				return GitCommandResult.Success();
			}
			string text;
			try
			{
				text = File.ReadAllText(gitModule.StashFilePath());
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			string text2 = text.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstItem();
			if (text2 == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find stash SHA after a successuful stash"));
			}
			WriteSyncFile(gitModule, "stash", text2);
			WriteSyncFile(gitModule, "nextStep", "1");
			return GitCommandResult.Success();
		}

		private static GitCommandResult Step1SyncWithUpstream(GitModule gitModule, CommitGraphCache commitGraphCache, JobMonitor monitor)
		{
			string text = ReadSyncFile(gitModule, "orig");
			if (text == null || !(Reference.Create(Sha.NullSha, isHead: true, text, null, null, DateTimeHelper.UnixStartTime) is LocalBranch localBranch))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read active branch"));
			}
			string text2 = ReadSyncFile(gitModule, "localMain");
			if (text2 == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read local main branch"));
			}
			string text3 = ReadSyncFile(gitModule, "upstream");
			if (text3 != null && Reference.Create(Sha.NullSha, isHead: true, text3, null, null, DateTimeHelper.UnixStartTime) is RemoteBranch remoteBranch)
			{
				monitor.AppendOutputLine(ServiceLocator.Localization.FormatCurrent("# Synchronize '{0}' with '{1}':\n", localBranch.Name, remoteBranch.Name));
				bool rebaseMerges = localBranch.FullReference == text2;
				GitCommandResult gitCommandResult = new RebaseBranchGitCommand().Execute(gitModule, text3, rebaseMerges, updateRefs: false, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return gitCommandResult;
				}
				GitCommandResult<GitConfig> gitCommandResult2 = new GetGitConfigGitCommand().Execute(gitModule);
				if (!gitCommandResult2.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult2.Error);
				}
				GitConfig result = gitCommandResult2.Result;
				GitCommandResult<ReferenceStorage> gitCommandResult3 = new GetReferencesGitCommand().Execute(gitModule, result);
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult3.Error);
				}
				ReferenceStorage result2 = gitCommandResult3.Result;
				Sha? sha = null;
				Sha? sha2 = null;
				for (int i = 0; i < result2.Refs.Length; i++)
				{
					string text4 = result2.Refs[i];
					if (text4 == text)
					{
						sha = result2.Shas[i];
					}
					else if (text4 == text3)
					{
						sha2 = result2.Shas[i];
					}
				}
				if (!sha.HasValue)
				{
					return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find active branch SHA"));
				}
				Sha valueOrDefault = sha.GetValueOrDefault();
				if (!sha2.HasValue)
				{
					return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find upstream SHA"));
				}
				Sha valueOrDefault2 = sha2.GetValueOrDefault();
				GitCommandResult<RevisionStorage> gitCommandResult4 = new GetRevisionStorageGitCommand().Execute(gitModule, commitGraphCache, valueOrDefault, valueOrDefault2);
				RevisionStorage result3 = gitCommandResult4.Result;
				if (result3 == null)
				{
					return gitCommandResult4.ToGitCommandResult();
				}
				int[] remotePosition = GetRemotePosition(result3);
				WriteSyncFile(gitModule, "upstreamPosition", string.Join("\n", remotePosition));
			}
			if (text == text2)
			{
				WriteSyncFile(gitModule, "nextStep", "4");
				return GitCommandResult.Success();
			}
			WriteSyncFile(gitModule, "nextStep", "2");
			return GitCommandResult.Success();
		}

		private static GitCommandResult Step2SyncWithMain(GitModule gitModule, JobMonitor monitor)
		{
			string text = ReadSyncFile(gitModule, "orig");
			if (text == null || !(Reference.Create(Sha.NullSha, isHead: true, text, null, null, DateTimeHelper.UnixStartTime) is LocalBranch localBranch))
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read active branch"));
			}
			string text2 = ReadSyncFile(gitModule, "main");
			if (text2 == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read main branch"));
			}
			monitor.AppendOutputLine(ServiceLocator.Localization.FormatCurrent("# Synchronize '{0}' with '{1}':\n", localBranch.Name, text2));
			GitCommandResult gitCommandResult = new RebaseBranchGitCommand().Execute(gitModule, text2, rebaseMerges: false, updateRefs: false, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			WriteSyncFile(gitModule, "nextStep", "3");
			return GitCommandResult.Success();
		}

		private static GitCommandResult Step3MoveUpstream(GitModule gitModule, CommitGraphCache commitGraphCache, JobMonitor monitor)
		{
			string text = ReadSyncFile(gitModule, "upstream");
			if (text == null || !(Reference.Create(Sha.NullSha, isHead: true, text, null, null, DateTimeHelper.UnixStartTime) is RemoteBranch remoteBranch))
			{
				WriteSyncFile(gitModule, "nextStep", "4");
				return GitCommandResult.Success();
			}
			string text2 = ReadSyncFile(gitModule, "orig");
			if (text2 == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read active branch"));
			}
			string text3 = ReadSyncFile(gitModule, "upstreamPosition");
			if (text3 == null)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Invalid 'sync' content. Cannot read upstream position"));
			}
			int[] parentIndexes = text3.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries).Map((string x) => int.Parse(x));
			GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult.Error);
			}
			GitConfig result = gitCommandResult.Result;
			GitCommandResult<ReferenceStorage> gitCommandResult2 = new GetReferencesGitCommand().Execute(gitModule, result);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult.Failure(gitCommandResult2.Error);
			}
			ReferenceStorage result2 = gitCommandResult2.Result;
			Sha? sha = null;
			Sha? sha2 = null;
			for (int i = 0; i < result2.Refs.Length; i++)
			{
				string text4 = result2.Refs[i];
				if (text4 == text2)
				{
					sha = result2.Shas[i];
				}
				else if (text4 == text)
				{
					sha2 = result2.Shas[i];
				}
			}
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				if (sha2.HasValue)
				{
					Sha valueOrDefault2 = sha2.GetValueOrDefault();
					GitCommandResult<RevisionStorage> gitCommandResult3 = new GetRevisionStorageGitCommand().Execute(gitModule, commitGraphCache, valueOrDefault, valueOrDefault2);
					if (gitCommandResult3.Result == null)
					{
						return gitCommandResult3.ToGitCommandResult();
					}
					string text5 = GetNewRemotePosition(gitCommandResult3.Result, parentIndexes)?.ToString() ?? text2;
					monitor.AppendOutputLine(ServiceLocator.Localization.FormatCurrent("# Move '{0}' to '{1}':\n", remoteBranch.Name, text5));
					GitCommandResult gitCommandResult4 = new PushGitCommand().Execute(gitModule, remoteBranch, text5, monitor);
					if (!gitCommandResult4.Succeeded)
					{
						return gitCommandResult4;
					}
					WriteSyncFile(gitModule, "nextStep", "4");
					return GitCommandResult.Success();
				}
				return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find upstream SHA"));
			}
			return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find active branch SHA"));
		}

		private static GitCommandResult Step4UpdateSubmodules(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			if (ReadSyncFile(gitModule, "update-submodules") == null)
			{
				WriteSyncFile(gitModule, "nextStep", "5");
				return GitCommandResult.Success();
			}
			monitor.AppendOutputLine(ServiceLocator.Localization.Current("# Update submodules:\n"));
			GitCommandResult gitCommandResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			WriteSyncFile(gitModule, "nextStep", "5");
			return GitCommandResult.Success();
		}

		private static GitCommandResult Step5ApplyStash(GitModule gitModule, JobMonitor monitor)
		{
			string text = ReadSyncFile(gitModule, "stash");
			if (text == null || !(text != ""))
			{
				RemoveSyncDirectory(gitModule);
				return GitCommandResult.Success();
			}
			monitor.AppendOutputLine(ServiceLocator.Localization.Current("# Apply stashed changes:\n"));
			string text2;
			try
			{
				text2 = File.ReadAllText(gitModule.StashFilePath());
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			int num = text2.IndexOf(text);
			if (num == -1)
			{
				return GitCommandResult.Failure(new GitCommandError.Bug("Cannot find stash by SHA"));
			}
			GitCommandResult gitCommandResult = new ApplyStashGitCommand().Execute(gitModule, $"stash@{{{num}}}", deleteAfterApply: true, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			RemoveSyncDirectory(gitModule);
			return GitCommandResult.Success();
		}

		[Null]
		public static RemoteBranch Upstream(this RepositoryReferences repositoryReferences, LocalBranch localBranch)
		{
			string upstreamFullReference = localBranch.UpstreamFullReference;
			if (upstreamFullReference != null)
			{
				RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryReferences.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
				if (remoteBranch != null)
				{
					return remoteBranch;
				}
			}
			return null;
		}

		[Null]
		public static LocalBranch LocalMain(this RepositoryReferences repositoryReferences, GitModule gitModule)
		{
			string mainBranch = gitModule.Settings.LeanBranchingMainBranch;
			return IReadOnlyListExtensions.FirstItem(repositoryReferences.LocalBranches, (LocalBranch x) => x.Name == mainBranch) ?? IReadOnlyListExtensions.FirstItem(repositoryReferences.LocalBranches, (LocalBranch x) => x.Name == "develop") ?? IReadOnlyListExtensions.FirstItem(repositoryReferences.LocalBranches, (LocalBranch x) => x.Name == "main") ?? IReadOnlyListExtensions.FirstItem(repositoryReferences.LocalBranches, (LocalBranch x) => x.Name == "master");
		}

		[Null]
		public static Branch MainBranch(this RepositoryReferences repositoryReferences, GitModule gitModule, CommitGraphCache commitGraphCache)
		{
			LocalBranch localBranch = repositoryReferences.LocalMain(gitModule);
			if (localBranch == null)
			{
				return null;
			}
			RemoteBranch remoteBranch = repositoryReferences.Upstream(localBranch);
			if (remoteBranch == null)
			{
				return localBranch;
			}
			if (localBranch.Sha == remoteBranch.Sha)
			{
				return localBranch;
			}
			GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, localBranch.Sha, remoteBranch.Sha, commitGraphCache);
			if (!gitCommandResult.Succeeded)
			{
				return null;
			}
			if (gitCommandResult.Result.Left != 0)
			{
				return localBranch;
			}
			return remoteBranch;
		}

		private static int[] GetRemotePosition(RevisionStorage revisionStorage)
		{
			RevisionStorage.Handle? handle = revisionStorage.First();
			if (handle.HasValue)
			{
				RevisionStorage.Handle valueOrDefault = handle.GetValueOrDefault();
				Sha sha = revisionStorage.GetSha(valueOrDefault);
				List<int> list = new List<int>();
				HashSet<Sha> hashSet = new HashSet<Sha>();
				hashSet.Add(sha);
				HandleEnumerator enumerator = revisionStorage.GetEnumerator();
				while (enumerator.MoveNext())
				{
					RevisionStorage.Handle current = enumerator.Current;
					Sha sha2 = revisionStorage.GetSha(current);
					if (hashSet.Remove(sha2))
					{
						list.Add(0);
						Sha? sha3 = revisionStorage.GetParents(current).Nth(0);
						if (!sha3.HasValue)
						{
							Log.Error("Cannot find parent for " + sha2);
							return new int[0];
						}
						Sha valueOrDefault2 = sha3.GetValueOrDefault();
						hashSet.Add(valueOrDefault2);
					}
				}
				list.Reverse();
				return list.ToArray();
			}
			return new int[0];
		}

		private static Sha? GetNewRemotePosition(RevisionStorage revisionStorage, int[] parentIndexes)
		{
			if (revisionStorage.Count == 0)
			{
				return null;
			}
			List<int> list = new List<int>(parentIndexes);
			int index = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
			if (revisionStorage.Count <= 0 || list.Count <= 0)
			{
				return null;
			}
			HashSet<Sha> hashSet = new HashSet<Sha>();
			RevisionStorage.Handle? handle = revisionStorage.First();
			if (handle.HasValue)
			{
				RevisionStorage.Handle valueOrDefault = handle.GetValueOrDefault();
				Sha? sha = revisionStorage.GetParents(valueOrDefault).Nth(index);
				if (!sha.HasValue)
				{
					return null;
				}
				Sha valueOrDefault2 = sha.GetValueOrDefault();
				hashSet.Add(valueOrDefault2);
			}
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				if (list.Count == 0)
				{
					break;
				}
				if (hashSet.Remove(revisionStorage.GetSha(current)))
				{
					int index2 = list[list.Count - 1];
					list.RemoveAt(list.Count - 1);
					Sha? sha = revisionStorage.GetParents(current).Nth(index2);
					if (!sha.HasValue)
					{
						return null;
					}
					Sha valueOrDefault3 = sha.GetValueOrDefault();
					hashSet.Add(valueOrDefault3);
				}
			}
			using (HashSet<Sha>.Enumerator enumerator2 = hashSet.GetEnumerator())
			{
				if (enumerator2.MoveNext())
				{
					return enumerator2.Current;
				}
			}
			return null;
		}

		private static bool SyncDirectoryExists(GitModule gitModule)
		{
			try
			{
				return Directory.Exists(gitModule.SyncPath());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check if sync dir exists", ex);
				return false;
			}
		}

		private static bool CreateSyncDirectory(GitModule gitModule)
		{
			try
			{
				Directory.CreateDirectory(gitModule.SyncPath());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create sync dir", ex);
				return false;
			}
			return true;
		}

		private static void RemoveSyncDirectory(GitModule gitModule)
		{
			try
			{
				if (Directory.Exists(gitModule.SyncPath()))
				{
					Directory.Delete(gitModule.SyncPath(), recursive: true);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to delete sync dir", ex);
			}
		}

		[Null]
		private static string ReadSyncFile(GitModule gitModule, string syncFile)
		{
			try
			{
				string path = Path.Combine(gitModule.SyncPath(), syncFile);
				if (File.Exists(path))
				{
					return File.ReadAllText(path);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read sync file '" + syncFile + "'", ex);
			}
			return null;
		}

		private static bool WriteSyncFile(GitModule gitModule, string syncFile, string value)
		{
			try
			{
				File.WriteAllText(Path.Combine(gitModule.SyncPath(), syncFile), value);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write sync file '" + syncFile + "'", ex);
				return false;
			}
			return true;
		}
	}
}
