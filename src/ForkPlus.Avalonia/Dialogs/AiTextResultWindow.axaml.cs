using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Avalonia.Dialogs;
using ForkPlus.Jobs;
using Markdown.Avalonia;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.2a：Avalonia 版 AiTextResultWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiTextResultWindow.xaml.cs（483 行）：
    //   - public partial class AiTextResultWindow : CustomWindow, ILocalizableControl
    //   - 流式渲染字段：_streamingMarkdown (StringBuilder) / _streamingLock /
    //     _lastStreamingRenderUtc / StreamingRenderIntervalMs (400ms) /
    //     _streamingActive / _pendingStreamingScrollToEnd / _streamingUserAtBottom
    //   - 重试委托：_requestAction (Action<AiTextResultWindow, JobMonitor>) /
    //     _currentMonitor (JobMonitor) / _cachedCss (string)
    //   - 模型下拉框：_modelListLoaded (bool)
    //   - 构造函数：InitializeComponent + PreferencesLocalization.ApplyCurrent + Loaded += AiTextResultWindow_Loaded
    //   - AiTextResultWindow_Loaded：InitializeModelComboBox + ApplyLocalizationToButtons +
    //     InitializeWebView + 首次 RunRequest
    //   - InitializeModelComboBox：先用当前选中模型占位，再后台 ThreadPool 拉取 OpenAiService.ListModels
    //   - ModelComboBox_SelectionChanged：保存 ForkPlusSettings.Default.AiReviewSelectedModel
    //   - ApplyLocalizationToButtons：Retry/Stop/Copy/ModelComboBox ToolTip 翻译
    //   - InitializeWebView：WebView2.EnsureCoreWebView2Async + 禁用右键菜单 +
    //     WebMessageReceived (scroll-at-bottom) + NavigationCompleted (scroll-to-end)
    //   - StartStreaming(title, requestAction)：设置 TitleTextBlock + Title + _requestAction，
    //     若 WebView2 已加载则 RunRequest
    //   - RunRequest：重置流式状态 + StatusTextBlock + StatusProgressBar + BusyIndicator +
    //     StopButton + RetryButton + 创建 JobMonitor + Task.Run 调用 _requestAction
    //   - OnChunk(chunk)：lock 追加 _streamingMarkdown + Dispatcher.Async TryRenderStreamingPreview
    //   - OnSuccess(finalMarkdown)：_streamingActive=false + StopButton.Collapse + RetryButton.Enable +
    //     StatusProgressBar.Collapse + BusyIndicator.Collapse + StatusTextBlock + RenderMarkdown(finalMarkdown)
    //   - OnError(errorMessage)：Dispatcher.Async ShowError
    //   - ShowError：HTML 渲染错误信息到 WebView2
    //   - StopStreamingRender：取消时切换 UI 状态
    //   - TryRenderStreamingPreview：节流 400ms + RenderMarkdown(md, scrollToEnd)
    //   - RenderMarkdown(markdown, scrollToEnd)：Bt.bt_md_to_html 转 HTML + 拼 CSS +
    //     scrollScript + NavigateToString
    //   - CoreWebView2_WebMessageReceived：解析 scroll-at-bottom 消息更新 _streamingUserAtBottom
    //   - ConvertMarkdownToHtml：Bt.bt_md_to_html P/Invoke
    //   - GetCss：从嵌入式资源 ForkPlus.Assets.md-ai-output.css 读取 CSS
    //   - RetryButton_Click / StopButton_Click / CopyButton_Click
    //   - ApplyLocalization（ILocalizableControl）：PreferencesLocalization.ApplyCurrent + ApplyLocalizationToButtons
    //
    // 公共方法签名对照（保留与 WPF 一致，调用方可零改动切换到 Avalonia 版）：
    //   - WPF: public void StartStreaming(string title, Action<AiTextResultWindow, JobMonitor> requestAction)
    //   - WPF: public void OnChunk(string chunk)
    //   - WPF: public void OnSuccess(string finalMarkdown = null)
    //   - WPF: public void OnError(string errorMessage)
    //   - WPF: public void ApplyLocalization()  （ILocalizableControl 接口）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer 替代 WebView2 链路
    //      - viewer.Markdown 直接接收 markdown 字符串
    //      - 无需 Bt.bt_md_to_html / GetCss / CreateHtmlDocument / scrollScript
    //   2. spike 版公共方法体仅 stub（保留签名 + 基本字段，不接 JobMonitor/OpenAiService）
    //      - StartStreaming：仅设 Title + 暂存 _requestAction，不触发 RunRequest
    //      - OnChunk：追加 _streamingMarkdown + 直接刷 viewer.Markdown（不做节流）
    //      - OnSuccess：刷 viewer.Markdown + 切 UI 状态
    //      - OnError：显示错误到 Fallback TextBlock
    //   3. 跳过 InitializeModelComboBox / ModelComboBox_SelectionChanged（spike 无模型下拉框）
    //   4. 跳过 InitializeWebView（无 WebView2）
    //   5. 跳过 TryRenderStreamingPreview 节流（spike 直接刷新，性能留 Phase 5.2b 验证）
    //   6. 跳过 CoreWebView2_WebMessageReceived scroll-at-bottom 协议（无 WebView2）
    //   7. 跳过 ApplyLocalization（ILocalizableControl，Phase 0 抽 ILocalizationService 后接入）
    //   8. 用 Dispatcher.UIThread.Post 替代 WPF Dispatcher.Async
    //   9. 用 Avalonia Application.Current.Clipboard 替代 WPF Clipboard.SetText
    //
    // 本 spike 版暂不迁移（留 Phase 5.2b）：
    //   - OpenAiService.ListModels 后台拉取模型列表（InitializeModelComboBox）
    //   - Retry / Stop 按钮 + 完整 RunRequest 流程（spike 不显示这两个按钮）
    //   - JobMonitor + Task.Run 调用 _requestAction（spike 仅暂存 _requestAction）
    //   - 400ms 节流渲染（TryRenderStreamingPreview）
    //   - scroll-at-bottom 协议（_streamingUserAtBottom 自动跟随）
    //   - ForkPlusSettings.Default.AiReviewSelectedModel 持久化
    //   - PreferencesLocalization / ILocalizableControl
    //   - NotificationCenter.ApplicationThemeChanged 主题切换订阅
    //
    // 本 spike 版验证：
    //   - Markdown.Avalonia.Tight MarkdownScrollViewer 可接收流式 chunk 更新
    //   - GFM 表格 + 代码块 + 链接渲染正常
    //   - 公共方法签名与 WPF 版一致（StartStreaming / OnChunk / OnSuccess / OnError）
    //   - DynamicResource 主题 brush 跟随主题切换
    //   - Copy 按钮可复制 _streamingMarkdown 到剪贴板
    //   - Close 按钮可关闭窗口
    public partial class AiTextResultWindow : CustomWindow
    {
        // 流式渲染相关字段（对照 WPF，spike 保留以维持签名一致）
        private StringBuilder _streamingMarkdown;
        private readonly object _streamingLock = new object();
        private bool _streamingActive;

        // 用户传入的"重试"委托（spike 仅暂存，不调用；Phase 5.2b 接入 RunRequest）
        private Action<AiTextResultWindow, JobMonitor> _requestAction;

        public AiTextResultWindow()
        {
            InitializeComponent();

            // 对照 WPF: Title 默认值（spike 用字面量英文，Phase 5.2b 接入 PreferencesLocalization）
            Title = "AI Result";
            _streamingMarkdown = new StringBuilder();
        }

        /// <summary>
        /// 启动一次 AI 请求。调用方在 requestAction 内调用 OnChunk(chunk) 把流式数据写回。
        /// spike 版：仅设置标题 + 暂存 _requestAction，不触发 RunRequest。
        /// Phase 5.2b 接入 JobMonitor + 后台 Task.Run 调用 _requestAction 后才真正执行请求。
        /// </summary>
        public void StartStreaming(string title, Action<AiTextResultWindow, JobMonitor> requestAction)
        {
            if (TitleTextBlock != null) TitleTextBlock.Text = title;
            Title = title;
            _requestAction = requestAction;

            // spike 版：重置流式状态，标记为 active（不触发后台 Task.Run）
            lock (_streamingLock)
            {
                _streamingMarkdown = new StringBuilder();
            }
            _streamingActive = true;

            if (StatusTextBlock != null) StatusTextBlock.Text = "Queued...";
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = true;

            // Phase 5.2b 在此接入：
            //   _currentMonitor = new JobMonitor();
            //   _currentMonitor.SetCancellationAction(() => Dispatcher.UIThread.Post(StopStreamingRender));
            //   Task.Run(() => _requestAction(this, _currentMonitor));
        }

        /// <summary>
        /// 流式 chunk 回调：追加到缓冲 + 直接刷新 MarkdownScrollViewer。
        /// 对照 WPF: spike 版不做 400ms 节流，直接刷 viewer.Markdown。
        /// Phase 5.2b 接入 TryRenderStreamingPreview 节流以减少渲染压力。
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

            // spike 版：直接在 UI 线程刷新 viewer.Markdown（不做节流）
            Dispatcher.UIThread.Post(() =>
            {
                string md;
                lock (_streamingLock)
                {
                    md = _streamingMarkdown?.ToString() ?? "";
                }
                int lengthSoFar = md.Length;
                RenderMarkdown(md);
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = $"Generating... ({lengthSoFar} chars)";
                }
            });
        }

        /// <summary>
        /// 请求成功完成时调用：渲染最终内容并切到完成态。
        /// 对照 WPF: public void OnSuccess(string finalMarkdown = null)
        /// spike 版：刷 viewer.Markdown + 切 UI 状态（不接 StopButton / RetryButton，spike 不显示）。
        /// </summary>
        public void OnSuccess(string finalMarkdown = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _streamingActive = false;
                if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
                if (StatusTextBlock != null) StatusTextBlock.Text = "Done";

                // 如果调用方给了最终 markdown，覆盖渲染
                if (!string.IsNullOrEmpty(finalMarkdown))
                {
                    lock (_streamingLock)
                    {
                        _streamingMarkdown = new StringBuilder(finalMarkdown);
                    }
                    RenderMarkdown(finalMarkdown);
                }
            });
        }

        /// <summary>
        /// 请求失败时调用：显示错误。
        /// 对照 WPF: public void OnError(string errorMessage)
        /// spike 版：用 TextBlock 占位替代 WPF FallbackUserControl + WebView2 HTML 渲染。
        /// </summary>
        public void OnError(string errorMessage)
        {
            Dispatcher.UIThread.Post(() => ShowError(errorMessage));
        }

        /// <summary>
        /// 应用本地化。对照 WPF: public void ApplyLocalization() (ILocalizableControl)。
        /// spike 版：仅 stub（Phase 0 抽 ILocalizationService 后接入）。
        /// </summary>
        public void ApplyLocalization()
        {
            // Phase 5.2b 接入：PreferencesLocalization.ApplyCurrent(this) + ApplyLocalizationToButtons()
        }

        // spike 版：刷新 MarkdownScrollViewer.Markdown（对照 WPF RenderMarkdown + NavigateToString）
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
                Console.WriteLine($"[AiTextResultWindow] Render markdown failed: {ex.Message}");
                ShowError(ex.Message);
            }
        }

        // 对照 WPF: private void ShowError(string message)
        // spike 版：用 TextBlock 占位替代 WPF FallbackUserControl + WebView2 HTML 渲染
        private void ShowError(string message)
        {
            _streamingActive = false;
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Failed";

            var viewer = this.FindControl<MarkdownScrollViewer>("AiResponseMarkdownViewer");
            if (viewer != null) viewer.IsVisible = false;
            if (AiResponseFallback != null)
            {
                AiResponseFallback.IsVisible = true;
                AiResponseFallback.Text = message ?? "";
            }
        }

        // 对照 WPF: private void StopStreamingRender()
        // spike 版：仅切 UI 状态（Phase 5.2b 接入 StopButton / RetryButton 后再完整迁移）
        private void StopStreamingRender()
        {
            _streamingActive = false;
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Canceled";
        }

        // 对照 WPF: private void CopyButton_Click(object sender, RoutedEventArgs e)
        // spike 版：用 Avalonia TopLevel.GetTopLevel(this).Clipboard 替代 WPF Clipboard.SetText
        // （参照 HexEditor.cs / HexContentControl.axaml.cs 的 spike 约定）
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string md;
            lock (_streamingLock)
            {
                md = _streamingMarkdown?.ToString() ?? "";
            }
            if (string.IsNullOrEmpty(md))
            {
                return;
            }

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(md);
                    if (StatusTextBlock != null) StatusTextBlock.Text = "Copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiTextResultWindow] Copy to clipboard failed: {ex.Message}");
            }
        }

        // spike 版新增：Close 按钮点击处理（WPF 用系统标题栏 Close，spike 显式放一个 Close 按钮）
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
