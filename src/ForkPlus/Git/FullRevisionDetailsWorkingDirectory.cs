namespace ForkPlus.Git
{
	public class FullRevisionDetailsWorkingDirectory : FullRevisionDetails
	{
		public FullRevisionDetailsWorkingDirectory(RevisionDetails revisionDetails, ChangedFile[] changedFiles)
			: base(revisionDetails, changedFiles)
		{
		}
	}
}
