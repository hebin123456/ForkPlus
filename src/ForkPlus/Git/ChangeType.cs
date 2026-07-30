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
		Ignored,
		// v3.8.0：未变更文件（用于"显示完整工作目录"模式，与变更文件区分，不显示状态图标）
		Unchanged
	}
}
