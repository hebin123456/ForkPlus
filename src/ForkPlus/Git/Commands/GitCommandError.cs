using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ForkPlus.Git.Interaction;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public abstract class GitCommandError
	{
		public class GenericError : GitCommandError
		{
			public string Message { get; }

			public override string FriendlyDescription => Message;

			public GenericError(string message)
			{
				Message = message;
			}
		}

		public class BtError : GitCommandError
		{
			public string Error { get; }

			public override string FriendlyDescription => Error;

			public BtError(string error)
			{
				Error = error;
			}
		}

		public class NotFound : GitCommandError
		{
			[Null]
			public string Message { get; }

			public override string FriendlyDescription => Message ?? "[internal] Not found";

			public NotFound([Null] string message = null)
			{
				if (message != null)
				{
					Log.Error(message);
				}
				Message = message;
			}
		}

		public class Cancelled : GitCommandError
		{
			public override string FriendlyDescription => "[internal] Cancelled";
		}

		public class CheckoutLocalChangesWouldBeOverwritten : GitError
		{
			private static readonly string Pattern = "error: Your local changes to the following files would be overwritten by checkout:";

			public static bool Match(string stderr)
			{
				return stderr.Contains(Pattern);
			}

			public CheckoutLocalChangesWouldBeOverwritten(ShellRequestResult gitRequestResult)
				: base(gitRequestResult)
			{
			}
		}

		public class MergeLocalChangesWouldBeOverwritten : GitError
		{
			private static readonly string Pattern = "error: Your local changes to the following files would be overwritten by merge:";

			public static bool Match(string stderr)
			{
				return stderr.Contains(Pattern);
			}

			public MergeLocalChangesWouldBeOverwritten(ShellRequestResult gitRequestResult)
				: base(gitRequestResult)
			{
			}
		}

		public class ChangesAreTooLarge : GitCommandError
		{
			public long FileSize { get; }

			public override string FriendlyDescription => "Changes are too large to display";

			public ChangesAreTooLarge(long fileSize)
			{
				FileSize = fileSize;
			}
		}

		public class AutomaticMergeFailed : GitError
		{
			private static readonly string Pattern = "CONFLICT (content): Merge conflict in";

			public static bool Match(GitRequestResult gitRequestResult)
			{
				if (gitRequestResult.Stdout.Contains(Pattern))
				{
					return true;
				}
				return false;
			}

			public AutomaticMergeFailed(GitRequestResult gitRequestResult)
				: base(gitRequestResult)
			{
			}
		}

		public class CherryPickNothingToCommit : GitError
		{
			private static readonly string Pattern1 = "nothing to commit, working tree clean";

			private static readonly string Pattern2 = "The previous cherry-pick is now empty, possibly due to conflict resolution.";

			public static bool Match(GitRequestResult gitRequestResult)
			{
				if (gitRequestResult.Stdout.Contains(Pattern1) || gitRequestResult.Stdout.Contains(Pattern2))
				{
					return true;
				}
				return false;
			}

			public CherryPickNothingToCommit(GitRequestResult gitRequestResult)
				: base(gitRequestResult)
			{
			}
		}

		public class TagMismatch : GitError
		{
			private static readonly string Pattern = "(would clobber existing tag)";

			public Remote Remote { get; }

			public static bool Match(string stderr)
			{
				return stderr.Contains(Pattern);
			}

			public static bool Match(GitRequestResult gitRequestResult)
			{
				return Match(gitRequestResult.Stderr);
			}

			public TagMismatch(GitRequestResult gitRequestResult, Remote remote)
				: base(gitRequestResult)
			{
				Remote = remote;
			}
		}

		public class LfsFileIsLocked : GitError
		{
			private static readonly Regex Regex = new Regex("(.+)\\sis locked by\\s.+\\s?", RegexOptions.Multiline | RegexOptions.Compiled);

			public IReadOnlyList<string> Paths { get; }

			[Null]
			public static string[] Match(string stderr)
			{
				MatchCollection matchCollection = Regex.Matches(stderr);
				if (matchCollection.Count == 0)
				{
					return null;
				}
				List<string> list = new List<string>(4);
				for (int i = 0; i < matchCollection.Count; i++)
				{
					Match match = matchCollection[i];
					if (match.Success && match.Groups.Count == 2)
					{
						list.Add(match.Groups[1].Value);
					}
				}
				if (list.Count <= 0)
				{
					return null;
				}
				return list.ToArray();
			}

			public LfsFileIsLocked(GitRequestResult gitRequestResult, IReadOnlyList<string> paths)
				: base(gitRequestResult)
			{
				Paths = paths;
			}
		}

		public class WorkingDirectoryIsDirty : GitCommandError
		{
			public override string FriendlyDescription => "Error: Working tree contains changes";
		}

		public class RepositoryIsLocked : GitError
		{
			public override string FriendlyDescription => "Repository is busy because the Git index is locked. Please wait for the current operation to finish and try again.";

			public RepositoryIsLocked(string fullOutput, string stderr)
				: base(fullOutput, stderr)
			{
			}

			public RepositoryIsLocked(GitRequestResult response)
				: base(response)
			{
			}

			public static bool Test(string stderr)
			{
				if (stderr.StartsWith("fatal: Unable to create") && stderr.Contains("index.lock': File exists."))
				{
					return true;
				}
				return false;
			}
		}

		public class PatchDoesNotApply : GitError
		{
			private static readonly string Pattern1 = "error: patch failed";

			private static readonly string Pattern2 = "patch does not apply";

			public PatchDoesNotApply(GitRequestResult gitResponse)
				: base(gitResponse)
			{
			}

			public static bool Match(string stderr)
			{
				if (stderr.StartsWith(Pattern1) && stderr.Contains(Pattern2))
				{
					return true;
				}
				return false;
			}
		}

		public class AuthenticationFailed : GitError
		{
			public enum Kind
			{
				Generic,
				GitHubConnectionError
			}

			public Kind ErrorKind { get; }

			public AuthenticationFailed(string stderr, Kind errorKind)
				: base(stderr)
			{
				ErrorKind = errorKind;
			}

			[Null]
			public static AuthenticationFailed Test(string stderr)
			{
				if (stderr.Contains("fatal: Authentication failed"))
				{
					return new AuthenticationFailed(stderr, Kind.Generic);
				}
				if (stderr.Contains("remote: Repository not found.") && stderr.Contains("github.com"))
				{
					return new AuthenticationFailed(stderr, Kind.GitHubConnectionError);
				}
				return null;
			}
		}

		public class UnsafeRepository : GitError
		{
			private static readonly Regex UnsafeRepositoryPathRegex = new Regex("fatal: unsafe repository \\('(.+)'");

			private static readonly Regex DubiousOwnershipRegex = new Regex("fatal: detected dubious ownership in repository at \\'(.+)'");

			public string RepositoryPath { get; }

			public string ProposedRepositoryPath { get; }

			public UnsafeRepository(string fullOutput, [Null] string stderr, string repositoryPath, string proposedRepositoryPath)
				: base(fullOutput, stderr)
			{
				RepositoryPath = repositoryPath;
				ProposedRepositoryPath = proposedRepositoryPath;
			}

			[Null]
			public static UnsafeRepository Test(GitRequestResult gitRequestResult, string repositoryPath)
			{
				return Test(gitRequestResult.FullReadableOutput(), gitRequestResult.Stderr, repositoryPath);
			}

			[Null]
			public static UnsafeRepository Test(string fullOutput, [Null] string stderr, string repositoryPath)
			{
				if (stderr == null)
				{
					return null;
				}
				if (TestUnsafeRepository(stderr))
				{
					Match match = UnsafeRepositoryPathRegex.Match(stderr);
					if (!match.Success || match.Groups.Count != 2)
					{
						Log.Error("Cannot parse unsafe repository path in '" + stderr + "'");
						return new UnsafeRepository(fullOutput, stderr, repositoryPath, repositoryPath);
					}
					return new UnsafeRepository(fullOutput, stderr, repositoryPath, match.Groups[1].Value);
				}
				if (TestDubiousOwnership(stderr))
				{
					Match match2 = DubiousOwnershipRegex.Match(stderr);
					if (!match2.Success || match2.Groups.Count != 2)
					{
						Log.Error("Cannot parse dubious ownership repository path in '" + stderr + "'");
						return new UnsafeRepository(fullOutput, stderr, repositoryPath, repositoryPath);
					}
					return new UnsafeRepository(fullOutput, stderr, repositoryPath, match2.Groups[1].Value);
				}
				return null;
			}

			private static bool TestUnsafeRepository(string stderr)
			{
				if (stderr.Contains("fatal: unsafe repository ") && stderr.Contains("is owned by someone else)"))
				{
					return true;
				}
				return false;
			}

			private static bool TestDubiousOwnership(string stderr)
			{
				if (stderr.Contains("detected dubious ownership in repository at"))
				{
					return true;
				}
				return false;
			}
		}

		public class MergeUnrelatedHistory : GitError
		{
			private static readonly string Pattern = "fatal: refusing to merge unrelated histories";

			public Reference Source { get; }

			public MergeType MergeType { get; }

			public static bool Match(GitRequestResult gitRequestResult)
			{
				string stderr = gitRequestResult.Stderr;
				if (stderr != null && stderr.Contains(Pattern))
				{
					return true;
				}
				return false;
			}

			public MergeUnrelatedHistory(GitRequestResult gitResponse, Reference source, MergeType mergeType)
				: base(gitResponse)
			{
				Source = source;
				MergeType = mergeType;
			}
		}

		public class ParseError : GitCommandError
		{
			public string ErrorMessage { get; }

			public override string FriendlyDescription => ErrorMessage;

			public ParseError(string stderr)
			{
				Log.Error(stderr);
				ErrorMessage = stderr;
			}
		}

		public class Bug : GitCommandError
		{
			public string Message { get; }

			public override string FriendlyDescription => "[bug] " + Message;

			public Bug(string message)
			{
				Log.Error(message);
				Message = message;
			}
		}

		public class FileIsBusy : GitCommandError
		{
			public string FilePath { get; }

			public override string FriendlyDescription => "[internal] File is busy '" + FilePath + "'";

			public FileIsBusy(string filePath)
			{
				FilePath = filePath;
			}
		}

		public class UnknownException : GitCommandError
		{
			public Exception Exception { get; }

			public override string FriendlyDescription => $"[Internal]: {Exception}";

			public UnknownException(Exception ex)
			{
				Exception = ex;
				Log.Error("Unknown exception", ex);
			}
		}

		public class GitError : GitCommandError
		{
				private static readonly Regex NoPathInCommitRegex = new Regex("fatal: There is no path '(.+)' in the commit", RegexOptions.Compiled);
			private static readonly Regex PermissionDeniedOpenRegex = new Regex("error: open\\('?(.+?)'?\\): Permission denied", RegexOptions.Compiled);
			private static readonly Regex CannotHashRegex = new Regex("fatal: cannot hash (.+)", RegexOptions.Compiled);

			public string FullOutput { get; }

			public string Stderr { get; }

			public override string FriendlyDescription => Translate(FullOutput);

			public GitError(string fullOutput, string stderr)
			{
				FullOutput = fullOutput;
				Stderr = stderr;
			}

			public GitError(ShellRequestResult gitRequestResult)
			{
				FullOutput = gitRequestResult.FullReadableOutput();
				Stderr = gitRequestResult.Stderr;
			}

			public GitError(string stderr)
			{
				FullOutput = stderr;
				Stderr = stderr;
			}

			public override string ToString()
			{
				return FullOutput;
			}

			private static string Translate(string output)
			{
			 if (string.IsNullOrEmpty(output))
			 {
			  return output;
			 }
			 output = NoPathInCommitRegex.Replace(output, delegate(Match match)
			 {
			  return string.Format(PreferencesLocalization.Translate("fatal: There is no path '{0}' in the commit", ForkPlusSettings.Default.UiLanguage), match.Groups[1].Value);
			 });
			 output = PermissionDeniedOpenRegex.Replace(output, delegate(Match match)
			 {
			  return string.Format(PreferencesLocalization.Translate("error: open('{0}'): Permission denied", ForkPlusSettings.Default.UiLanguage), match.Groups[1].Value);
			 });
			 output = CannotHashRegex.Replace(output, delegate(Match match)
			 {
			  string key = (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
			   ? "fatal: cannot hash {0}"
			   : "fatal: cannot hash";
			  if (key.Contains("{0}"))
			  {
			   return string.Format(PreferencesLocalization.Translate(key, ForkPlusSettings.Default.UiLanguage), match.Groups[1].Value);
			  }
			  return PreferencesLocalization.Translate(key, ForkPlusSettings.Default.UiLanguage);
			 });
			 return output;
			}
		}

		public class CallbackUnknownError : GitCommandError
		{
			public string FullOutput { get; }

			public override string FriendlyDescription => FullOutput;

			public CallbackUnknownError(string fullOutput)
			{
				FullOutput = fullOutput;
			}
		}

		public class CommitFailed : GitCommandError
		{
			public string Message { get; }

			public bool Amend { get; }

			public bool CommitAndPush { get; }

			public string Stderr { get; }

			public override string FriendlyDescription => "[Internal]: git commit failed";

			public CommitFailed(string message, bool amend, bool commitAndPush, string stderr)
			{
				Message = message;
				Amend = amend;
				CommitAndPush = commitAndPush;
				Stderr = stderr;
			}
		}

		public abstract string FriendlyDescription { get; }
	}
}
