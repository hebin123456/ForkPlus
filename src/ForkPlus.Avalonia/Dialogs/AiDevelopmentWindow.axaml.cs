using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Git;
using Markdown.Avalonia;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.3a：Avalonia 版 AiDevelopmentWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiDevelopmentWindow.xaml.cs（1906 行）：
    //   - public partial class AiDevelopmentWindow : CustomWindow
    //   - 依赖：RepositoryUserControl _repositoryUserControl + GitModule _gitModule
    //   - 字段：
    //     * Job _activeJob / List<AiFileChange> _fileChanges /
    //       Dictionary<string,string> _lastBeforeContents（撤销支持）
    //     * DispatcherTimer _statusTimer（轮询 JobStatus）
    //     * List<AiSkillEntry> _skillEntries / Queue<string> _pendingRequests /
    //       bool _isProcessing / List<JObject> _conversationHistory（多轮对话）
    //     * MaxHistoryMessages=20 / MaxContextTokenEstimate=6000 /
    //       KeepRecentMessagesOnCompress=6 / bool _isCompressingContext / _modelListLoaded
    //     * StringBuilder _streamingMarkdown / object _streamingLock /
    //       DateTime _lastStreamingRenderUtc / StreamingRenderIntervalMs=400 /
    //       WebView2 _streamingWebView / string _cachedCss
    //   - 构造函数：InitializeComponent + 设置 Title + PreferencesLocalization.Apply +
    //     InputTextBox.TextChanged/PreviewKeyDown += + Loaded += + _statusTimer.Tick += +
    //     LoadSkillList + InitializeModelComboBox + ShowWelcomeMessage
    //   - InitializeModelComboBox：先用当前选中模型占位，再后台 ThreadPool 拉取
    //     OpenAiService.ListModels 替换填充
    //   - ModelComboBox_SelectionChanged：保存 ForkPlusSettings.Default.AiReviewSelectedModel
    //   - OnSourceInitialized：跟随主窗口 WindowState.Maximized
    //   - AiDevelopmentWindow_Loaded：ApplySendMode + UpdateHintText + InputTextBox.Focus
    //   - UpdateHintText / ShowWelcomeMessage（动态构造 Border + TextBlock 加入 MessagePanel）
    //   - InputTextBox_TextChanged / InputTextBox_PreviewKeyDown（Enter/Ctrl+Enter 发送）
    //   - UpdateSendButton / SendButton_Click / SendModeMenuItem_Click / ApplySendMode
    //   - StatusTimer_Tick：轮询 _activeJob.Status（Running/Finished/Canceled）
    //   - UpdateProcessingStatus：AddStatusMessage
    //   - SendRequest：AddUserMessage + 清空输入 + 若 _isProcessing 入队 _pendingRequests，
    //     否则 ProcessRequest
    //   - ProcessRequest：_isProcessing=true + ProgressBar/StopButton.Visible +
    //     _statusTimer.Start + GetCurrentFileContents + CreateStreamingResponseBubble +
    //     _repositoryUserControl.JobQueue.Add(JobFlags.Hidden, delegate(JobMonitor monitor)
    //       { OpenAiService.CreateFromAiReviewSettings + BuildSystemPrompt +
    //         CompressHistoryIfNeeded + aiService.OpenAiRequestStreamingWithRetry(
    //           historySnapshot, systemPrompt, requirement, monitor, onChunk:lock 追加
    //           _streamingMarkdown + Dispatcher.Async TryRenderStreamingPreview) +
    //         记录 _conversationHistory + ParseAiResponse + ApplyFileChanges +
    //         RemoveStreamingResponseBubble 或 FinalizeStreamingResponseBubble +
    //         ShowDiffResults + RefreshRepositoryStatus + FinishRequest })
    //   - FinishRequest：ProgressBar.Collapsed + _statusTimer.Stop + _activeJob=null +
    //     处理 _pendingRequests 队列下一个或 _isProcessing=false
    //   - StopButton_Click：_pendingRequests.Clear + _activeJob.Monitor.Cancel
    //   - UpdateQueueIndicator：SendButton.Content 显示队列数
    //   - RefreshRepositoryStatus：_repositoryUserControl.InvalidateAndRefresh
    //   - GetCss：读嵌入资源 ForkPlus.Assets.md-ai-output.css（缓存）
    //   - ConvertMarkdownToHtml：Bt.bt_md_to_html P/Invoke
    //   - BuildHtmlDocument：拼 CSS + bodyHtml
    //   - RenderMarkdownToWebView：转 HTML + NavigateToString
    //   - TryRenderStreamingPreview：节流 400ms + RenderMarkdownToWebView
    //   - InitializeAiMessageWebViewAsync：EnsureCoreWebView2Async +
    //     PreferredColorScheme + ContextMenuRequested + NavigationCompleted 自动高度
    //     (ExecuteScriptAsync scrollHeight)
    //   - AddUserMessage：动态构造 Border + TextBlock + TextBox 加入 MessagePanel
    //   - CreateStreamingResponseBubble：动态构造 Border + TextBlock + WebView2，
    //     _streamingWebView=webView + InitializeAiMessageWebViewAsync
    //   - RemoveStreamingResponseBubble / FinalizeStreamingResponseBubble
    //   - UndoAiChanges / UndoButton_Click：用 _lastBeforeContents 回写文件
    //   - ClearConversation / ClearConversationButton_Click：清空历史 + MessagePanel.Clear +
    //     ShowWelcomeMessage
    //   - EstimateHistoryTokens / CompressHistoryIfNeeded：超长上下文自动压缩为摘要
    //   - AddAiResponseMessage：动态构造 Border + TextBlock + WebView2 + 渲染 markdown
    //   - AddStatusMessage：动态构造 TextBlock 加入 MessagePanel
    //   - ScrollToEnd：MainScrollViewer.ScrollToEnd
    //   - SaveSkillList / LoadSkillList：ForkPlusSettings.Default.AiDevSkillList JSON 持久化
    //   - BuildSystemPrompt：拼 repo 路径 + ===FILE:=== 格式说明 + skills
    //   - ParseAiResponse：解析 ===FILE:=== + ```code block``` + DELETE 标记
    //   - GetCurrentFileContents / GetRelativePath：读取工作目录文件内容快照
    //   - GetAllowedDirectories / IsPathInAllowedDirectories：路径安全检查
    //   - GetIndexContent / DetectLineEnding / NormalizeLineEndings：git index + 换行符
    //   - ApplyFileChanges：写文件 + 路径安全检查 + 换行符规范化 + git index 对比
    //   - ShowDiffResults：动态构造 Border + DockPanel(UndoButton) + 每文件 TextBlock +
    //     ScrollViewer + GenerateUnifiedDiffText
    //   - GenerateUnifiedDiffText：LCS-based unified diff
    //   - Translate：PreferencesLocalization.Translate
    //
    // 公共方法签名对照（保留与 WPF 一致，调用方可零改动切换到 Avalonia 版）：
    //   - WPF: public AiDevelopmentWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
    //     Avalonia: 同上（RepositoryUserControl 改用 ForkPlus.Avalonia.Views.UserControls.RepositoryUserControl，
    //     GitModule 沿用 ForkPlus.Git.GitModule from ForkPlus.Core）
    //   注：WPF 版除构造函数外其余方法均为 private，无其他 public 方法需对照。
    //
    // WebView2 → Markdown.Avalonia 取舍说明：
    //   WPF 版用 WebView2 渲染 markdown，链路为：
    //     markdown → Bt.bt_md_to_html (native P/Invoke) → HTML + CSS (md-ai-output.css)
    //     → webView.NavigateToString(htmlDocument)
    //   C# ↔ JS 双向通信：
    //     - C# → JS: webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollHeight")
    //       用于 NavigationCompleted 后测量内容高度，回调 C# 调整 webView.Height（自动高度）
    //     - JS → C#: 无（本窗口未用 WebMessageReceived / PostWebMessageAsString 双向消息；
    //       AiCodeReviewWindow 才有 AiDevelopmentWebView_MarkdownRequested 这类 JS→C# 回调）
    //   Avalonia 版改用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer，链路简化为：
    //     markdown → viewer.Markdown = markdown（Markdig 解析 → Avalonia 控件树）
    //   原本的 C# ↔ JS 通信改为简单的 C# 方法直接调用：
    //     - 自动高度：MarkdownScrollViewer 自带 ScrollViewer，内容自动撑开，无需 JS 测高度
    //     - 流式渲染：直接 viewer.Markdown = md（spike 不做节流，Phase 5.3b 再加）
    //     - 主题切换：Markdown.Avalonia 内置 light/dark 检测，无需 CSS @media prefers-color-scheme
    //   spike 阶段无 JS 引擎，所有原 JS 逻辑由 Avalonia 原生控件自动处理。
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer 替代 WebView2 链路
    //      - 单一 viewer 渲染整段对话（user 问题用 blockquote，AI 响应用普通 markdown）
    //      - 无需 Bt.bt_md_to_html / GetCss / BuildHtmlDocument / NavigateToString
    //      - 无需动态构造 Border + WebView2 气泡（spike 用单一 viewer + _conversationMarkdown 累积）
    //   2. spike 版构造函数保留 WPF 签名（RepositoryUserControl + GitModule），但仅暂存字段
    //      不真正调用 _repositoryUserControl.JobQueue / _gitModule.Path（Phase 5.3b 接入）
    //   3. spike 版 ProcessRequest 仅 stub：显示 user 消息 + 假 AI 响应占位，不请求 OpenAiService
    //   4. 跳过 InitializeModelComboBox 后台拉取 OpenAiService.ListModels（spike 下拉为空占位）
    //   5. 跳过 ModelComboBox_SelectionChanged 持久化 ForkPlusSettings（spike 仅 stub）
    //   6. 跳过 StatusTimer_Tick 轮询 JobStatus（spike 无 Job）
    //   7. 跳过 _conversationHistory / CompressHistoryIfNeeded 上下文压缩（spike 仅显示）
    //   8. 跳过 ParseAiResponse / ApplyFileChanges / ShowDiffResults / UndoAiChanges
    //      （spike 不解析 ===FILE:=== 块，不写文件，不显示 diff，不做撤销）
    //   9. 跳过 SendMode Enter/Ctrl+Enter 切换（spike 固定 Enter 发送）
    //  10. 跳过 LoadSkillList / SaveSkillList / BuildSystemPrompt（spike 不加载 skills）
    //  11. 跳过 PreferencesLocalization / ILocalizableControl 翻译
    //  12. 跳过 NotificationCenter.ApplicationThemeChanged 主题切换订阅
    //  13. 跳过 400ms 节流渲染 TryRenderStreamingPreview（spike 直接刷新 viewer.Markdown）
    //  14. 用 Dispatcher.UIThread.Post 替代 WPF Dispatcher.Async / BeginInvoke
    //
    // 本 spike 版暂不迁移（留 Phase 5.3b）：
    //   - OpenAiService.CreateFromAiReviewSettings / OpenAiRequestStreamingWithRetry
    //   - JobQueue / JobMonitor / Job / JobFlags.Hidden
    //   - 多轮对话 _conversationHistory + CompressHistoryIfNeeded
    //   - ParseAiResponse / ApplyFileChanges / ShowDiffResults / UndoAiChanges
    //   - GetAllowedDirectories / IsPathInAllowedDirectories 路径安全
    //   - GetCurrentFileContents / GetIndexContent / DetectLineEnding / NormalizeLineEndings
    //   - InitializeModelComboBox 后台拉取模型列表
    //   - ModelComboBox_SelectionChanged 持久化
    //   - LoadSkillList / SaveSkillList / BuildSystemPrompt
    //   - DispatcherTimer StatusTimer_Tick
    //   - SendMode Enter/Ctrl+Enter 切换
    //   - PreferencesLocalization / ILocalizableControl
    //   - NotificationCenter.ApplicationThemeChanged
    //   - GenerateUnifiedDiffText LCS diff
    //   - 400ms 节流渲染 TryRenderStreamingPreview
    //
    // 本 spike 版验证：
    //   - Markdown.Avalonia.Tight MarkdownScrollViewer 可接收流式 chunk 更新
    //   - 多轮对话场景下 markdown 累积渲染正常
    //   - GFM 表格 + 代码块 + 链接渲染正常
    //   - 公共构造函数签名与 WPF 版一致
    //   - DynamicResource 主题 brush 跟随主题切换
    //   - Send/Stop/Clear 按钮 + InputTextBox 基本交互可用
    public partial class AiDevelopmentWindow : CustomWindow
    {
        // 对照 WPF 依赖字段（spike 保留以维持构造函数签名一致）
        private readonly RepositoryUserControl _repositoryUserControl;
        private readonly GitModule _gitModule;

        // 对照 WPF 流式渲染字段（spike 保留以维持渲染逻辑形状）
        private readonly StringBuilder _conversationMarkdown = new StringBuilder();
        private readonly object _streamingLock = new object();
        private bool _isProcessing;

        /// <summary>
        /// 构造函数。对照 WPF: public AiDevelopmentWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)。
        /// spike 版：仅暂存 _repositoryUserControl + _gitModule + InitializeComponent + ShowWelcomeMessage，
        /// 不调 InitializeModelComboBox / LoadSkillList / PreferencesLocalization.Apply（Phase 5.3b 接入）。
        /// </summary>
        public AiDevelopmentWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
        {
            _repositoryUserControl = repositoryUserControl;
            _gitModule = gitModule;

            InitializeComponent();

            // 对照 WPF: base.Title = PreferencesLocalization.Current("AI-Assisted Development")
            // spike 版用字面量英文（Phase 5.3b 接入 PreferencesLocalization）
            Title = "AI-Assisted Development";

            // 对照 WPF: ShowWelcomeMessage() - 动态构造 Border + TextBlock 加入 MessagePanel
            // spike 版：用单一 MarkdownScrollViewer，欢迎信息作为 markdown 文档首段
            ShowWelcomeMessage();
        }

        // 对照 WPF: private void ShowWelcomeMessage()
        // spike 版：把欢迎信息写入 _conversationMarkdown + 渲染到 viewer
        // （WPF 版动态构造 Border + StackPanel + TextBlock 加入 MessagePanel）
        private void ShowWelcomeMessage()
        {
            lock (_streamingLock)
            {
                _conversationMarkdown.Clear();
                _conversationMarkdown.AppendLine("# AI-Assisted Development");
                _conversationMarkdown.AppendLine();
                _conversationMarkdown.AppendLine("Describe your development requirement below. The AI will analyze your codebase and generate file changes. You can have a continuous conversation - the AI remembers previous context in this session.");
                _conversationMarkdown.AppendLine();
            }
            RenderConversation();
        }

        // 对照 WPF: private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSendButton();
        }

        // 对照 WPF: private void InputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        // spike 版：固定 Enter 发送，Shift+Enter 换行（跳过 Ctrl+Enter 模式，Phase 5.3b 接入 SendMode 切换）
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var modifiers = e.KeyModifiers;
                bool shiftPressed = (modifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
                bool ctrlPressed = (modifiers & KeyModifiers.Control) == KeyModifiers.Control;
                // Enter 发送，Shift+Enter / Ctrl+Enter 换行
                if (!shiftPressed && !ctrlPressed)
                {
                    e.Handled = true;
                    SendRequest();
                }
            }
        }

        // 对照 WPF: private void UpdateSendButton()
        private void UpdateSendButton()
        {
            if (SendButton != null && InputTextBox != null)
            {
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputTextBox.Text);
            }
        }

        // 对照 WPF: private void SendButton_Click(object sender, RoutedEventArgs e)
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendRequest();
        }

        // 对照 WPF: private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        // spike 版：仅 stub（Phase 5.3b 接入 ForkPlusSettings.Default.AiReviewSelectedModel 持久化）
        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Phase 5.3b 接入：
            //   if (ModelComboBox.SelectedItem == null) return;
            //   string selected = (string)ModelComboBox.SelectedItem;
            //   if (string.IsNullOrWhiteSpace(selected)) return;
            //   if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel,
            //       StringComparison.OrdinalIgnoreCase)) return;
            //   ForkPlusSettings.Default.AiReviewSelectedModel = selected;
            //   ForkPlusSettings.Default.Save();
            //   AddStatusMessage("Model switched to: " + selected);
        }

        // 对照 WPF: private void SendRequest()
        // spike 版：AddUserMessage + 清空输入 + ProcessRequest（跳过 _pendingRequests 队列）
        private void SendRequest()
        {
            if (InputTextBox == null) return;
            string requirement = InputTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(requirement))
            {
                return;
            }

            AddUserMessage(requirement);
            InputTextBox.Text = "";
            UpdateSendButton();

            // spike 版：不维护 _pendingRequests 队列，直接 ProcessRequest
            // Phase 5.3b 接入：if (_isProcessing) { _pendingRequests.Enqueue(requirement); return; }
            ProcessRequest(requirement);
        }

        // 对照 WPF: private void ProcessRequest(string requirement)
        // spike 版：仅 stub - 显示"请求中"状态 + 假响应占位，不真正请求 OpenAiService
        // Phase 5.3b 接入：_repositoryUserControl.JobQueue.Add + OpenAiRequestStreamingWithRetry
        private void ProcessRequest(string requirement)
        {
            _isProcessing = true;
            if (ProgressBar != null) ProgressBar.IsVisible = true;
            if (StopButton != null) StopButton.IsVisible = true;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Queued...";

            // spike 版：模拟一个假 AI 响应占位（验证 markdown 渲染链路）
            // Phase 5.3b 替换为真实 OpenAiRequestStreamingWithRetry + onChunk 回调
            Dispatcher.UIThread.Post(() =>
            {
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Generating (spike stub - no real AI request)...";

                // 模拟流式 chunk：分两段写入，验证 _conversationMarkdown 累积渲染
                OnChunk("I would help you with: **" + requirement + "**\n\n");
                OnChunk("> _Phase 5.3a spike stub - real AI response will be wired in Phase 5.3b via `OpenAiRequestStreamingWithRetry`._\n\n");
                OnChunk("```csharp\n// Placeholder for AI-generated code changes\n// File: example.cs\npublic class Example {\n    // TODO: Phase 5.3b\n}\n```\n");

                OnSuccess();
            });
        }

        /// <summary>
        /// 流式 chunk 回调：追加到 _conversationMarkdown + 直接刷新 MarkdownScrollViewer。
        /// 对照 WPF: onChunk delegate(string delta) { lock _streamingLock _streamingMarkdown.Append(delta);
        /// Dispatcher.Async TryRenderStreamingPreview }
        /// spike 版：不做 400ms 节流，直接刷 viewer.Markdown（Phase 5.3b 接入 TryRenderStreamingPreview）。
        /// 注：此方法为 spike 新增（WPF 版 onChunk 是 ProcessRequest 内的 lambda，非 public 方法），
        /// 供 spike ProcessRequest 模拟流式输出调用；Phase 5.3b 改为 OpenAiRequestStreamingWithRetry 的 onChunk 回调。
        /// </summary>
        private void OnChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk) || !_isProcessing)
            {
                return;
            }

            lock (_streamingLock)
            {
                _conversationMarkdown.Append(chunk);
            }

            Dispatcher.UIThread.Post(() =>
            {
                RenderConversation();
                string md;
                lock (_streamingLock)
                {
                    md = _conversationMarkdown.ToString();
                }
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = $"Generating... ({md.Length} chars)";
                }
            });
        }

        /// <summary>
        /// 请求成功完成时调用：渲染最终内容并切到完成态。
        /// 对照 WPF: ProcessRequest 内 result.Succeeded 后的 FinishRequest 调用。
        /// spike 版：切 UI 状态（不接 StopButton / RetryButton，spike 不显示 Retry）。
        /// </summary>
        private void OnSuccess()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isProcessing = false;
                if (ProgressBar != null) ProgressBar.IsVisible = false;
                if (StopButton != null) StopButton.IsVisible = false;
                if (StatusTextBlock != null) StatusTextBlock.Text = "Done (spike stub)";
                RenderConversation();
            });
        }

        // 对照 WPF: private void StopButton_Click(object sender, RoutedEventArgs e)
        // spike 版：仅切 UI 状态（Phase 5.3b 接入 _activeJob.Monitor.Cancel + _pendingRequests.Clear）
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _isProcessing = false;
            if (ProgressBar != null) ProgressBar.IsVisible = false;
            if (StopButton != null) StopButton.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Stopped (spike stub)";

            // Phase 5.3b 接入：
            //   int cleared = _pendingRequests.Count;
            //   _pendingRequests.Clear();
            //   if (_activeJob != null && !_activeJob.Monitor.IsCanceled) _activeJob.Monitor.Cancel();
        }

        // 对照 WPF: private void ClearConversationButton_Click(object sender, RoutedEventArgs e)
        //        + private void ClearConversation()
        // spike 版：清空 _conversationMarkdown + 重新 ShowWelcomeMessage
        private void ClearConversationButton_Click(object sender, RoutedEventArgs e)
        {
            ClearConversation();
        }

        private void ClearConversation()
        {
            lock (_streamingLock)
            {
                _conversationMarkdown.Clear();
            }
            _isProcessing = false;
            if (ProgressBar != null) ProgressBar.IsVisible = false;
            if (StopButton != null) StopButton.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Conversation cleared.";
            ShowWelcomeMessage();
        }

        // 对照 WPF: private void AddUserMessage(string message)
        // spike 版：把 user 消息以 blockquote 形式追加到 _conversationMarkdown
        // （WPF 版动态构造 Border + TextBlock + TextBox 加入 MessagePanel）
        private void AddUserMessage(string message)
        {
            lock (_streamingLock)
            {
                _conversationMarkdown.AppendLine();
                _conversationMarkdown.AppendLine("> **You:**");
                _conversationMarkdown.AppendLine(">");
                // blockquote 内每行都要加 > 前缀
                string[] lines = message.Replace("\r\n", "\n").Split('\n');
                foreach (string line in lines)
                {
                    _conversationMarkdown.Append("> ");
                    _conversationMarkdown.AppendLine(line);
                }
                _conversationMarkdown.AppendLine();
            }
            RenderConversation();
        }

        // 对照 WPF: private void AddStatusMessage(string message, Brush foreground)
        //        + private void ScrollToEnd()
        // spike 版：状态消息写入 StatusTextBlock（WPF 版动态构造 TextBlock 加入 MessagePanel）
        private void AddStatusMessage(string message)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = message;
            }
        }

        // spike 版：刷新 MarkdownScrollViewer.Markdown（对照 WPF RenderMarkdownToWebView + NavigateToString）
        // 注：AvaloniaNameSourceGenerator 无法解析第三方命名空间 md:MarkdownScrollViewer
        // （xmlns:md="https://github.com/whistyun/Markdown.Avalonia.Tight"），故不生成
        // ConversationMarkdownViewer 字段。这里运行时通过 FindControl 查找控件
        // （与 AiTextResultWindow.axaml.cs / GitMmReferenceWindow.axaml.cs 同样约定）。
        private void RenderConversation()
        {
            try
            {
                var viewer = this.FindControl<MarkdownScrollViewer>("ConversationMarkdownViewer");
                if (viewer != null)
                {
                    string md;
                    lock (_streamingLock)
                    {
                        md = _conversationMarkdown.ToString();
                    }
                    viewer.Markdown = md;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiDevelopmentWindow] Render conversation failed: {ex.Message}");
            }
        }
    }
}
