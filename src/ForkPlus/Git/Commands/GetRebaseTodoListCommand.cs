using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRebaseTodoListCommand
	{
		private interface IParsedEntry
		{
		}

		private class ParsedCommit : IParsedEntry
		{
			public InteractiveRebaseAction Action { get; }

			public Sha Sha { get; }

			public ParsedCommit(InteractiveRebaseAction action, Sha sha)
			{
				Action = action;
				Sha = sha;
			}
		}

		private class ParsedUpdateRef : IParsedEntry
		{
			public string RefName { get; }

			public ParsedUpdateRef(string refName)
			{
				RefName = refName;
			}
		}

		private static readonly string IRCommentToken = "#";

		private static readonly string TokenSeparator = "#!_";

		private static readonly string LineSeparator = "±!LineSeparator!±";

		public GitCommandResult<InteractiveRebaseTodoListItem[]> Execute(GitModule gitModule, string todoListPath, RepositoryReferences references)
		{
			string[] array = File.ReadAllText(todoListPath).Split(Consts.Chars.NewLine);
			List<IParsedEntry> list = new List<IParsedEntry>();
			List<Sha> list2 = new List<Sha>();
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (string.IsNullOrWhiteSpace(text) || text.StartsWith(IRCommentToken))
				{
					continue;
				}
				InteractiveRebaseAction? interactiveRebaseAction = ParseInteractiveRebaseInstruction(text[0]);
				if (!interactiveRebaseAction.HasValue)
				{
					continue;
				}
				InteractiveRebaseAction valueOrDefault = interactiveRebaseAction.GetValueOrDefault();
				if (valueOrDefault == InteractiveRebaseAction.UpdateRefs)
				{
					int num = text.IndexOf(' ');
					if (num >= 0)
					{
						string refName = text.Substring(num + 1);
						list.Add(new ParsedUpdateRef(refName));
					}
				}
				else if (text.Length > 50 && text.IndexOf(TokenSeparator) != -1)
				{
					int startIndex = text.IndexOf(TokenSeparator) + TokenSeparator.Length;
					string text2 = text.Substring(startIndex, 40);
					if (!Sha.TryParse(text2, out var result))
					{
						Log.Error("Cannot parse SHA in '" + text2 + "'");
						continue;
					}
					list.Add(new ParsedCommit(valueOrDefault, result));
					list2.Add(result);
				}
			}
			return CreateTodoListItems(gitModule, list.ToArray(), list2.ToArray(), references);
		}

		private GitCommandResult<InteractiveRebaseTodoListItem[]> CreateTodoListItems(GitModule gitModule, IParsedEntry[] entries, Sha[] shas, RepositoryReferences references)
		{
			GitCommandResult<Dictionary<Sha, RevisionInfo>> revisionInfoForRebase = GetRevisionInfoForRebase(gitModule, shas);
			if (!revisionInfoForRebase.Succeeded)
			{
				return GitCommandResult<InteractiveRebaseTodoListItem[]>.Failure(revisionInfoForRebase.Error);
			}
			Dictionary<Sha, RevisionInfo> result = revisionInfoForRebase.Result;
			List<InteractiveRebaseTodoListItem> list = new List<InteractiveRebaseTodoListItem>(entries.Length);
			foreach (IParsedEntry parsedEntry in entries)
			{
				if (parsedEntry is ParsedCommit parsedCommit)
				{
					if (result.TryGetValue(parsedCommit.Sha, out var value))
					{
						InteractiveRebaseTodoListItem item = new InteractiveRebaseTodoListItem(parsedCommit.Sha, parsedCommit.Action, value.Author, value.AuthorDate, value.Message, Array.Empty<LocalBranch>());
						list.Add(item);
					}
					continue;
				}
				ParsedUpdateRef updateRef = parsedEntry as ParsedUpdateRef;
				if (updateRef == null)
				{
					continue;
				}
				InteractiveRebaseTodoListItem interactiveRebaseTodoListItem = list.LastOrDefault();
				if (interactiveRebaseTodoListItem != null)
				{
					LocalBranch localBranch = IReadOnlyListExtensions.FirstItem(references.LocalBranches, (LocalBranch x) => x.FullReference == updateRef.RefName);
					if (localBranch != null)
					{
						interactiveRebaseTodoListItem.Refs = interactiveRebaseTodoListItem.Refs.Append(localBranch).ToArray();
					}
				}
			}
			return GitCommandResult<InteractiveRebaseTodoListItem[]>.Success(list.ToArray());
		}

		private GitCommandResult<Dictionary<Sha, RevisionInfo>> GetRevisionInfoForRebase(GitModule gitModule, Sha[] shas)
		{
			GitCommand gitCommand = new GitCommand("log", "--no-show-signature", "--no-walk", "--pretty=format:%H" + TokenSeparator + "%aN" + TokenSeparator + "%aE" + TokenSeparator + "%at" + TokenSeparator + "%B" + LineSeparator);
			gitCommand.AddRange(shas.Map((Sha x) => x.ToString()));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Dictionary<Sha, RevisionInfo>>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(new string[1] { LineSeparator }, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<Sha, RevisionInfo> dictionary = new Dictionary<Sha, RevisionInfo>();
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				RevisionInfo? revisionInfo = ParseRevisionInfo(array2[i].Trim());
				if (revisionInfo.HasValue)
				{
					RevisionInfo valueOrDefault = revisionInfo.GetValueOrDefault();
					dictionary[valueOrDefault.Sha] = valueOrDefault;
				}
			}
			return GitCommandResult<Dictionary<Sha, RevisionInfo>>.Success(dictionary);
		}

		private static RevisionInfo? ParseRevisionInfo(string revisionEntry)
		{
			string[] array = revisionEntry.Split(new string[1] { TokenSeparator }, StringSplitOptions.None);
			if (array.Length < 5)
			{
				Log.Error("Cannot parse revisioninfo in '" + revisionEntry + "'");
				return null;
			}
			if (!Sha.TryParse(array[0], out var result))
			{
				Log.Error("Cannot parse SHA in line '" + revisionEntry + "'");
				return null;
			}
			UserIdentity author = new UserIdentity(array[1], array[2]);
			if (!DateTimeHelper.TryParseUnixDate(array[3], out var result2))
			{
				Log.Error("Cannot parse date in '" + revisionEntry + "'");
				return null;
			}
			string message = array[4];
			return new RevisionInfo(result, author, result2, message);
		}

		private static InteractiveRebaseAction? ParseInteractiveRebaseInstruction(char instruction)
		{
			return instruction switch
			{
				'f' => InteractiveRebaseAction.Fixup, 
				'd' => InteractiveRebaseAction.Drop, 
				's' => InteractiveRebaseAction.Squash, 
				'p' => InteractiveRebaseAction.Pick, 
				'r' => InteractiveRebaseAction.Reword, 
				'e' => InteractiveRebaseAction.Edit, 
				'u' => InteractiveRebaseAction.UpdateRefs, 
				_ => null, 
			};
		}
	}
}
