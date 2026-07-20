using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	public static class RevisionParser
	{
		public static string Format = "%H%n%aN%n%aE%n%ct%n%s";

		private static readonly Sha[] _empty = new Sha[0];

		public static RevisionWithFiles ParseRevisionWithFiles(string[] lines, Submodule[] submodules, ref int i)
		{
			Revision revision = ParseRevision(lines, ref i);
			if (revision == null)
			{
				return null;
			}
			List<ChangedFile> list = new List<ChangedFile>();
			while (lines[i] != "")
			{
				ChangedFile changedFile = ReadChangedFile(lines[i], submodules);
				if (changedFile != null)
				{
					list.Add(changedFile);
				}
				i++;
			}
			return new RevisionWithFiles(revision, list.ToArray());
		}

		[Null]
		public static Revision ParseRevision(string[] lines, ref int i)
		{
			int num = 0;
			Sha sha = Sha.NullSha;
			string name = "";
			string email = "";
			DateTime authorDate = DateTimeHelper.UnixStartTime;
			while (i < lines.Length)
			{
				string text = lines[i];
				switch (num)
				{
				case 0:
					if (!(text == ""))
					{
						if (!Sha.TryParse(text, out var result2))
						{
							return null;
						}
						sha = result2;
					}
					break;
				case 1:
					name = text;
					break;
				case 2:
					email = text;
					break;
				case 3:
				{
					if (DateTimeHelper.TryParseUnixDate(text, out var result))
					{
						authorDate = result;
						break;
					}
					Log.Error("Cannot parse author date in '" + text + "'");
					return null;
				}
				case 4:
				{
					string message = text;
					i++;
					return new Revision(sha, new RevisionHeader(new UserIdentity(name, email), authorDate, message, hasBody: false));
				}
				default:
					Log.Error($"Cannot reach here. Invalid part number: '{num}'");
					return null;
				}
				num++;
				i++;
			}
			Log.Error("Cannot reach here. Incomplete revision data");
			return null;
		}

		public static ChangedFile ReadChangedFile(string line, Submodule[] submodules)
		{
			string[] array = line.Split(Consts.Chars.Tab);
			if (array.Length < 2)
			{
				Log.Warn("Cannot parse filepath in '" + line + "'");
				return null;
			}
			string text = array[0];
			if (text.Length <= 0)
			{
				Log.Warn("Cannot parse change type in '" + line + "'");
				return null;
			}
			StatusType status = StatusTypeHelper.Parse(text[0]);
			if (array.Length == 3)
			{
				string path = array[2].TrimEnd(Consts.Chars.NewLine);
				Submodule submodule = IReadOnlyListExtensions.FirstItem(submodules, (Submodule x) => x.Path == path);
				if (submodule != null)
				{
					return new SubmoduleChangedFile(submodule, path, status, StatusType.None, array[1]);
				}
				return new ChangedFile(path, status, StatusType.None, array[1]);
			}
			string path2 = array[1].TrimEnd(Consts.Chars.NewLine);
			Submodule submodule2 = IReadOnlyListExtensions.FirstItem(submodules, (Submodule x) => x.Path == path2);
			if (submodule2 != null)
			{
				return new SubmoduleChangedFile(submodule2, path2, status);
			}
			return new ChangedFile(path2, status);
		}

		public static Sha[] ParseRevisionParents(string line)
		{
			switch (line.Length)
			{
			case 40:
			{
				if (Sha.TryParse(line, out var result))
				{
					return new Sha[1] { result };
				}
				return _empty;
			}
			case 81:
			{
				if (Sha.TryParse(line.Substring(0, 40), out var result2) && Sha.TryParse(line.Substring(41), out var result3))
				{
					return new Sha[2] { result2, result3 };
				}
				return _empty;
			}
			case 0:
				return _empty;
			default:
				return line.Split(Consts.Chars.Space).Map((string parentString) => Sha.Parse(parentString).Value);
			}
		}
	}
}
