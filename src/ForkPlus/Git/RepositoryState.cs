namespace ForkPlus.Git
{
	public abstract class RepositoryState
	{
		public class MergeInProgress : RepositoryState
		{
			public Reference Local { get; }

			public Reference Remote { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public MergeInProgress(Reference local, Reference remote, ChangedFile[] unmergedFiles)
			{
				Local = local;
				Remote = remote;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class SequencerInProgress : RepositoryState
		{
		}

		public class SquashInProgress : RepositoryState
		{
			public Reference Head { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public SquashInProgress(Reference head, ChangedFile[] unmergedFiles)
			{
				Head = head;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class RebaseInProgress : RepositoryState
		{
			public Reference Local { get; }

			public Reference Remote { get; }

			public Sha? ActiveSha { get; }

			public int Done { get; }

			public int Total { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public bool Interactive { get; }

			public string AmendSha { get; }

			public RebaseInProgress(Reference local, Reference remote, Sha? activeSha, int done, int total, ChangedFile[] unmergedFiles, bool interactive, string amendSha)
			{
				Local = local;
				Remote = remote;
				ActiveSha = activeSha;
				Done = done;
				Total = total;
				UnmergedFiles = unmergedFiles;
				Interactive = interactive;
				AmendSha = amendSha;
			}
		}

		public class CherryPickInProgress : RepositoryState
		{
			public Reference Head { get; }

			public Reference CherryPickHead { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public CherryPickInProgress(Reference head, Reference cherryPickHead, ChangedFile[] unmergedFiles)
			{
				Head = head;
				CherryPickHead = cherryPickHead;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class RevertInProgress : RepositoryState
		{
			public Sha RevertHead { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public RevertInProgress(Sha revertHead, ChangedFile[] unmergedFiles)
			{
				RevertHead = revertHead;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class UnmergedIndex : RepositoryState
		{
			public Reference Head { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public UnmergedIndex(Reference head, ChangedFile[] unmergedFiles)
			{
				Head = head;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class BisectInProgress : RepositoryState
		{
			public Reference Start { get; }

			public Sha? Sha { get; }

			public BisectInProgress(Reference start, Sha? currentSha)
			{
				Start = start;
				Sha = currentSha;
			}
		}

		public class AmInProgress : RepositoryState
		{
			public Reference Local { get; }

			public int Done { get; }

			public int Total { get; }

			public ChangedFile[] UnmergedFiles { get; }

			public AmInProgress(Reference local, int done, int total, ChangedFile[] unmergedFiles)
			{
				Local = local;
				Done = done;
				Total = total;
				UnmergedFiles = unmergedFiles;
			}
		}

		public class OK : RepositoryState
		{
		}
	}
}
