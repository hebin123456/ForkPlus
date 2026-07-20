namespace ForkPlus.Git.Merge
{
	public struct ConflictStatus
	{
		public int Resolved { get; }

		public int Total { get; }

		public ConflictStatus(int resolved, int total)
		{
			Resolved = resolved;
			Total = total;
		}
	}
}
