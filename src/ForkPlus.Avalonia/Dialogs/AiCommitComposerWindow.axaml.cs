using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Avalonia.Dialogs;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using Markdown.Avalonia;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.3a：Avalonia 版 AiCommitComposerWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiCommitComposerWindow.xaml.cs（542 行）：
    //   - public partial class AiCommitComposerWindow : CustomWindow, ILocalizableControl
    //   - 字段：GitModule _gitModule / bool _amend / ChangedFile[] _stagedFiles
    //           WipCommitPlan _plan / bool _aiRunning / bool _applying
    //           JobMonitor _currentMonitor / bool _modelListLoaded
    //           bool _suppressTextBoxSync（Subject/Body TextBox 切换分组时抑制 TextChanged）
    //   - 构造函数 (GitModule, ChangedFile[] stagedFiles, bool amend)：
    //     InitializeComponent + PreferencesLocalization.ApplyCurrent + Loaded += AiCommitComposerWindow_Loaded
    //   - AiCommitComposerWindow_Loaded: InitializeModelComboBox + ApplyLocalizationToButtons + StartAiRequest
    //   - InitializeModelComboBox: 后台 ThreadPool 拉取 OpenAiService.ListModels
    //   - ModelComboBox_SelectionChanged: 保存 ForkPlusSettings.Default.AiReviewSelectedModel
    //   - ApplyLocalizationToButtons: Retry/Stop/ApplyAll/Cancel 文案翻译
    //   - StartAiRequest:
    //     * 校验 OpenAiService.IsAiReviewConfigured + _stagedFiles.Length > 0
    //     * JobQueue.Add 调 GetWorkingDirectoryFileChangesGitCommand.GetStagedPatch 拿 staged diff
    //     * OpenAiService.GenerateWipCommitSplits(patch, filePaths, gitModule, monitor, onChunk)
    //     * OpenAiService.ParseWipCommitPlan(response.Message, stagedFiles) → WipCommitPlan
    //     * PopulateGroupsListBox 填左侧 ListBox（每组一个 ListBoxItem.Tag = WipCommitGroup）
    //     * GroupsListBox_SelectionChanged → DisplayGroupDetails 写中间 FilesListBox + 右侧 Subject/Body
    //     * SubjectTextBox_TextChanged / BodyTextBox_TextChanged 回写 group.Subject/Body
    //   - ApplyAllButton_Click:
    //     * 校验每个 group.Subject 非空
    //     * AddUndoable("Compose WIP commits") + ComposeWipCommitsGitCommand.Execute
    //     * 成功后 InvalidateAndRefresh(SubDomain.All) + Close
    //   - RetryButton_Click: StartAiRequest 重试
    //   - StopButton_Click: _currentMonitor.Cancel()
    //   - CancelButton_Click: applying 时 cancel，否则 Close
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 构造函数重写：解耦 RepositoryUserControl，改为
    //      (GitModule, string stagedDiff, Action<string>? onAccept, Action? onReject)
    //      - 不再持有 ChangedFile[] stagedFiles / bool amend / RepositoryUserControl
    //      - staged diff 由调用方预先计算后传入（spike 不做 GetStagedPatch）
    //      - onAccept(finalMarkdown) / onReject() 回调由调用方提供
    //   2. 不做多分组 WIP 拆分 UI，spike 版用单一 Markdown 流式渲染区替代
    //      （参考 AiTextResultWindow.axaml.cs 流式模式）
    //   3. 流式渲染：StringBuilder + lock + DispatcherTimer 400ms 节流
    //   4. 公共方法签名：StartStreaming / OnChunk / OnSuccess / OnError（与 AiTextResultWindow 一致）
    //   5. 按钮：Retry / Stop / Accept / Reject
    //      - Retry：重新调用 StartStreaming（用同一 _requestAction）
    //      - Stop：_currentMonitor.Cancel()
    //      - Accept：onAccept?.Invoke(finalMarkdown) + Close
    //      - Reject：onReject?.Invoke() + Close
    //   6. 跳过 InitializeModelComboBox / ModelComboBox_SelectionChanged（spike 无模型下拉框）
    //   7. 跳过 PreferencesLocalization.ApplyCurrent / ApplyLocalization / ILocalizableControl
    //   8. 跳过 AddUndoable / InvalidateAndRefresh（spike 不接 Undo/Redo 栈，由调用方回调里处理）
    //   9. 用 Dispatcher.UIThread.Post 替代 WPF Dispatcher.Async
    //  10. 用 ServiceLocator.Localization.Translate 替代 PreferencesLocalization.Translate
    //
    // 本 spike 版暂不迁移（留 Phase 5.3b 或更后）：
    //   - 多分组 WIP 拆分 UI（GroupsListBox + FilesListBox + SubjectTextBox/BodyTextBox）
    //   - ComposeWipCommitsGitCommand 调用链（spike 版交给调用方在 onAccept 里执行）
    //   - OpenAiService.ListModels 后台拉取（InitializeModelComboBox）
    //   - AddUndoable / InvalidateAndRefresh（spike 不接 Undo/Redo 栈）
    //   - PreferencesLocalization / ILocalizableControl.ApplyLocalization
    //   - NotificationCenter.ApplicationThemeChanged 主题切换订阅
    //
    // 本 spike 版验证：
    //   - Markdown.Avalonia.Tight MarkdownScrollViewer 流式渲染 commit message 正常
    //   - 公共方法签名与 AiTextResultWindow 一致（StartStreaming / OnChunk / OnSuccess / OnError）
    //   - Retry / Stop / Accept / Reject 按钮行为正确
    //   - onAccept / onReject 回调链路工作
    public partial class AiCommitComposerWindow : CustomWindow
    {
        // 对照 WPF: StreamingRenderIntervalMs = 400
        private const int StreamingRenderIntervalMs = 400;

        // 流式渲染字段（对照 AiTextResultWindow.axaml.cs，spike 保留以维持流式模式）
        private StringBuilder _streamingMarkdown;
        private readonly object _streamingLock = new object();
        private bool _streamingActive;
        private bool _streamingDirty;
        private DispatcherTimer? _streamingRenderTimer;

        // 对照 WPF: JobMonitor _currentMonitor
        private JobMonitor? _currentMonitor;

        // 对照 WPF: Action<AiTextResultWindow, JobMonitor> _requestAction（spike 用本类签名）
        private Action<JobMonitor>? _requestAction;

        // spike 版新增：onAccept/onReject 回调（解耦 RepositoryUserControl）
        private readonly Action<string>? _onAccept;
        private readonly Action? _onReject;

        // spike 版新增：暂存最终 markdown（供 Accept 按钮回调）
        private string _finalMarkdown = string.Empty;

        // 对照 WPF: bool _aiRunning / _applying
        private bool _aiRunning;

        // 构造函数（spike 版签名）：
        // 对照 WPF: (GitModule, ChangedFile[] stagedFiles, bool amend)
        // Avalonia: (GitModule, string stagedDiff, Action<string>? onAccept, Action? onReject)
        // - stagedDiff 由调用方预先 GetStagedPatch 计算后传入
        // - onAccept/onReject 回调由调用方提供（spike 不做 ComposeWipCommits）
        public AiCommitComposerWindow(
            GitModule gitModule,
            string stagedDiff,
            Action<string>? onAccept = null,
            Action? onReject = null)
        {
            InitializeComponent();

            _streamingMarkdown = new StringBuilder();
            _onAccept = onAccept;
            _onReject = onReject;

            // 暂存 stagedDiff 供调用方 requestAction 读取（spike 版不直接用，留给 requestAction 闭包）
            StagedDiff = stagedDiff ?? string.Empty;
            GitModule = gitModule;

            // 对照 WPF: TitleTextBlock.Text = PreferencesLocalization.Current("AI Commit Composer")
            Title = Translate("AI Commit Composer");
            if (TitleTextBlock != null) TitleTextBlock.Text = Translate("AI Commit Composer");

            // 节流计时器（对照 WPF TryRenderStreamingPreview 400ms 间隔）
            _streamingRenderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(StreamingRenderIntervalMs)
            };
            _streamingRenderTimer.Tick += StreamingRenderTimer_Tick;

            // spike 版：按钮初始状态
            RetryButton.IsEnabled = false;
            StopButton.IsVisible = false;
            AcceptButton.IsEnabled = false;

            Closed += AiCommitComposerWindow_Closed;
        }

        // spike 版：暴露给调用方 requestAction 闭包使用
        public GitModule GitModule { get; }
        public string StagedDiff { get; }

        /// <summary>
        /// 启动一次 AI 请求。调用方在 requestAction 内调用 OnChunk/OnSuccess/OnError。
        /// 对照 AiTextResultWindow.StartStreaming。
        /// spike 版：真正在后台 Task.Run 调用 _requestAction（不像 AiTextResultWindow 仅 stub）。
        /// </summary>
        public void StartStreaming(string title, Action<JobMonitor>? requestAction)
        {
            if (TitleTextBlock != null) TitleTextBlock.Text = title;
            Title = title;
            _requestAction = requestAction;
            _finalMarkdown = string.Empty;

            // 重置流式状态
            lock (_streamingLock)
            {
                _streamingMarkdown = new StringBuilder();
            }
            _streamingActive = true;
            _streamingDirty = false;

            // 清空 Markdown 渲染区
            var viewer = this.FindControl<MarkdownScrollViewer>("AiResponseMarkdownViewer");
            if (viewer != null)
            {
                viewer.Markdown = string.Empty;
                viewer.IsVisible = true;
            }
            if (AiResponseFallback != null) AiResponseFallback.IsVisible = false;

            // 切 UI 状态：进度条可见 + StatusText 显示 Queued
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = true;
            if (StatusTextBlock != null) StatusTextBlock.Text = Translate("Queued...");
            RetryButton.IsEnabled = false;
            StopButton.IsVisible = true;
            AcceptButton.IsEnabled = false;

            if (_requestAction == null)
            {
                // 没有请求委托：直接显示等待状态（spike 调用方应通过 StartStreaming 注入）
                if (StatusTextBlock != null) StatusTextBlock.Text = Translate("Waiting for request action...");
                return;
            }

            _aiRunning = true;
            _currentMonitor = new JobMonitor();
            _currentMonitor.SetCancellationAction(() => Dispatcher.UIThread.Post(StopStreamingRender));

            // 后台调用 _requestAction（对照 WPF JobQueue.Add）
            Task.Run(() =>
            {
                try
                {
                    _requestAction(_currentMonitor);
                }
                catch (Exception ex)
                {
                    Log.Error("AiCommitComposerWindow requestAction failed", ex);
                    OnError(ex.Message);
                }
            });
        }

        /// <summary>
        /// 流式 chunk 回调：追加到缓冲 + 节流刷新 MarkdownScrollViewer。
        /// 对照 AiTextResultWindow.OnChunk，spike 版加 400ms 节流（DispatcherTimer）。
        /// </summary>
        public void OnChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk) || !_streamingActive)
            {
                return;
            }

            lock (_streamingLock)
            {
                _streamingMarkdown?.Append(chunk);
            }
            _streamingDirty = true;

            int lengthSoFar;
            lock (_streamingLock)
            {
                lengthSoFar = _streamingMarkdown?.Length ?? 0;
            }

            // 启动节流计时器（若未启动）
            Dispatcher.UIThread.Post(() =>
            {
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = FormatCurrent("Generating... ({0} chars)", lengthSoFar);
                }
                if (_streamingRenderTimer != null && !_streamingRenderTimer.IsEnabled)
                {
                    _streamingRenderTimer.Start();
                }
            });
        }

        /// <summary>
        /// 请求成功完成时调用：渲染最终内容并切到完成态。
        /// 对照 AiTextResultWindow.OnSuccess。
        /// </summary>
        public void OnSuccess(string finalMarkdown = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _streamingActive = false;
                StopStreamingTimer();

                string md;
                if (!string.IsNullOrEmpty(finalMarkdown))
                {
                    lock (_streamingLock)
                    {
                        _streamingMarkdown = new StringBuilder(finalMarkdown);
                    }
                    md = finalMarkdown;
                    _finalMarkdown = finalMarkdown;
                }
                else
                {
                    lock (_streamingLock)
                    {
                        md = _streamingMarkdown?.ToString() ?? string.Empty;
                    }
                    _finalMarkdown = md;
                }

                RenderMarkdown(md);

                // 切 UI 状态
                if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
                if (StatusTextBlock != null) StatusTextBlock.Text = Translate("Done");
                RetryButton.IsEnabled = true;
                StopButton.IsVisible = false;
                AcceptButton.IsEnabled = !string.IsNullOrEmpty(_finalMarkdown);

                _aiRunning = false;
                _currentMonitor = null;
            });
        }

        /// <summary>
        /// 请求失败时调用：显示错误。
        /// 对照 AiTextResultWindow.OnError。
        /// </summary>
        public void OnError(string errorMessage)
        {
            Dispatcher.UIThread.Post(() => ShowError(errorMessage));
        }

        // spike 版：节流计时器 Tick 回调，刷新 MarkdownScrollViewer
        // 对照 WPF: TryRenderStreamingPreview
        private void StreamingRenderTimer_Tick(object? sender, EventArgs e)
        {
            StopStreamingTimer();
            if (!_streamingDirty) return;
            _streamingDirty = false;

            string md;
            lock (_streamingLock)
            {
                md = _streamingMarkdown?.ToString() ?? string.Empty;
            }
            RenderMarkdown(md);
        }

        // spike 版：刷新 MarkdownScrollViewer.Markdown（对照 AiTextResultWindow.RenderMarkdown）
        private void RenderMarkdown(string markdown)
        {
            try
            {
                // AvaloniaNameSourceGenerator 无法解析第三方命名空间 md:MarkdownScrollViewer
                // （xmlns:md="https://github.com/whistyun/Markdown.Avalonia.Tight"），故不生成
                // AiResponseMarkdownViewer 字段。这里运行时通过 FindControl 查找控件。
                var viewer = this.FindControl<MarkdownScrollViewer>("AiResponseMarkdownViewer");
                if (viewer != null)
                {
                    viewer.Markdown = markdown;
                    viewer.IsVisible = true;
                }
                if (AiResponseFallback != null) AiResponseFallback.IsVisible = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiCommitComposerWindow] Render markdown failed: {ex.Message}");
                ShowError(ex.Message);
            }
        }

        // 对照 WPF: private void ShowError(string message)
        // spike 版：用 TextBlock 占位替代 WPF FallbackUserControl + WebView2 HTML 渲染
        private void ShowError(string message)
        {
            _streamingActive = false;
            _aiRunning = false;
            _currentMonitor = null;
            StopStreamingTimer();

            if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = Translate("Failed");
            RetryButton.IsEnabled = true;
            StopButton.IsVisible = false;
            AcceptButton.IsEnabled = false;

            var viewer = this.FindControl<MarkdownScrollViewer>("AiResponseMarkdownViewer");
            if (viewer != null) viewer.IsVisible = false;
            if (AiResponseFallback != null)
            {
                AiResponseFallback.IsVisible = true;
                AiResponseFallback.Text = message ?? "";
            }
        }

        // 对照 WPF: private void StopStreamingRender()
        // spike 版：切 UI 状态（用户点 Stop 或 monitor.Cancel 触发）
        private void StopStreamingRender()
        {
            _streamingActive = false;
            _aiRunning = false;
            StopStreamingTimer();

            if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = Translate("Canceled");
            RetryButton.IsEnabled = true;
            StopButton.IsVisible = false;

            // 若已有内容则仍允许 Accept
            string md;
            lock (_streamingLock)
            {
                md = _streamingMarkdown?.ToString() ?? string.Empty;
            }
            AcceptButton.IsEnabled = !string.IsNullOrEmpty(md);
            _finalMarkdown = md;
        }

        private void StopStreamingTimer()
        {
            if (_streamingRenderTimer != null && _streamingRenderTimer.IsEnabled)
            {
                _streamingRenderTimer.Stop();
            }
        }

        // 对照 WPF: RetryButton_Click → StartAiRequest 重试
        // spike 版：用同一 _requestAction 重新 StartStreaming
        public void RetryButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_requestAction == null) return;
            StartStreaming(TitleTextBlock?.Text ?? Title, _requestAction);
        }

        // 对照 WPF: StopButton_Click → _currentMonitor.Cancel()
        public void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentMonitor?.Cancel();
        }

        // spike 版新增：Accept 按钮，调用 onAccept(finalMarkdown) + Close
        public void AcceptButton_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_finalMarkdown))
            {
                return;
            }
            try
            {
                _onAccept?.Invoke(_finalMarkdown);
            }
            catch (Exception ex)
            {
                Log.Error("AiCommitComposerWindow onAccept callback failed", ex);
            }
            Close();
        }

        // spike 版新增：Reject 按钮，调用 onReject() + Close
        public void RejectButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                _onReject?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("AiCommitComposerWindow onReject callback failed", ex);
            }
            Close();
        }

        // 对照 WPF: Closed 事件清理
        private void AiCommitComposerWindow_Closed(object? sender, EventArgs e)
        {
            StopStreamingTimer();
            _streamingActive = false;
            _aiRunning = false;
            _currentMonitor?.Cancel();
        }

        // 对照 WPF: private static string Translate(string text)
        // spike 版：用 ServiceLocator.Localization.Translate 替代 PreferencesLocalization.Translate
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }

        // 对照 WPF: PreferencesLocalization.FormatCurrent
        // spike 版：用 ServiceLocator.Localization.FormatCurrent 替代
        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
