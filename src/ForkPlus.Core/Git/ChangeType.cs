namespace ForkPlus.Git
{
	public enum ChangeType : byte
	{
		Modified,
		Deleted,
		Copied,
		Renamed,
		Added,
		TypeChanged,
		Unmerged,
		Untracked,
		Unknown,
		Ignored
	}
}
