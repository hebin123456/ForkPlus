namespace ForkPlus.Git.Interaction
{
	internal struct FileProgress
	{
		public long DownloadedBytes;

		public long TotalBytes;

		public double Ratio => (double)DownloadedBytes / (double)TotalBytes;
	}
}
