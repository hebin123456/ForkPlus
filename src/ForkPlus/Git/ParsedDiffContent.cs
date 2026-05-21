using ForkPlus.Git.Diff;

namespace ForkPlus.Git
{
	public class ParsedDiffContent : DiffContent
	{
		public GitModule GitModule { get; }

		public ForkPlus.Git.Diff.Diff Diff { get; }

		public int TabWidth { get; }

		public bool EntireFile { get; }

		public ParsedDiffContent(GitModule gitModule, ChangedFile changedFile, ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile)
			: base(changedFile)
		{
			GitModule = gitModule;
			Diff = diff;
			TabWidth = tabWidth;
			EntireFile = entireFile;
		}
	}
}
