using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetConflictFileModificationsGitCommand
	{
		public class ConflictModifications
		{
			public static ConflictModifications Empty => new ConflictModifications(new Revision[0], new Revision[0]);

			public Revision[] SrcRevisions { get; }

			public Revision[] DstRevisions { get; }

			public ConflictModifications(Revision[] srcRevisions, Revision[] dstRevisions)
			{
				SrcRevisions = srcRevisions;
				DstRevisions = dstRevisions;
			}
		}

		public ConflictModifications Execute(GitModule gitModule, RepositoryState repositoryState, [Null] string src, [Null] string dst, string filepath)
		{
			bool flag = false;
			if (repositoryState is RepositoryState.RebaseInProgress { ActiveSha: var activeSha })
			{
				src = activeSha.ToString();
				dst = "HEAD";
				flag = true;
			}
			else if (repositoryState is RepositoryState.CherryPickInProgress cherryPickInProgress)
			{
				src = cherryPickInProgress.CherryPickHead.Sha.ToString();
				dst = "HEAD";
				flag = true;
			}
			else if (repositoryState is RepositoryState.RevertInProgress { RevertHead: var revertHead })
			{
				src = revertHead.ToString();
				dst = "HEAD";
				flag = true;
			}
			if (src == null || dst == null)
			{
				return ConflictModifications.Empty;
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-base", src, dst).Execute();
			if (!gitRequestResult.Success)
			{
				return ConflictModifications.Empty;
			}
			string text = gitRequestResult.Stdout.Trim();
			if (text.Length != 40)
			{
				return ConflictModifications.Empty;
			}
			Revision[] srcRevisions = ((!flag) ? GetRevisions(gitModule, text, src, filepath) : GetRevisions(gitModule, src + "~", src, filepath));
			Revision[] revisions = GetRevisions(gitModule, text, dst, filepath);
			return new ConflictModifications(srcRevisions, revisions);
		}

		private static Revision[] GetRevisions(GitModule gitModule, string mergeBase, string dst, string filepath)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--no-show-signature", "--date-order", "--pretty=format:" + RevisionParser.Format, mergeBase + ".." + dst, "--", filepath.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return new Revision[0];
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine);
			List<Revision> list = new List<Revision>(array.Length / 6);
			int i = 0;
			while (i < array.Length)
			{
				Revision revision = RevisionParser.ParseRevision(array, ref i);
				if (revision != null)
				{
					list.Add(revision);
				}
			}
			return list.ToArray();
		}
	}
}
