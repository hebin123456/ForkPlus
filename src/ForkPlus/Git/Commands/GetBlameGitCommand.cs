using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetBlameGitCommand
	{
		public class BlameChunk
		{
			public int LineNumber { get; }

			public int LineCount { get; }

			public Revision Revision { get; }

			public BlameChunk(int lineNumber, int lineCount, Revision revision)
			{
				LineNumber = lineNumber;
				LineCount = lineCount;
				Revision = revision;
			}
		}

		private static readonly char[] TrimEmailCharacters = new char[2] { '<', '>' };

		public GitCommandResult<BlameChunk[]> Execute(GitModule gitModule, string filePath, [Null] string sha)
		{
			GitCommand gitCommand = new GitCommand("blame", "--porcelain");
			if (sha != null)
			{
				gitCommand.Add(sha);
			}
			gitCommand.Add("--");
			gitCommand.Add(filePath.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.Contains("fatal: no such path") || gitRequestResult.Stderr.Contains("fatal: bad revision"))
				{
					return GitCommandResult<BlameChunk[]>.Success(new BlameChunk[0]);
				}
				return GitCommandResult<BlameChunk[]>.Failure(new GitCommandError.GitError(gitRequestResult.Stderr));
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, Revision> dictionary = new Dictionary<string, Revision>();
			List<BlameChunk> list = new List<BlameChunk>();
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				string[] array2 = text.Split(Consts.Chars.Space);
				if (array2.Length < 3 || !int.TryParse(array2[2], out var result))
				{
					return GitCommandResult<BlameChunk[]>.Failure(new GitCommandError.ParseError("Can't parse blame header line: '" + text + "'"));
				}
				string key = array2[0];
				Revision revision;
				if (dictionary.TryGetValue(key, out var value))
				{
					revision = value;
					i++;
					if (array[i].StartsWith("filename "))
					{
						i++;
					}
				}
				else
				{
					Revision revision2 = ParseRevisionHeader(array, ref i);
					if (revision2 == null)
					{
						return GitCommandResult<BlameChunk[]>.Failure(new GitCommandError.ParseError("Can't parse blame"));
					}
					dictionary[key] = revision2;
					revision = revision2;
				}
				if (array2.Length >= 4 && int.TryParse(array2[3], out var result2))
				{
					list.Add(new BlameChunk(result, result2, revision));
				}
			}
			return GitCommandResult<BlameChunk[]>.Success(list.ToArray());
		}

		[Null]
		private Revision ParseRevisionHeader(string[] lines, ref int start)
		{
			int i = 0;
			Sha sha = Sha.NullSha;
			string name = "";
			string email = "";
			DateTime authorDate = default(DateTime);
			string message = "";
			for (; start + i < lines.Length; i++)
			{
				string text = lines[start + i];
				switch (i)
				{
				case 0:
				{
					string[] array = text.Split(Consts.Chars.Space);
					if (!Sha.TryParse(array[0], out var result))
					{
						Log.Error("Can not parse SHA in line: '" + array[0] + "'");
						return null;
					}
					sha = result;
					break;
				}
				case 1:
				{
					string text4 = "author ";
					if (!text.StartsWith(text4))
					{
						Log.Error("Unexpected blame prefix in line: '" + text + "'");
						return null;
					}
					name = text.Substring(text4.Length);
					break;
				}
				case 2:
				{
					string text3 = "author-mail ";
					if (!text.StartsWith(text3))
					{
						Log.Error("Unexpected blame prefix in line: '" + text + "'");
						return null;
					}
					email = text.Substring(text3.Length).Trim(TrimEmailCharacters);
					break;
				}
				case 3:
				{
					string text5 = "author-time ";
					if (!text.StartsWith(text5))
					{
						Log.Error("Unexpected blame prefix in line: '" + text + "'");
						return null;
					}
					if (!DateTimeHelper.TryParseUnixDate(text.Substring(text5.Length), out var result2))
					{
						Log.Error("Can't parse date in: '" + text + "'");
						return null;
					}
					authorDate = result2;
					break;
				}
				case 9:
				{
					string text2 = "summary ";
					if (!text.StartsWith(text2))
					{
						Log.Error("Unexpected blame prefix in line: '" + text + "'");
						return null;
					}
					message = text.Substring(text2.Length);
					break;
				}
				default:
					if (text.StartsWith("\t"))
					{
						start += i;
						return new Revision(sha, new RevisionHeader(new UserIdentity(name, email), authorDate, message, hasBody: false));
					}
					break;
				}
			}
			Log.Error("Error in parsing blame. Can't reach here");
			return null;
		}
	}
}
