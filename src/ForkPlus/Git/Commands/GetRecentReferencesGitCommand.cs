using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRecentReferencesGitCommand
	{
		public class RecentReference
		{
			public Reference Reference { get; }

			public UserIdentity Committer { get; }

			public DateTime CommitterDate { get; }

			public string RevisionSubject { get; }

			public RecentReference(Reference reference, UserIdentity committer, DateTime committerDate, string revisionSubject)
			{
				Reference = reference;
				Committer = committer;
				CommitterDate = committerDate;
				RevisionSubject = revisionSubject;
			}
		}

		public class RecentReferences
		{
			public RecentReference[] RemoteBranches { get; }

			public RecentReference[] Tags { get; }

			public RecentReferences(RecentReference[] remoteBranches, RecentReference[] tags)
			{
				RemoteBranches = remoteBranches;
				Tags = tags;
			}
		}

		public GitCommandResult<RecentReferences> Execute(GitModule gitModule)
		{
			GitCommandResult<RecentReference[]> allReferences = GetAllReferences(gitModule);
			if (!allReferences.Succeeded)
			{
				return GitCommandResult<RecentReferences>.Failure(allReferences.Error);
			}
			List<RecentReference> list = new List<RecentReference>(5);
			List<RecentReference> list2 = new List<RecentReference>(5);
			foreach (RecentReference item in allReferences.Result.OrderByDescending((RecentReference x) => x.CommitterDate))
			{
				if (list.Count < 5 && item.Reference is RemoteBranch)
				{
					list.Add(item);
				}
				else if (list2.Count < 5 && item.Reference is Tag)
				{
					list2.Add(item);
				}
			}
			return GitCommandResult<RecentReferences>.Success(new RecentReferences(list.ToArray(), list2.ToArray()));
		}

		public GitCommandResult<RecentReference[]> GetAllReferences(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("for-each-ref", "--format=\"%(objectname)\t%(refname)\t%(HEAD)\t%(upstream)\t%(*objectname)\t%(committerdate:raw)\t%(*committerdate:raw)\t%(committername)\t%(*committername)\t%(committeremail)\t%(*committeremail)\t%(contents:subject)\"").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<RecentReference[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			List<RecentReference> list = new List<RecentReference>(array.Length);
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(Consts.Chars.Tab);
				if (array3.Length != 12)
				{
					Log.Error("Cannot parse reference line " + text);
					continue;
				}
				string shaString = array3[0];
				string text2 = array3[1];
				string text3 = array3[2];
				string upstream = ((array3[3].Length > 0) ? array3[3] : null);
				string dereferencedShaString = ((array3[4].Length > 0) ? array3[4] : null);
				if (!Sha.TryParse(shaString, out var result))
				{
					Log.Warn("Cannot parse ref SHA in '" + text + "'");
					continue;
				}
				if (!ParseUnixDate(array3[5], out var result2) && !ParseUnixDate(array3[6], out result2))
				{
					Log.Warn("Cannot parse committer date in '" + text + "'");
					continue;
				}
				string text4 = array3[7];
				if (string.IsNullOrEmpty(text4))
				{
					text4 = array3[8];
					if (string.IsNullOrEmpty(text4))
					{
						Log.Warn("Cannot parse author in '" + text + "'");
						continue;
					}
				}
				string text5 = array3[9].Trim('<', '>');
				if (string.IsNullOrEmpty(text5))
				{
					text5 = array3[10].Trim('<', '>');
					if (string.IsNullOrEmpty(text5))
					{
						Log.Warn("Cannot parse author email in '" + text + "'");
						continue;
					}
				}
				string revisionSubject = array3[11];
				Reference reference = Reference.Create(result, text3 == "*", text2, upstream, dereferencedShaString, result2);
				if (reference == null)
				{
					if (text2 != "refs/stash")
					{
						Log.Error("Cannot parse reference " + text2);
					}
				}
				else
				{
					list.Add(new RecentReference(reference, new UserIdentity(text4, text5), result2, revisionSubject));
				}
			}
			return GitCommandResult<RecentReference[]>.Success(list.ToArray());
		}

		private static bool ParseUnixDate(string unixDateString, out DateTime result)
		{
			string[] array = unixDateString.Split(Consts.Chars.SpaceChar);
			if (array.Length > 1 && DateTimeHelper.TryParseUnixDate(array[0], out result))
			{
				return true;
			}
			result = DateTime.MinValue;
			return false;
		}
	}
}
