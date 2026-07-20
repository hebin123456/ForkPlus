using System;
using System.Collections.Generic;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetReferencesGitCommand
	{
		public GitCommandResult<ReferenceStorage> Execute(GitModule gitModule, GitConfig gitConfig)
		{
			return BtRequest.Run(() => default(BtReferences), delegate(ref BtReferences x)
			{
				return Bt.bt_get_references(gitModule.GitDir(), skip_tags: false, ref x);
			}, delegate(ref BtReferences x)
			{
				GitCommandResult<(string[], Sha[])> refs = x.GetRefs();
				if (!refs.Succeeded)
				{
					return GitCommandResult<ReferenceStorage>.Failure(refs.Error);
				}
				(string[], Sha[]) result = refs.Result;
				string[] names = result.Item1;
				Sha[] shas = result.Item2;
				int[] source = names.CreateIndex((string a, string b) => string.CompareOrdinal(a, b));
				shas = source.Map((int index) => shas[index]);
				names = source.Map((int index) => names[index]);
				GitCommandResult<(string[], string[])> symrefs = x.GetSymrefs();
				if (!symrefs.Succeeded)
				{
					return GitCommandResult<ReferenceStorage>.Failure(symrefs.Error);
				}
				(string[], string[]) result2 = symrefs.Result;
				string[] item = result2.Item1;
				string[] item2 = result2.Item2;
				ReferenceStorage.UpstreamTrackingReference[] array = gitConfig.ReadUpstreams();
				return GitCommandResult<ReferenceStorage>.Success(ReferenceStorage.New(names, shas, x.hash, new DateTime[0], item, item2, array, HashHelper.GetHashCode(array)));
			}, delegate(ref BtReferences x)
			{
				Bt.bt_release_references(ref x);
			});
		}

		public GitCommandResult<ReferenceStorage> ExecuteOld(GitModule gitModule, bool hideTags = false)
		{
			GitCommand gitCommand = new GitCommand("for-each-ref", "--include-root-refs", "--format=%(objectname)\t%(refname)\t%(HEAD)\t%(upstream)\t%(*objectname)\t%(committerdate:raw)\t%(*committerdate:raw)\t%(symref)");
			if (hideTags)
			{
				gitCommand.Add("--exclude=refs/tags");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				GitCommandError.UnsafeRepository unsafeRepository = GitCommandError.UnsafeRepository.Test(gitRequestResult, gitModule.Path);
				if (unsafeRepository != null)
				{
					return GitCommandResult<ReferenceStorage>.Failure(unsafeRepository);
				}
				return GitCommandResult<ReferenceStorage>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			List<(string, Sha, string, DateTime)> list = new List<(string, Sha, string, DateTime)>(array.Length);
			List<Symref> list2 = new List<Symref>();
			string[] array2 = array;
			foreach (string text in array2)
			{
				string[] array3 = text.Split(Consts.Chars.Tab);
				if (array3.Length != 8)
				{
					Log.Error("Cannot parse reference line " + text);
					continue;
				}
				string text2 = array3[0];
				string text3 = array3[1];
				if (!(text3 == "FETCH_HEAD") && !(text3 == "MERGE_HEAD"))
				{
					string item = ((array3[3].Length > 0) ? array3[3] : null);
					string text4 = array3[4];
					string text5 = array3[7];
					if (!ParseUnixDate(array3[5], out var result) && !ParseUnixDate(array3[6], out result))
					{
						result = DateTimeHelper.UnixStartTime;
					}
					if (text5.Length > 0)
					{
						list2.Add(new Symref(text3, text5));
						continue;
					}
					if (!Sha.TryParse(text2, out var result2))
					{
						Log.Error("Cannot parse reference SHA in '" + text2 + "'");
						continue;
					}
					Sha result3;
					Sha item2 = (text3.StartsWith("refs/heads/") ? result2 : (text3.StartsWith("refs/tags/") ? ((text4.Length <= 0 || !Sha.TryParse(text4, out result3)) ? result2 : result3) : ((!text3.StartsWith("refs/remotes/")) ? result2 : result2)));
					list.Add((text3, item2, item, result));
				}
			}
			list.Sort(((string fullRef, Sha sha, string upstream, DateTime date) a, (string fullRef, Sha sha, string upstream, DateTime date) b) => string.CompareOrdinal(a.fullRef, b.fullRef));
			string[] array4 = new string[list.Count];
			Sha[] array5 = new Sha[list.Count];
			DateTime[] array6 = new DateTime[list.Count];
			List<ReferenceStorage.UpstreamTrackingReference> list3 = new List<ReferenceStorage.UpstreamTrackingReference>();
			for (int j = 0; j < list.Count; j++)
			{
				array4[j] = list[j].Item1;
				array5[j] = list[j].Item2;
				array6[j] = list[j].Item4;
				if (list[j].Item3 != null)
				{
					list3.Add(new ReferenceStorage.UpstreamTrackingReference(list[j].Item1, list[j].Item3));
				}
			}
			string[] array7 = new string[list2.Count];
			string[] array8 = new string[list2.Count];
			for (int k = 0; k < list2.Count; k++)
			{
				array7[k] = list2[k].Name;
				array8[k] = list2[k].Target;
			}
			ReferenceStorage.UpstreamTrackingReference[] upstreamPairs = list3.ToArray();
			return GitCommandResult<ReferenceStorage>.Success(ReferenceStorage.New(array4, array5, 0uL, array6, array7, array8, upstreamPairs, 0));
		}

		private static bool ParseUnixDate(string unixDateString, out DateTime result)
		{
			string[] array = unixDateString.Split(Consts.Chars.Space);
			if (array.Length > 1 && DateTimeHelper.TryParseUnixDate(array[0], out result))
			{
				return true;
			}
			result = default(DateTime);
			return false;
		}
	}
}
