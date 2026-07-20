namespace ForkPlus.Git.Commands
{
	public abstract class BlobTarget
	{
		public class Revision : BlobTarget
		{
			public string Revspec { get; }

			public string File { get; }

			public Revision(string revspec, string file)
			{
				Revspec = revspec;
				File = file;
			}
		}

		public class Blob : BlobTarget
		{
			public Sha Sha { get; }

			public Blob(Sha sha)
			{
				Sha = sha;
			}
		}

		public class Unstaged : BlobTarget
		{
			public string File { get; }

			public Unstaged(string file)
			{
				File = file;
			}
		}
	}
}
