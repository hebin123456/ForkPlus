namespace ForkPlus.Git
{
	public abstract class DiffContent
	{
		public ChangedFile ChangedFile { get; }

		public DiffContent(ChangedFile changedFile)
		{
			ChangedFile = changedFile;
		}
	}
}
