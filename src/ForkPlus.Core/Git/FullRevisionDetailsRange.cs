namespace ForkPlus.Git
{
	public class FullRevisionDetailsRange : FullRevisionDetails
	{
		public RevisionDetails SrcRevisionDetails { get; }

		public FullRevisionDetailsRange(RevisionDetails srcRevisionDetails, RevisionDetails dstRevisionDetails, ChangedFile[] changedFiles)
			: base(dstRevisionDetails, changedFiles)
		{
			SrcRevisionDetails = srcRevisionDetails;
		}
	}
}
