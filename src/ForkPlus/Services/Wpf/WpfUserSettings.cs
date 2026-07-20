using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// IUserSettings 的 WPF 实现，委托到 ForkPlusSettings.Default。
	///
	/// ForkPlusSettings 有 WPF 依赖（WindowLocationState / DiffLayoutMode 等），暂未迁入 Core。
	/// 本类作为桥接，让 Core 的 Git/ 代码通过 ServiceLocator.UserSettings 访问设置值。
	/// </summary>
	public class WpfUserSettings : IUserSettings
	{
		public string UiLanguage => ForkPlusSettings.Default.UiLanguage;
		public bool VerboseGitOutput => ForkPlusSettings.Default.VerboseGitOutput;
		public bool ShowWorktrees
		{
			get => ForkPlusSettings.Default.ShowWorktrees;
			set => ForkPlusSettings.Default.ShowWorktrees = value;
		}
		public ForkPlus.Git.RevisionSortOrder RevisionSortOrder => ForkPlusSettings.Default.RevisionSortOrder;
		public bool LogElapsedTime => ForkPlusSettings.Default.LogElapsedTime;
		public int AiReviewTimeoutSeconds => ForkPlusSettings.Default.AiReviewTimeoutSeconds;
		public string[] SshKeys => ForkPlusSettings.Default.SshKeys;
		public int PageGuideLinePosition => ForkPlusSettings.Default.PageGuideLinePosition;
		public int MaxCommitCount => ForkPlusSettings.Default.MaxCommitCount;
		public int CommitSubjectLowLimit => ForkPlusSettings.Default.CommitSubjectLowLimit;
		public int CommitSubjectHighLimit => ForkPlusSettings.Default.CommitSubjectHighLimit;
		public int MinPagesCount => ForkPlusSettings.Default.MinPagesCount;
	}
}
