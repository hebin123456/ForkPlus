using System;
using System.Collections.Generic;
using System.Diagnostics;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetStashesGitCommand
	{
		public GitCommandResult<StashRevision[]> Execute(GitModule gitModule)
		{
			Benchmarker benchmarker = new Benchmarker("bt_get_repository_stashes");
			BtRepositoryStashes out_result = default(BtRepositoryStashes);
			BtResult btResult = Bt.bt_get_repository_stashes(gitModule.Path, gitModule.GitDir(), ref out_result);
			if (btResult != 0)
			{
				return GitCommandResult<StashRevision[]>.Failure(btResult.ToGitCommandError());
			}
			UserIdentity[] identities = out_result.identities.GetStructArray(out_result.identities_len, (BtIdentity bt_identity) => new UserIdentity(bt_identity.name.GetUtf8String(), bt_identity.email.GetUtf8String()));
			StashRevision[] structArray = out_result.stashes.GetStructArray(out_result.stashes_len, (BtStash bt_stash) => ToStashRevision(bt_stash, identities));
			Bt.bt_release_repository_stashes(ref out_result);
			benchmarker.ReportElapsed();
			return GitCommandResult<StashRevision[]>.Success(structArray);
		}

		private static StashRevision ToStashRevision(BtStash bt_stash, UserIdentity[] identities)
		{
			Sha sha = bt_stash.oid.ToSha();
			Sha sha2 = bt_stash.first_parent.ToSha();
			return new StashRevision(subject: ToFriendlyName(bt_stash.subject.GetUtf8String()), author: identities[bt_stash.author_index], reflogName: $"stash@{{{bt_stash.reflog_id}}}", authorDate: DateTimeOffset.FromUnixTimeSeconds(bt_stash.author_time).LocalDateTime, sha: sha, parents: new Sha[1] { sha2 });
		}

		private GitCommandResult<StashRevision[]> ExecuteOld(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--format=\"%H%n%P%n%aN%n%aE%n%at%n%gd%n%s\"", "-g", "--first-parent", "-m", "refs/stash", "--").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.Contains("bad revision"))
				{
					Log.Warn("Repository contains no stashes");
				}
				return GitCommandResult<StashRevision[]>.Success(new StashRevision[0]);
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine);
			List<StashRevision> list = new List<StashRevision>(array.Length / 7);
			int num = 0;
			int num2 = 0;
			Sha sha = Sha.NullSha;
			Sha[] array2 = new Sha[0];
			string name = "";
			string email = "";
			DateTime authorDate = DateTimeHelper.UnixStartTime;
			string reflogName = "";
			string text = "";
			foreach (string text2 in array)
			{
				switch (num2)
				{
				case 0:
					if (!(text2 == ""))
					{
						if (!Sha.TryParse(text2, out var result2))
						{
							return GitCommandResult<StashRevision[]>.Failure(new GitCommandError.ParseError("Failed to parse SHA in '" + text2 + "'"));
						}
						sha = result2;
					}
					break;
				case 1:
					array2 = ((text2.Length != 40) ? ((text2.Length <= 40) ? new Sha[0] : new Sha[1] { Sha.Parse(text2.Substring(0, 40)).Value }) : new Sha[1] { Sha.Parse(text2).Value });
					break;
				case 2:
					name = text2;
					break;
				case 3:
					email = text2;
					break;
				case 4:
				{
					if (DateTimeHelper.TryParseUnixDate(text2, out var result))
					{
						authorDate = result;
						break;
					}
					Log.Error("Cannot parse author date in '" + text2 + "'");
					return GitCommandResult<StashRevision[]>.Success(new StashRevision[0]);
				}
				case 5:
					reflogName = text2;
					break;
				case 6:
					text = ToFriendlyName(text2);
					if (array2.Length != 0)
					{
						list.Add(new StashRevision(sha, array2, text, new UserIdentity(name, email), authorDate, reflogName));
					}
					num++;
					num2 = -1;
					break;
				default:
					Log.Error($"Cannot reach here. Invalid part number: '{num2}'.");
					return GitCommandResult<StashRevision[]>.Success(new StashRevision[0]);
				}
				num2++;
			}
			return GitCommandResult<StashRevision[]>.Success(list.ToArray());
		}

		private static string ToFriendlyName(string line)
		{
			if (!line.StartsWith("WIP"))
			{
				int num = line.IndexOf(": ");
				if (num != -1)
				{
					return line.Substring(num + 2);
				}
			}
			return line;
		}

		[Conditional("DEBUG")]
		private static void AssertAreEqual(StashRevision[] current, StashRevision[] old)
		{
			for (int i = 0; i < current.Length; i++)
			{
			}
		}
	}
}
