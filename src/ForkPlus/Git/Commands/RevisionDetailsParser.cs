using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	public static class RevisionDetailsParser
	{
		public static string FormatString = "%H±.%aN±.%aE±.%at±.%cN±.%cE±.%ct±.%P±.%B";

		private static readonly string[] RevisionDetailsSeparator = new string[1] { "±." };

		[Null]
		public static RevisionDetails Parse(string input)
		{
			string[] array = input.Split(RevisionDetailsSeparator, StringSplitOptions.None);
			if (array.Length <= 8)
			{
				Log.Error("Cannot parse revision details line: '" + input + "'");
				return null;
			}
			if (!DateTimeHelper.TryParseUnixDate(array[3], out var result))
			{
				Log.Error("Cannot parse author date in '" + input + "'");
				return null;
			}
			if (!DateTimeHelper.TryParseUnixDate(array[6], out var result2))
			{
				Log.Error("Cannot parse committer date in '" + input + "'");
				return null;
			}
			string[] array2 = array[7].Split(Consts.Chars.Space);
			List<Sha> list = new List<Sha>(array2.Length);
			string[] array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				if (Sha.TryParse(array3[i], out var result3))
				{
					list.Add(result3);
				}
			}
			Sha.TryParse(array[0], out var result4);
			return new RevisionDetails(result4, new UserIdentity(array[1], array[2]), result, new UserIdentity(array[4], array[5]), result2, list.ToArray(), array[8]);
		}

		public static GitCommandResult<ChangedFile[]> ParseChangedFiles(string input, Submodule[] submodules)
		{
			string[] array = input.Split(Consts.Chars.NewLine);
			List<ChangedFile> list = new List<ChangedFile>(array.Length);
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (!text.StartsWith(":"))
				{
					Log.Warn("Line must start with :");
					continue;
				}
				string[] array3 = text.Split(Consts.Chars.Tab);
				if (array3.Length <= 1)
				{
					return GitCommandResult<ChangedFile[]>.Failure(new GitCommandError.ParseError("Can't find \\t separator in '" + text + "'"));
				}
				string[] array4 = array3[0].Split(Consts.Chars.Space);
				if (array4.Length <= 4)
				{
					return GitCommandResult<ChangedFile[]>.Failure(new GitCommandError.ParseError("Can't parse status in '" + text + "'"));
				}
				StatusType status = StatusTypeHelper.Parse(array4[4][0]);
				if (array3.Length == 3)
				{
					string path = array3[2].TrimEnd(Consts.Chars.NewLine);
					Submodule submodule = IReadOnlyListExtensions.FirstItem(submodules, (Submodule x) => x.Path == path);
					if (submodule != null)
					{
						list.Add(new SubmoduleChangedFile(submodule, path, status, StatusType.None, array3[1]));
					}
					else
					{
						list.Add(new ChangedFile(path, status, StatusType.None, array3[1]));
					}
				}
				else
				{
					string path2 = array3[1].TrimEnd(Consts.Chars.NewLine);
					Submodule submodule2 = IReadOnlyListExtensions.FirstItem(submodules, (Submodule x) => x.Path == path2);
					if (submodule2 != null)
					{
						list.Add(new SubmoduleChangedFile(submodule2, path2, status));
					}
					else
					{
						list.Add(new ChangedFile(path2, status));
					}
				}
			}
			return GitCommandResult<ChangedFile[]>.Success(list.ToArray());
		}
	}
}
