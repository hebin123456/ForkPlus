using System.Text.RegularExpressions;

namespace ForkPlus.Git.Commands
{
	public abstract class GetFileChangesGitCommand
	{
		private static readonly Regex BinaryFileFallbackRegex = new Regex("^Binary files", RegexOptions.Multiline | RegexOptions.Compiled);

		private static readonly Regex LfsFileDiffRegEx = new Regex("([-+])oid sha256:(\\b[0-9a-f]{64})\\n([-+])size (\\d*)", RegexOptions.Multiline | RegexOptions.Compiled);

		private static readonly Regex LfsFileMergeRegEx = new Regex("( -|- )oid sha256:(\\b[0-9a-f]{64})\\n( -|- )size (\\d*)", RegexOptions.Multiline | RegexOptions.Compiled);

		private static readonly Regex LfsSameSizeFileMergeRegEx = new Regex("- oid sha256:(\\b[0-9a-f]{64})\\n -oid sha256:(\\b[0-9a-f]{64})\n--size (\\d*)", RegexOptions.Multiline | RegexOptions.Compiled);

		public static readonly Regex BinaryFileMergeRegEx = new Regex("(\\b[0-9a-f]{40}),(\\b[0-9a-f]{40})", RegexOptions.Multiline | RegexOptions.Compiled);

		protected static bool IsBinaryContent([Null] string output)
		{
			if (string.IsNullOrEmpty(output))
			{
				return false;
			}
			if (output.Length > 3072)
			{
				return false;
			}
			if (BinaryFileFallbackRegex.IsMatch(output))
			{
				return true;
			}
			return false;
		}

		protected static bool IsLfsContent([Null] string output)
		{
			if (output == null)
			{
				return false;
			}
			if (output.Length <= 120 || output.Length >= 1024)
			{
				return false;
			}
			if (!output.Contains("version https://git-lfs.github.com/spec/v1"))
			{
				return false;
			}
			if (!output.Contains("oid sha256:"))
			{
				return false;
			}
			return true;
		}

		public static LfsPointer[] ParseLfsDiff(string output, bool merge)
		{
			if (merge)
			{
				MatchCollection matchCollection = LfsFileMergeRegEx.Matches(output);
				if (matchCollection.Count == 2)
				{
					LfsPointer lfsPointer = ParseInMergeMatch(matchCollection[0]);
					if (lfsPointer != null)
					{
						LfsPointer lfsPointer2 = ParseInMergeMatch(matchCollection[1]);
						if (lfsPointer2 != null)
						{
							return new LfsPointer[2] { lfsPointer2, lfsPointer };
						}
					}
				}
				MatchCollection matchCollection2 = LfsSameSizeFileMergeRegEx.Matches(output);
				if (matchCollection2.Count == 1)
				{
					LfsPointer[] array = ParseInSameSizeFileMergeMatch(matchCollection2[0]);
					if (array != null)
					{
						return array;
					}
				}
			}
			MatchCollection matchCollection3 = LfsFileDiffRegEx.Matches(output);
			if (matchCollection3.Count == 1)
			{
				LfsPointer lfsPointer3 = ParseInDiffMatch(matchCollection3[0]);
				if (lfsPointer3 != null)
				{
					if (matchCollection3[0].Groups[1].Value == "-")
					{
						return new LfsPointer[2] { lfsPointer3, null };
					}
					return new LfsPointer[2] { null, lfsPointer3 };
				}
			}
			if (matchCollection3.Count == 2)
			{
				LfsPointer lfsPointer4 = ParseInDiffMatch(matchCollection3[0]);
				if (lfsPointer4 != null)
				{
					LfsPointer lfsPointer5 = ParseInDiffMatch(matchCollection3[1]);
					if (lfsPointer5 != null)
					{
						return new LfsPointer[2] { lfsPointer4, lfsPointer5 };
					}
				}
			}
			return null;
		}

		private static LfsPointer ParseInDiffMatch(Match match)
		{
			if (match.Groups.Count != 5)
			{
				return null;
			}
			string value = match.Groups[2].Value;
			if (!long.TryParse(match.Groups[4].Value, out var result))
			{
				return null;
			}
			return new LfsPointer(value, result);
		}

		private static LfsPointer ParseInMergeMatch(Match match)
		{
			if (match.Groups.Count != 5)
			{
				return null;
			}
			string value = match.Groups[2].Value;
			if (!long.TryParse(match.Groups[4].Value, out var result))
			{
				return null;
			}
			return new LfsPointer(value, result);
		}

		private static LfsPointer[] ParseInSameSizeFileMergeMatch(Match match)
		{
			if (match.Groups.Count != 4)
			{
				return null;
			}
			string value = match.Groups[1].Value;
			string value2 = match.Groups[2].Value;
			if (!long.TryParse(match.Groups[3].Value, out var result))
			{
				return null;
			}
			return new LfsPointer[2]
			{
				new LfsPointer(value2, result),
				new LfsPointer(value, result)
			};
		}

		protected static bool IsRootReferenceError(GitCommandError error)
		{
			if (!(error is GitCommandError.GitError gitError))
			{
				return false;
			}
			if (gitError.Stderr.Contains("fatal: bad revision") || gitError.Stderr.Contains("unknown revision or path not in the working tree."))
			{
				return true;
			}
			return false;
		}
	}
}
