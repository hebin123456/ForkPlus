namespace ForkPlus.Git
{
	public class FullRevisionDetails
	{
		public RevisionDetails RevisionDetails { get; }

		public ChangedFile[] ChangedFiles { get; }

		public FullRevisionDetails(RevisionDetails revisionDetails, ChangedFile[] changedFiles)
		{
			RevisionDetails = revisionDetails;
			ChangedFiles = changedFiles;
		}
	}
}
