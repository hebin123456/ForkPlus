using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus
{
	[DebuggerDisplay("Settings for {GitModule.RepositoryName}")]
	public class RepositorySettings
	{
		private static readonly string DraftMessageKey = "draftMessage";

		private string _draftMessage;

		private static readonly string RecentNewBranchPrefixKey = "recentNewBranchPrefix";

		private string _recentNewBranchPrefix;

		private static readonly string RecentRevisionSearchQueriesKey = "recentSearchQueries";

		private RevisionSearchQuery[] _recentRevisionSearchQueries;

		private static readonly string PullRequestsRecentSearchQueriesKey = "pullRequestsRecentSearchQueries";

		private string[] _pullRequestsRecentSearchQueries;

		private static readonly string IssuesRecentSearchQueriesKey = "issuesRecentSearchQueries";

		private string[] _issuesRecentSearchQueries;

		private static readonly string PullRequestsDefaultRemoteKey = "pullRequestsDefaultRemote";

		private string _pullRequestsDefaultRemote;

		private static readonly string IssuesDefaultRemoteKey = "issuesDefaultRemote";

		private string _issuesDefaultRemote;

		private static readonly string StarredReferencesKey = "starredReferences";

		private static readonly string PinnedReferencesKey = "pinnedReferences";

		private string[] _pinnedReferences;

		private static readonly string FilterReferencesKey = "filterReferences";

		private string[] _filterReferences;

		private static readonly string HiddenReferencesKey = "hiddenReferences";

		private string[] _hiddenReferences;

		private static readonly string SignOffKey = "signOff";

		private bool _signOff;

		private static readonly string ShowBugtrackerLinksKey = "showBugtrackerLinks";

		private bool _showBugtrackerLinks;

		private static readonly string HideTagsKey = "hideTags";

		private bool _hideTags;

		private static readonly string TrustSharedCommandsKey = "trustSharedCommands";

		private bool _trustSharedCommands;

		private static readonly string CollapseAllMergeRevisionsKey = "collapseAllMergeRevisions";

		private bool _collapseAllMergeRevisions;

		private static readonly string HideStashesInRevisionListKey = "hideStashesInRevisionList";

		private bool _hideStashesInRevisionList;

		private static readonly string ExpandedSidebarItemsKey = "expandedSidebarItems";

		private ExpandedTreeViewElement[] _expandedSidebarItems;

		private static readonly string RecentRemoteKey = "recentRemote";

		private string _recentRemote;

		private static readonly string PushLastCustomRefspecKey = "pushLastCustomRefspec";

		[Null]
		private string _pushLastCustomRefspec;

		private static readonly string LeanBranchingMainBranchKey = "leanBranchingMainBranch";

		private string _leanBranchingMainBranch;

		private static readonly string LeanBranchingNoFastForwardKey = "leanBranchingNoFastForward";

		private bool _leanBranchingNoFastForward;

		private static readonly string TabWidthKey = "tabWidth";

		private int _tabWidth;

		private static readonly string HideUntrackedFilesKey = "hideUntrackedFiles";

		private bool _hideUntrackedFiles;

		private static readonly string GitignoreSuggestionDismissedKey = "gitignoreSuggestionDismissed";

		private bool _gitignoreSuggestionDismissed;

		private static readonly string CommitMessageRegexKey = "commitMessageRegex";

		private string _commitMessageRegex;

		private static readonly string SkipCommitMessageKey = "skipCommitMessage";

		private bool _skipCommitMessage;

		public GitModule GitModule { get; private set; }

		public string DraftMessage
		{
			get
			{
				return _draftMessage;
			}
			set
			{
				_draftMessage = value;
			}
		}

		[Null]
		public string RecentNewBranchPrefix
		{
			get
			{
				return _recentNewBranchPrefix;
			}
			set
			{
				_recentNewBranchPrefix = value;
			}
		}

		public RevisionSearchQuery[] RecentRevisionSearchQueries
		{
			get
			{
				return _recentRevisionSearchQueries;
			}
			private set
			{
				_recentRevisionSearchQueries = value;
			}
		}

		public string[] PullRequestsRecentSearchQueries
		{
			get
			{
				return _pullRequestsRecentSearchQueries;
			}
			private set
			{
				_pullRequestsRecentSearchQueries = value;
			}
		}

		public string[] IssuesRecentSearchQueries
		{
			get
			{
				return _issuesRecentSearchQueries;
			}
			private set
			{
				_issuesRecentSearchQueries = value;
			}
		}

		[Null]
		public string PullRequestsDefaultRemote
		{
			get
			{
				return _pullRequestsDefaultRemote;
			}
			set
			{
				_pullRequestsDefaultRemote = value;
			}
		}

		[Null]
		public string IssuesDefaultRemote
		{
			get
			{
				return _issuesDefaultRemote;
			}
			set
			{
				_issuesDefaultRemote = value;
			}
		}

		public string[] PinnedReferences
		{
			get
			{
				return _pinnedReferences;
			}
			set
			{
				_pinnedReferences = value;
			}
		}

		public string[] FilterReferences
		{
			get
			{
				return _filterReferences;
			}
			set
			{
				_filterReferences = value;
			}
		}

		public string[] HiddenReferences
		{
			get
			{
				return _hiddenReferences;
			}
			set
			{
				_hiddenReferences = value;
			}
		}

		public bool SignOff
		{
			get
			{
				return _signOff;
			}
			set
			{
				_signOff = value;
			}
		}

		public bool ShowBugtrackerLinks
		{
			get
			{
				return _showBugtrackerLinks;
			}
			set
			{
				_showBugtrackerLinks = value;
			}
		}

		public bool HideTags
		{
			get
			{
				return _hideTags;
			}
			set
			{
				_hideTags = value;
			}
		}

		public bool TrustSharedCommands
		{
			get
			{
				return _trustSharedCommands;
			}
			set
			{
				_trustSharedCommands = value;
			}
		}

		public bool CollapseAllMergeRevisions
		{
			get
			{
				return _collapseAllMergeRevisions;
			}
			set
			{
				_collapseAllMergeRevisions = value;
			}
		}

		public bool HideStashesInRevisionList
		{
			get
			{
				return _hideStashesInRevisionList;
			}
			set
			{
				_hideStashesInRevisionList = value;
			}
		}

		public ExpandedTreeViewElement[] ExpandedSidebarItems
		{
			get
			{
				return _expandedSidebarItems;
			}
			set
			{
				_expandedSidebarItems = value;
			}
		}

		[Null]
		public string RecentRemote
		{
			get
			{
				return _recentRemote;
			}
			set
			{
				_recentRemote = value;
			}
		}

		[Null]
		public string PushLastCustomRefspec
		{
			get
			{
				return _pushLastCustomRefspec;
			}
			set
			{
				_pushLastCustomRefspec = value;
			}
		}

		[Null]
		public string LeanBranchingMainBranch
		{
			get
			{
				return _leanBranchingMainBranch;
			}
			set
			{
				_leanBranchingMainBranch = value;
			}
		}

		public bool LeanBranchingNoFastForward
		{
			get
			{
				return _leanBranchingNoFastForward;
			}
			set
			{
				_leanBranchingNoFastForward = value;
			}
		}

		public int TabWidth
		{
			get
			{
				return _tabWidth;
			}
			set
			{
				_tabWidth = value;
			}
		}

		public bool HideUntrackedFiles
		{
			get
			{
				return _hideUntrackedFiles;
			}
			set
			{
				_hideUntrackedFiles = value;
			}
		}

		public bool GitignoreSuggestionDismissed
		{
			get
			{
				return _gitignoreSuggestionDismissed;
			}
			set
			{
				_gitignoreSuggestionDismissed = value;
			}
		}

		[Null]
		public string CommitMessageRegex
		{
			get
			{
				return _commitMessageRegex;
			}
			set
			{
				_commitMessageRegex = value;
			}
		}

		public bool SkipCommitMessage
		{
			get
			{
				return _skipCommitMessage;
			}
			set
			{
				_skipCommitMessage = value;
			}
		}

		public static RepositorySettings Load(GitModule gitModule)
		{
			Log.Debug("Loading " + gitModule.RepositoryName + " settings");
			try
			{
				string path = gitModule.ForkPlusSettingsFile();
				if (File.Exists(path))
				{
					RepositorySettings repositorySettings = Decode(JsonConvert.DeserializeObject(File.ReadAllText(path)) as JObject);
					repositorySettings.GitModule = gitModule;
					return repositorySettings;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Cannot load settings", ex);
			}
			Log.Info("Use default repository settings");
			RepositorySettings repositorySettings2 = Decode(new JObject());
			repositorySettings2.GitModule = gitModule;
			return repositorySettings2;
		}

		public void Save()
		{
			Log.Debug("Saving " + GitModule.RepositoryName + " settings");
			Formatting formatting = Formatting.None;
			try
			{
				string contents = Encode(this).ToString(formatting);
				File.WriteAllText(GitModule.ForkPlusSettingsFile(), contents);
			}
			catch (Exception ex)
			{
				Log.Error("Cannot save " + GitModule.RepositoryName + " settings", ex);
			}
		}

		public void AddSearchQueryToRecent(RevisionSearchQuery revisionSearchQuery)
		{
			RecentRevisionSearchQueries = CreateRecent(RecentRevisionSearchQueries, revisionSearchQuery);
		}

		public void AddIssueSearchQueryToRecent(string item)
		{
			IssuesRecentSearchQueries = CreateRecent(IssuesRecentSearchQueries, item);
		}

		public void AddPullRequestSearchQueryToRecent(string item)
		{
			PullRequestsRecentSearchQueries = CreateRecent(PullRequestsRecentSearchQueries, item);
		}

		public void ClearRecentIssueSearchQueries()
		{
			IssuesRecentSearchQueries = new string[0];
		}

		public void ClearRecentRevisionSearchQueries()
		{
			RecentRevisionSearchQueries = new RevisionSearchQuery[0];
		}

		public void ClearRecentPullRequestSearchQueries()
		{
			PullRequestsRecentSearchQueries = new string[0];
		}

		private static RepositorySettings Decode(JObject json)
		{
			json = json ?? new JObject();
			string draftMessage = json[DraftMessageKey]?.Value<string>() ?? string.Empty;
			string recentNewBranchPrefix = json[RecentNewBranchPrefixKey]?.Value<string>();
			RevisionSearchQuery[] recentRevisionSearchQueries = RevisionSearchQuery.Coder.Decode(json[RecentRevisionSearchQueriesKey] as JArray);
			string[] issuesRecentSearchQueries = JsonHelper.DecodeStringArray(json[IssuesRecentSearchQueriesKey] as JArray) ?? new string[0];
			string[] pullRequestsRecentSearchQueries = JsonHelper.DecodeStringArray(json[PullRequestsRecentSearchQueriesKey] as JArray) ?? new string[0];
			string[] pinnedReferences = JsonHelper.DecodeStringArray(json[PinnedReferencesKey] as JArray) ?? JsonHelper.DecodeStringArray(json[StarredReferencesKey] as JArray) ?? new string[0];
			string[] filterReferences = JsonHelper.DecodeStringArray(json[FilterReferencesKey] as JArray) ?? new string[0];
			string[] hiddenReferences = JsonHelper.DecodeStringArray(json[HiddenReferencesKey] as JArray) ?? new string[0];
			bool signOff = json[SignOffKey]?.Value<bool>() ?? false;
			ExpandedTreeViewElement[] expandedSidebarItems = ExpandedTreeViewElement.Coder.DecodeExpandedTreeViewElementArray(json[ExpandedSidebarItemsKey] as JArray) ?? ExpandedTreeViewElement.Coder.Decode(json[ExpandedSidebarItemsKey] as JObject)?.Children;
			bool showBugtrackerLinks = json[ShowBugtrackerLinksKey]?.Value<bool>() ?? true;
			bool trustSharedCommands = json[TrustSharedCommandsKey]?.Value<bool>() ?? false;
			bool hideTags = json[HideTagsKey]?.Value<bool>() ?? false;
			bool collapseAllMergeRevisions = json[CollapseAllMergeRevisionsKey]?.Value<bool>() ?? false;
			bool hideStashesInRevisionList = json[HideStashesInRevisionListKey]?.Value<bool>() ?? false;
			string recentRemote = json[RecentRemoteKey]?.Value<string>();
			string pushLastCustomRefspec = json[PushLastCustomRefspecKey]?.Value<string>();
			string pullRequestsDefaultRemote = json[PullRequestsDefaultRemoteKey]?.Value<string>();
			string issuesDefaultRemote = json[IssuesDefaultRemoteKey]?.Value<string>();
			string leanBranchingMainBranch = json[LeanBranchingMainBranchKey]?.Value<string>();
			bool leanBranchingNoFastForward = json[LeanBranchingNoFastForwardKey]?.Value<bool>() ?? false;
			int tabWidth = json[TabWidthKey]?.Value<int>() ?? 4;
			bool hideUntrackedFiles = json[HideUntrackedFilesKey]?.Value<bool>() ?? false;
			bool gitignoreSuggestionDismissed = json[GitignoreSuggestionDismissedKey]?.Value<bool>() ?? false;
			string commitMessageRegex = json[CommitMessageRegexKey]?.Value<string>() ?? string.Empty;
			bool skipCommitMessage = json[SkipCommitMessageKey]?.Value<bool>() ?? false;
			return new RepositorySettings
			{
				DraftMessage = draftMessage,
				RecentNewBranchPrefix = recentNewBranchPrefix,
				RecentRevisionSearchQueries = recentRevisionSearchQueries,
				IssuesRecentSearchQueries = issuesRecentSearchQueries,
				PullRequestsRecentSearchQueries = pullRequestsRecentSearchQueries,
				PullRequestsDefaultRemote = pullRequestsDefaultRemote,
				IssuesDefaultRemote = issuesDefaultRemote,
				PushLastCustomRefspec = pushLastCustomRefspec,
				PinnedReferences = pinnedReferences,
				FilterReferences = filterReferences,
				HiddenReferences = hiddenReferences,
				SignOff = signOff,
				ExpandedSidebarItems = expandedSidebarItems,
				ShowBugtrackerLinks = showBugtrackerLinks,
				TrustSharedCommands = trustSharedCommands,
				HideTags = hideTags,
				CollapseAllMergeRevisions = collapseAllMergeRevisions,
				HideStashesInRevisionList = hideStashesInRevisionList,
				RecentRemote = recentRemote,
				LeanBranchingMainBranch = leanBranchingMainBranch,
				LeanBranchingNoFastForward = leanBranchingNoFastForward,
				TabWidth = tabWidth,
				HideUntrackedFiles = hideUntrackedFiles,
				GitignoreSuggestionDismissed = gitignoreSuggestionDismissed,
				CommitMessageRegex = commitMessageRegex,
				SkipCommitMessage = skipCommitMessage
			};
		}

		private static JObject Encode(RepositorySettings target)
		{
			return new JObject
			{
				{
					DraftMessageKey,
					new JValue(target.DraftMessage)
				},
				{
					RecentNewBranchPrefixKey,
					new JValue(target.RecentNewBranchPrefix)
				},
				{
					RecentRevisionSearchQueriesKey,
					RevisionSearchQuery.Coder.Encode(target.RecentRevisionSearchQueries)
				},
				{
					IssuesRecentSearchQueriesKey,
					JsonHelper.EncodeStringArray(target.IssuesRecentSearchQueries)
				},
				{
					PullRequestsRecentSearchQueriesKey,
					JsonHelper.EncodeStringArray(target.PullRequestsRecentSearchQueries)
				},
				{
					PullRequestsDefaultRemoteKey,
					new JValue(target.PullRequestsDefaultRemote)
				},
				{
					IssuesDefaultRemoteKey,
					new JValue(target.IssuesDefaultRemote)
				},
				{
					StarredReferencesKey,
					JsonHelper.EncodeStringArray(target.PinnedReferences)
				},
				{
					FilterReferencesKey,
					JsonHelper.EncodeStringArray(target.FilterReferences)
				},
				{
					HiddenReferencesKey,
					JsonHelper.EncodeStringArray(target.HiddenReferences)
				},
				{
					SignOffKey,
					new JValue(target.SignOff)
				},
				{
					ExpandedSidebarItemsKey,
					ExpandedTreeViewElement.Coder.EncodeExpandedTreeViewElementArray(target.ExpandedSidebarItems)
				},
				{
					ShowBugtrackerLinksKey,
					new JValue(target.ShowBugtrackerLinks)
				},
				{
					TrustSharedCommandsKey,
					new JValue(target.TrustSharedCommands)
				},
				{
					HideTagsKey,
					new JValue(target.HideTags)
				},
				{
					CollapseAllMergeRevisionsKey,
					new JValue(target.CollapseAllMergeRevisions)
				},
				{
					HideStashesInRevisionListKey,
					new JValue(target.HideStashesInRevisionList)
				},
				{
					RecentRemoteKey,
					new JValue(target.RecentRemote)
				},
				{
					PushLastCustomRefspecKey,
					new JValue(target.PushLastCustomRefspec)
				},
				{
					LeanBranchingMainBranchKey,
					new JValue(target.LeanBranchingMainBranch)
				},
				{
					LeanBranchingNoFastForwardKey,
					new JValue(target.LeanBranchingNoFastForward)
				},
				{
					TabWidthKey,
					new JValue(target.TabWidth)
				},
				{
					HideUntrackedFilesKey,
					new JValue(target.HideUntrackedFiles)
				},
				{
					GitignoreSuggestionDismissedKey,
					new JValue(target.GitignoreSuggestionDismissed)
				},
				{
					CommitMessageRegexKey,
					new JValue(target.CommitMessageRegex)
				},
				{
					SkipCommitMessageKey,
					new JValue(target.SkipCommitMessage)
				}
			};
		}

		private static RevisionSearchQuery[] CreateRecent(RevisionSearchQuery[] recentQueries, RevisionSearchQuery newSearchQuery)
		{
			List<RevisionSearchQuery> list = new List<RevisionSearchQuery>(Math.Min(recentQueries.Length + 1, 10));
			list.Add(newSearchQuery);
			for (int i = 0; i < recentQueries.Length; i++)
			{
				if (list.Count > 10)
				{
					break;
				}
				RevisionSearchQuery revisionSearchQuery = recentQueries[i];
				if (!(revisionSearchQuery.SearchString == newSearchQuery.SearchString) || revisionSearchQuery.Type != newSearchQuery.Type || revisionSearchQuery.Scope != newSearchQuery.Scope)
				{
					list.Add(recentQueries[i]);
				}
			}
			return list.ToArray();
		}

		private static string[] CreateRecent(string[] recentQueries, string newSearchQuery)
		{
			List<string> list = new List<string>(Math.Min(recentQueries.Length + 1, 10));
			list.Add(newSearchQuery);
			for (int i = 0; i < recentQueries.Length; i++)
			{
				if (list.Count > 10)
				{
					break;
				}
				if (!(recentQueries[i] == newSearchQuery))
				{
					list.Add(recentQueries[i]);
				}
			}
			return list.ToArray();
		}
	}
}
