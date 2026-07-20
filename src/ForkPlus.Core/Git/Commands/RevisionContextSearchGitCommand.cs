using ForkPlus.Biturbo;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RevisionContextSearchGitCommand
	{
		public GitCommandResult<Sha[]> Execute(GitModule gitModule, string searchString, Sha[] shas, Sha[] refMatches, JobMonitor monitor)
		{
			Benchmarker benchmarker = new Benchmarker("bt_search_commits '" + searchString + "'");
			BtOid[] btShas = shas.Map((Sha x) => x.ToBtOid());
			BtOid[] btRefMatches = refMatches.Map((Sha x) => x.ToBtOid());
			BtCancellationToken btCancellationToken = Bt.bt_new_cancellation_token();
			monitor.SetCancellationAction(delegate
			{
				Bt.bt_cancel_cancellation_token(ref btCancellationToken);
			});
			GitCommandResult<Sha[]> result = BtRequest.Run(() => default(BtSearchCommitsResult), delegate(ref BtSearchCommitsResult x)
			{
				return Bt.bt_search_commits(gitModule.GitDir(), btShas, btShas.Length, searchString, btRefMatches, btRefMatches.Length, ref btCancellationToken, ref x);
			}, delegate(ref BtSearchCommitsResult x)
			{
				return Into(x);
			}, delegate(ref BtSearchCommitsResult x)
			{
				Bt.bt_release_search_commits(ref x);
			});
			monitor.SetCancellationAction(null);
			Bt.bt_release_cancellation_token(ref btCancellationToken);
			benchmarker.LogElapsed();
			return result;
		}

		private static GitCommandResult<Sha[]> Into(BtSearchCommitsResult btSearchCommitsResult)
		{
			return GitCommandResult<Sha[]>.Success(btSearchCommitsResult.matches.GetStructArray(btSearchCommitsResult.matches_len, (BtOid bt_oid) => bt_oid.ToSha()));
		}
	}
}
