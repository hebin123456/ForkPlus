using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	internal class BlameArgs
	{
		public Sha Sha { get; }

		public string Filepath { get; }

		public BlameArgs(Sha sha, string filepath)
		{
			Sha = sha;
			Filepath = filepath;
		}
	}
}
