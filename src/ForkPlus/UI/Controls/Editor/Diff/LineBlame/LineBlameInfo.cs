namespace ForkPlus.UI.Controls.Editor.Diff.LineBlame
{
	/// <summary>
	/// 行号 blame 浮窗的数据来源模式。
	/// WorkingTree：工作区 diff（未暂存 / 已暂存 / 未跟踪）；Revision：已提交版本之间的 diff。
	/// </summary>
	public enum LineBlameMode
	{
		WorkingTree,
		Revision,
		Disabled
	}

	/// <summary>
	/// 描述某个 FileDiffControl 中 diff 两侧分别对应哪个 git 版本。
	/// 版本用 git revision spec（完整 sha / "sha~1" / "HEAD"）表达，null 在 WorkingTree
	/// 模式下表示工作区（git blame 不带 sha 参数）。
	/// </summary>
	public sealed class LineBlameInfo
	{
		public LineBlameMode Mode { get; }

		/// <summary>新（右侧/工作区）一侧的 blame 版本；null = 工作区。</summary>
		[Null]
		public string NewRevisionSpec { get; }

		/// <summary>旧（左侧）一侧的 blame 版本；null = HEAD（工作区模式）或父提交由 NewRevisionSpec 推导。</summary>
		[Null]
		public string OldRevisionSpec { get; }

		public bool IsStaged { get; }

		public bool IsUntracked { get; }

		private LineBlameInfo(LineBlameMode mode, [Null] string newRevisionSpec, [Null] string oldRevisionSpec, bool isStaged, bool isUntracked)
		{
			Mode = mode;
			NewRevisionSpec = newRevisionSpec;
			OldRevisionSpec = oldRevisionSpec;
			IsStaged = isStaged;
			IsUntracked = isUntracked;
		}

		public static LineBlameInfo WorkingTree(bool isStaged, bool isUntracked, [Null] string oldRevisionSpec)
		{
			return new LineBlameInfo(LineBlameMode.WorkingTree, null, oldRevisionSpec, isStaged, isUntracked);
		}

		public static LineBlameInfo Revision(string newRevisionSpec, string oldRevisionSpec)
		{
			return new LineBlameInfo(LineBlameMode.Revision, newRevisionSpec, oldRevisionSpec, isStaged: false, isUntracked: false);
		}

		/// <summary>当前 diff 无法映射到单一行号来源（如多重提交合并对比），关闭行号 blame。</summary>
		public static LineBlameInfo Disabled { get; } = new LineBlameInfo(LineBlameMode.Disabled, null, null, isStaged: false, isUntracked: false);
	}
}
