using System;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// IUserSettings 的 Avalonia spike stub 实现。
    ///
    /// 背景：ForkPlusSettings（src/ForkPlus/Settings/ForkPlusSettings.cs，~3552 行）当前
    /// 在 WPF 工程中，依赖 System.Windows.WindowState（用于 WindowLocationState），
    /// 不能直接被 ForkPlus.Avalonia 引用（会破坏跨平台目标）。
    /// Phase 0.4 会把 ForkPlusSettings 迁入 Core（替换 WindowState 为自定义枚举）。
    ///
    /// Phase 6.4a 阶段先做 spike stub：所有 17 个属性返回 ForkPlusSettings.Decode 方法
    /// 中定义的默认值（让 Core 的 Git/ 代码能通过 ServiceLocator.UserSettings 访问）。
    /// 不读 settings.json，不持久化——Phase 0.4 + 6.4b 升级为真实实现。
    ///
    /// 对照 WPF 实现 src/ForkPlus/Services/Wpf/WpfUserSettings.cs（全部委托到
    /// ForkPlusSettings.Default.Xxx）。默认值来源：src/ForkPlus/Settings/ForkPlusSettings.cs
    /// 的 Decode(JObject) 方法中 json[key]?.Value&lt;T&gt;() ?? &lt;default&gt; 表达式。
    /// </summary>
    public class AvaloniaUserSettings : IUserSettings
    {
        // ShowWorktrees 是接口中唯一的可写属性。spike stub 用 volatile 字段保存运行时值，
        // 不持久化（重启后回退到默认值 false）。Phase 0.4 升级后委托到 ForkPlusSettings.Default。
        private volatile bool _showWorktrees = false;

        /// <summary>UI 语言代码。默认 "zh-Hans"（ForkPlusSettings.Decode 默认值）。</summary>
        public string UiLanguage => "zh-Hans";

        /// <summary>是否输出详细 git 命令日志。默认 false。</summary>
        public bool VerboseGitOutput => false;

        /// <summary>是否显示 worktrees。默认 false。可写但不持久化（spike stub）。</summary>
        public bool ShowWorktrees
        {
            get => _showWorktrees;
            set => _showWorktrees = value;
        }

        /// <summary>版本列表排序方式。默认 RevisionSortOrder.Date（值 = 1，最新在前）。</summary>
        public RevisionSortOrder RevisionSortOrder => RevisionSortOrder.Date;

        /// <summary>是否记录方法执行耗时。默认 false（ForkPlusSettings.Decode 强制 false）。</summary>
        public bool LogElapsedTime => false;

        /// <summary>AI 代码审查超时秒数。默认 300。</summary>
        public int AiReviewTimeoutSeconds => 300;

        /// <summary>SSH 私钥路径数组。默认空数组（不抛 null，调用方直接 .Length 即可）。</summary>
        public string[] SshKeys => Array.Empty<string>();

        /// <summary>页面参考线位置（提交消息编辑器右侧参考线）。默认 72。</summary>
        public int PageGuideLinePosition => 72;

        /// <summary>最大提交数（拉取历史时一次拉多少 commit）。默认 50000。</summary>
        public int MaxCommitCount => 50000;

        /// <summary>提交标题字数下限。默认 50。</summary>
        public int CommitSubjectLowLimit => 50;

        /// <summary>提交标题字数上限。默认 70。</summary>
        public int CommitSubjectHighLimit => 70;

        /// <summary>分页页数（派生）。等价 ForkPlusSettings.Default.MinPagesCount：
        /// (_maxCommitCount - 1) / 10000 + 1 = (50000 - 1) / 10000 + 1 = 5。</summary>
        public int MinPagesCount => (MaxCommitCount - 1) / 10000 + 1;

        /// <summary>AI 代码审查服务 URL。默认 "https://api.openai.com"。</summary>
        public string AiReviewServiceUrl => "https://api.openai.com";

        /// <summary>AI 代码审查 API Key。默认 ""（未配置）。</summary>
        public string AiReviewApiKey => string.Empty;

        /// <summary>AI 代码审查选中的模型。默认 ""（未选择）。</summary>
        public string AiReviewSelectedModel => string.Empty;

        /// <summary>AI 代码审查重试次数。默认 3。</summary>
        public int AiReviewRetryCount => 3;

        /// <summary>提交消息正则表达式。默认 ""（不校验）。</summary>
        public string CommitMessageRegex => string.Empty;
    }
}
