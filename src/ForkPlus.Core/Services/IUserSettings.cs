using ForkPlus.Git;

namespace ForkPlus.Services
{
	/// <summary>
	/// 用户设置抽象，用于解耦业务层对 ForkPlusSettings.Default 静态单例的直接依赖。
	///
	/// Phase 0.2c：Git/ 目录迁移到 Core 时，发现 27 处 ForkPlusSettings.Default.Xxx 引用。
	/// ForkPlusSettings 本身有 WPF 依赖（WindowLocationState / DiffLayoutMode / WindowState 等），
	/// 不能直接迁入 Core（Phase 0.4 工作）。本接口只抽出 Git/ 用到的 11 个属性，
	/// 让 Core 的 Git/ 代码通过 ServiceLocator.UserSettings.Xxx 访问。
	///
	/// 主工程实现 WpfUserSettings，委托到 ForkPlusSettings.Default。
	/// 后续 Phase 0.4 迁移 ForkPlusSettings 到 Core 后，本接口可考虑与 ForkPlusSettings 合并。
	/// </summary>
	public interface IUserSettings
	{
		/// <summary>当前 UI 语言代码（等价于 ForkPlusSettings.Default.UiLanguage）。</summary>
		string UiLanguage { get; }

		/// <summary>是否输出详细 git 命令日志（等价于 ForkPlusSettings.Default.VerboseGitOutput）。</summary>
		bool VerboseGitOutput { get; }

		/// <summary>是否显示 worktrees（等价于 ForkPlusSettings.Default.ShowWorktrees）。
		/// Phase 0.2c：RefreshRepositoryDataGitCommand 在检测到 worktrees 时会写入此属性。</summary>
		bool ShowWorktrees { get; set; }

		/// <summary>版本列表排序方式（等价于 ForkPlusSettings.Default.RevisionSortOrder）。</summary>
		RevisionSortOrder RevisionSortOrder { get; }

		/// <summary>是否记录方法执行耗时（等价于 ForkPlusSettings.Default.LogElapsedTime）。
		/// Phase 0.2c：Benchmarker 通过此属性决定是否输出耗时日志。</summary>
		bool LogElapsedTime { get; }

		/// <summary>AI 代码审查超时秒数（等价于 ForkPlusSettings.Default.AiReviewTimeoutSeconds）。</summary>
		int AiReviewTimeoutSeconds { get; }

		/// <summary>SSH 私钥路径数组（等价于 ForkPlusSettings.Default.SshKeys）。</summary>
		string[] SshKeys { get; }

		/// <summary>页面参考线位置（等价于 ForkPlusSettings.Default.PageGuideLinePosition）。</summary>
		int PageGuideLinePosition { get; }

		/// <summary>最大提交数（等价于 ForkPlusSettings.Default.MaxCommitCount）。</summary>
		int MaxCommitCount { get; }

		/// <summary>提交标题字数下限（等价于 ForkPlusSettings.Default.CommitSubjectLowLimit）。</summary>
		int CommitSubjectLowLimit { get; }

		/// <summary>提交标题字数上限（等价于 ForkPlusSettings.Default.CommitSubjectHighLimit）。</summary>
		int CommitSubjectHighLimit { get; }

		/// <summary>分页页数（派生：等价于 ForkPlusSettings.Default.MinPagesCount）。</summary>
		int MinPagesCount { get; }
	}
}
