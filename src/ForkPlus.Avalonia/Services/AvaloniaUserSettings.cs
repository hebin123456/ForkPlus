using System;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// IUserSettings 的 Avalonia 真实实现（Phase 0.4 / 6.4b 升级）。
    ///
    /// Phase 0.4 已把 ForkPlusSettings 从 WPF 迁入 Core（替换 System.Windows.WindowState
    /// 为跨平台 ForkPlus.UI.WindowState 枚举），Avalonia 工程可以直接引用。
    /// 本实现把所有 17 个属性委托到 ForkPlusSettings.Default.Xxx，
    /// 与 WPF 实现 src/ForkPlus/Services/Wpf/WpfUserSettings.cs 行为完全一致：
    ///   - 读 settings.json（ForkPlusSettings.Default.Load 在首次访问时触发）
    ///   - 写 ShowWorktrees 持久化（ForkPlusSettings.Default.Save）
    ///
    /// 对照 spike stub（Phase 6.4a）：所有属性硬编码默认值，不读 settings.json。
    /// </summary>
    public class AvaloniaUserSettings : IUserSettings
    {
        /// <summary>UI 语言代码。委托到 ForkPlusSettings.Default.UiLanguage。</summary>
        public string UiLanguage => ForkPlusSettings.Default.UiLanguage;

        /// <summary>是否输出详细 git 命令日志。</summary>
        public bool VerboseGitOutput => ForkPlusSettings.Default.VerboseGitOutput;

        /// <summary>是否显示 worktrees。可写，委托到 ForkPlusSettings.Default.ShowWorktrees（持久化）。</summary>
        public bool ShowWorktrees
        {
            get => ForkPlusSettings.Default.ShowWorktrees;
            set => ForkPlusSettings.Default.ShowWorktrees = value;
        }

        /// <summary>版本列表排序方式。</summary>
        public RevisionSortOrder RevisionSortOrder => ForkPlusSettings.Default.RevisionSortOrder;

        /// <summary>是否记录方法执行耗时。</summary>
        public bool LogElapsedTime => ForkPlusSettings.Default.LogElapsedTime;

        /// <summary>AI 代码审查超时秒数。</summary>
        public int AiReviewTimeoutSeconds => ForkPlusSettings.Default.AiReviewTimeoutSeconds;

        /// <summary>SSH 私钥路径数组。ForkPlusSettings 已保证非 null。</summary>
        public string[] SshKeys => ForkPlusSettings.Default.SshKeys ?? Array.Empty<string>();

        /// <summary>页面参考线位置（提交消息编辑器右侧参考线）。</summary>
        public int PageGuideLinePosition => ForkPlusSettings.Default.PageGuideLinePosition;

        /// <summary>最大提交数（拉取历史时一次拉多少 commit）。</summary>
        public int MaxCommitCount => ForkPlusSettings.Default.MaxCommitCount;

        /// <summary>提交标题字数下限。</summary>
        public int CommitSubjectLowLimit => ForkPlusSettings.Default.CommitSubjectLowLimit;

        /// <summary>提交标题字数上限。</summary>
        public int CommitSubjectHighLimit => ForkPlusSettings.Default.CommitSubjectHighLimit;

        /// <summary>分页页数（派生）。委托到 ForkPlusSettings.Default.MinPagesCount。</summary>
        public int MinPagesCount => ForkPlusSettings.Default.MinPagesCount;

        /// <summary>AI 代码审查服务 URL。</summary>
        public string AiReviewServiceUrl => ForkPlusSettings.Default.AiReviewServiceUrl;

        /// <summary>AI 代码审查 API Key。</summary>
        public string AiReviewApiKey => ForkPlusSettings.Default.AiReviewApiKey;

        /// <summary>AI 代码审查选中的模型。</summary>
        public string AiReviewSelectedModel => ForkPlusSettings.Default.AiReviewSelectedModel;

        /// <summary>AI 代码审查重试次数。</summary>
        public int AiReviewRetryCount => ForkPlusSettings.Default.AiReviewRetryCount;

        /// <summary>提交消息正则表达式。</summary>
        public string CommitMessageRegex => ForkPlusSettings.Default.CommitMessageRegex;
    }
}
