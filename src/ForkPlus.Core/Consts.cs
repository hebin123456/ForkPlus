namespace ForkPlus
{
	public static class Consts
	{
		public static class ForkPlus
		{
			public static class ApplicationUpdate
			{
				public static readonly string DevelopUpdateChannel = "https://hebin.me/update/win";

				public static readonly string StableUpdateChannel = "https://hebin.me/update/win/stable";
			}

			public static readonly string RepositorySettingsFilename = "fork-plus-settings";

			public static readonly string AskPassFilename = "ForkPlus.AskPass.exe";

			public static readonly string RIHelperFilename = "ForkPlus.RI.exe";

			public static readonly string BashFilename = "bash.exe";

			public static readonly string GitInstanceEnvVariable = "forkgitinstance";

			public static readonly string Website = "https://hebin.me";
		}

		public static class Env
		{
			public static readonly string AskPass = "SSH_ASKPASS";

			public static readonly string ForkPlusProcessId = "FORK_PLUS_PROCESS_ID";

			public static readonly string NoPrompt = "NO_PROMPT";

			public static readonly int ArgumentLengthLimit = 32000;

			public static string ProgramFiles => "%ProgramW6432%";

			public static string ProgramFiles86 => "%programfiles(x86)%";
		}

		public static class Git
		{
			public static class References
			{
				public static readonly string[] SpaceCharacterReplacements = new string[2] { "-", "_" };
			}

			public static class Diff
			{
				public const string SrcPrefix = "forkPlusSrcPrefix/";

				public const string DstPrefix = "forkPlusDstPrefix/";
			}

			public const string NullSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

			public const string GitDirectory = ".git";

			public const string HooksDirectory = "hooks";

			public const string CommitMessageFile = "COMMITMESSAGE";

			public const string MergeCommitMessageFile = "MERGE_MSG";

			public const string SquashCommitMessageFile = "SQUASH_MSG";

			public const string HeadReferencePrefix = "refs/heads/";

			public const string RemoteReferencePrefix = "refs/remotes/";

			public const string TagReferencePrefix = "refs/tags/";

			public const string BisectReferencePrefix = "refs/bisect/";

			public static readonly string DefaultRemoteName = "origin";

			public static readonly string PatchFileExtension = ".patch";

			public static readonly string CommentChar = "^";

			public const string SubmodulesPathPart = "\\.git\\modules\\";
		}

		public static class GitFlow
		{
			public const string MasterConfigName = "gitflow.branch.master";

			public const string DevelopConfigName = "gitflow.branch.develop";

			public const string FeaturePrefixConfigName = "gitflow.prefix.feature";

			public const string ReleasePrefixConfigName = "gitflow.prefix.release";

			public const string HotfixPrefixConfigName = "gitflow.prefix.hotfix";

			public const string VersionTagPrefixConfigName = "gitflow.prefix.versiontag";

			public const string BugfixPrefixConfigName = "gitflow.prefix.bugfix";

			public const string SupportPrefixConfigName = "gitflow.prefix.support";

			public const string HooksPathConfigName = "gitflow.path.hooks";

			public const string BugfixPrefixDefaultValue = "bugfix/";

			public const string SupportPrefixDefaultValue = "support/";

			public const string PathConfigName = "gitflow.path";

			public const string PrefixConfigName = "gitflow.prefix";

			public const string BranchConfigName = "gitflow.branch";
		}

		public static class Chars
		{
			public static readonly char NulChar = '\0';

			public static readonly char SpaceChar = ' ';

			public static readonly char TabChar = '\t';

			public static readonly char[] Dot = new char[1] { '.' };

			public static readonly char[] Nul = new char[1];

			public static readonly char[] NewLine = new char[1] { '\n' };

			public static readonly char[] NewLines = new char[2] { '\n', '\r' };

			public static readonly char[] Semicolon = new char[1] { ';' };

			public static readonly char[] Space = new char[1] { ' ' };

			public static readonly char[] Tab = new char[1] { '\t' };

			public static readonly char[] Slash = new char[1] { '/' };

			public static readonly char[] BackSlash = new char[1] { '\\' };

			public static readonly char[] QuotationMark = new char[1] { '"' };

			public const char DirectorySeparator = '\\';

			public const char AltDirectorySeparator = '/';

			public const string Hyphen = "-";

			public const string Underscore = "_";
		}

		public static class Fonts
		{
		public const string Monospace = "Consolas";

		public const string Proportional = "Segoe UI";
		}

		public static class Sidebar
		{
			public const int TruncateRecentLimit = 20;
		}

		public static readonly string NormalDateTimeFormat = "d MMM yyyy HH:mm";

		public static readonly string FullDateTimeFormat = "d MMM yyyy HH:mm:ss zzz";

		public static readonly string DirtyWorkingDirectoryMark = "*";

		public const int ShaLength = 40;

		public const int AbbreviatedShaLength = 7;

		public const int SearchLimit = 1000;

		public const int NotLfsFileMaxSize = 500000;

		public const int MaxRecentSearchItems = 10;

		public const int RevisionsPageSize = 10000;
	}
}
