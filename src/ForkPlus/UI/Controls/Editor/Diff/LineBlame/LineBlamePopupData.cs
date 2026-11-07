using ForkPlus.Git;

namespace ForkPlus.UI.Controls.Editor.Diff.LineBlame
{
	internal enum LineBlameUncommittedKind
	{
		None,
		WorkingTree,
		Staged,
		Untracked
	}

	/// <summary>浮窗中一张 blame 卡片对应的行信息（已提交或未提交）。</summary>
	internal sealed class LineBlameLineItem
	{
		public bool IsUncommitted { get; set; }

		public LineBlameUncommittedKind UncommittedKind { get; set; }

		/// <summary>已提交时为对应提交；未提交时为 null。</summary>
		[Null]
		public Revision Revision { get; set; }

		/// <summary>该行在被查询版本中的行号（历史查询使用）。</summary>
		public int Line { get; set; }

		/// <summary>该版本对应的文件路径（重命名时旧一侧使用旧路径）。</summary>
		public string Path { get; set; }

		/// <summary>blame 使用的 git revision spec；null = 工作区。</summary>
		[Null]
		public string RevisionSpec { get; set; }

		/// <summary>该行是否来自新 diff 的旧（左）一侧。</summary>
		public bool OldSide { get; set; }
	}

	/// <summary>一次悬停解析出的浮窗数据。</summary>
	internal sealed class LineBlamePopupData
	{
		public LineBlameLineItem Primary { get; set; }

		/// <summary>另一侧（旧版本/HEAD）信息；null 或与主卡无差异时不展示。</summary>
		[Null]
		public LineBlameLineItem Other { get; set; }

		public bool HasHistory { get; set; }

		[Null]
		public string HistoryPath { get; set; }

		public int HistoryLine { get; set; }

		[Null]
		public Sha? HistoryStartSha { get; set; }

		/// <summary>该文件在工作区中的路径（sha 跳转后定位文件使用）。</summary>
		public string FilePath { get; set; }
	}
}
