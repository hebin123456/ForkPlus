using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionHeadersGitCommand
	{
		public GitCommandResult<RevisionHeader[]> Execute(GitModule gitModule, Sha[] shas)
		{
			Benchmarker benchmarker = new Benchmarker("bt_get_revision_headers");
			BtOid[] oids = shas.Map((Sha x) => x.ToBtOid());
			GitCommandResult<RevisionHeader[]> result = BtRequest.Run(() => default(BtRevisionHeaders), delegate(ref BtRevisionHeaders x)
			{
				return Bt.bt_get_revision_headers(gitModule.Path, gitModule.GitDir(), oids, oids.Length, ref x);
			}, delegate(ref BtRevisionHeaders x)
			{
				return x.Into();
			}, delegate(ref BtRevisionHeaders x)
			{
				Bt.bt_release_revision_headers(ref x);
			});
			benchmarker.ReportElapsed();
			return result;
		}
	}
}
