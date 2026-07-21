using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using Markdown.Avalonia;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.4a：Avalonia 版 AiCodeReviewWindow（spike 骨架版，3 个 AI 窗口中最复杂的一个）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiCodeReviewWindow.xaml.cs（1758 行）：
    //   - public partial class AiCodeReviewWindow : CustomWindow, ILocalizableControl
    //   - 私有嵌套类 AiReviewSuggestion { File, Line, Comment, OldText, NewText }
    //   - 依赖字段：
    //     * RepositoryUserControl _repositoryUserControl
    //     * AiCodeReviewTarget _target
    //     * AiAgent _aiAgent
    //     * Job _aiReviewJob / Job _fileReviewDiffJob
    //     * AiCodeReviewTarget.Files _fileReviewTarget
    //     * List<AiReviewSuggestion> _suggestions
    //     * string _aiReviewMarkdown / _aiReviewHtml / _selectedFileReviewPath / _aiReviewStatusMessage
    //     * Dictionary<string,string> _fileReviewHtmlCache
    //     * bool _startUpFinished / _isClosed / _modelListLoaded
    //     * StringBuilder _streamingMarkdown / object _streamingLock
    //     * DateTime _lastStreamingRenderUtc / const int StreamingRenderIntervalMs = 400
    //     * bool _streamingActive / _pendingStreamingScrollToEnd / _streamingUserAtBottom
    //     * string _cachedCss
    //   - 构造函数 (RepositoryUserControl, AiCodeReviewTarget, AiAgent)：
    //     InitializeComponent + ApplyLocalization + Loaded += InitializeWebView +
    //     SizeChanged += Window_SizeChanged + Activated += Window_Activated +
    //     RetryAiReview(_target, replaceAll: true) + RestoreAiResultColumnWidth +
    //     按 target 类型初始化 RevisionDetails 或 FileReviewGrid + GridSplitter.DragCompleted 保存列宽 +
    //     NotificationCenter.ApplicationThemeChanged += ApplicationThemeChanged + InitializeModelComboBox
    //   - ApplyLocalization (ILocalizableControl)：Retry/Stop 文案 + ApplyTargetTitleLocalization +
    //     RevisionDetails/FileReviewDiffControl.ApplyLocalization + WebView 重新渲染
    //   - InitializeWebView：WebView2.EnsureCoreWebView2Async + ContextMenuRequested 禁用右键 +
    //     WebMessageReceived (preview-suggestion/apply-suggestion/scroll-at-bottom) +
    //     NavigationCompleted (流式渲染后 scrollTo 底部)
    //   - RetryAiReview：取消旧 job + PrepareAiReviewUi + 按 _aiAgent/OpenAiService 走 ReviewWithAiAgent 或 ReviewWithOpenAi 或 ShowError
    //   - ReviewWithOpenAi / ReviewFilesWithOpenAi / ReviewWithAiAgent：JobQueue.Add + OpenAiService.CodeReview(onChunk) → ConvertMarkdownToHtml → ApplyAiReviewResult
    //   - OnStreamingChunk：lock _streamingMarkdown.Append(chunk) + Dispatcher.Async TryRenderStreamingPreview
    //   - TryRenderStreamingPreview：节流 400ms + ConvertMarkdownToHtml + 注入 scrollScript + NavigateToString
    //   - ApplyAiReviewResult：ClearStatus + ExtractSuggestions + 若 Files target + !replaceAll 合并 → ShowMarkdownOutput
    //   - ShowMarkdownOutput：缓存 _aiReviewMarkdown + _aiReviewHtml → RenderAiReviewOutput
    //   - RenderAiReviewOutput：拼 HTML 文档（CSS + CreateStatusHtml + CreateReviewBodyHtml +
    //     CreateSuggestionsHtml + CreateAllReviewResultsHtml + previewSuggestion/applySuggestion JS） → NavigateToString
    //   - CreateSuggestionsHtml：为每个 suggestion 生成 <div class='ai-suggestion'> +
    //     <button onclick='previewSuggestion(i)'> + <button onclick='applySuggestion(i)'>
    //   - ShowFileDiff：JobQueue.Add(JobFlags.Hidden) + GetWorkingDirectoryFileChangesGitCommand → FileReviewDiffControl.Content = diff
    //   - PreviewSuggestion(i)：AiSuggestionPreviewWindow.ShowDialog → 若 OK ApplySuggestion
    //   - ApplySuggestion(i)：读文件 + 找 OldText + 替换 + 写文件 + InvalidateAndRefresh
    //   - ExtractSuggestions：解析 ```forkplus-ai-suggestions JSON 块
    //   - RemoveSuggestionBlocks：Regex 删除 suggestion JSON 块（不显示给用户）
    //   - ConvertMarkdownToHtml：Bt.bt_md_to_html P/Invoke
    //   - GetCss：读嵌入资源 ForkPlus.Assets.md-ai-output.css（缓存）
    //   - CoreWebView2_WebMessageReceived：解析 preview-suggestion / apply-suggestion / scroll-at-bottom 三种 postMessage
    //     ← 这就是 WPF 版 C#↔JS 双向通信的核心
    //   - InitializeModelComboBox：后台 ThreadPool 拉取 OpenAiService.ListModels
    //   - ModelComboBox_SelectionChanged：保存 ForkPlusSettings.Default.AiReviewSelectedModel
    //   - SendAiReviewCompletedNotification：NotificationManager.SendWindowsNotification
    //   - OnKeyDown (Esc 关闭) + OnClosed (取消 job + Dispose WebView2)
    //
    // 公共方法签名对照（保留与 WPF 一致，调用方可零改动切换到 Avalonia 版）：
    //   - WPF: public AiCodeReviewWindow(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, [Null] AiAgent aiAgent)
    //     Avalonia: 同上（RepositoryUserControl 改用 ForkPlus.Avalonia.Views.UserControls.RepositoryUserControl，
    //     AiCodeReviewTarget 沿用 ForkPlus.UI.Dialogs from ForkPlus.Core，
    //     AiAgent 沿用 ForkPlus.Git.Commands from ForkPlus.Core）
    //   - WPF: public void ApplyLocalization()  （ILocalizableControl 接口）
    //     Avalonia: 同上（spike 仅 stub，Phase 0 抽 ILocalizationService 后接入）
    //   注：WPF 版除构造函数 + ApplyLocalization 外其余方法均为 private，无其他 public 方法需对照。
    //
    // WebView2 → Markdown.Avalonia 取舍说明（C#↔JS 双向通信简化策略，spike 核心）：
    //   WPF 版用 WebView2 渲染 markdown，链路为：
    //     markdown → Bt.bt_md_to_html (native P/Invoke) → HTML + CSS (md-ai-output.css)
    //     → webView.NavigateToString(htmlDocument)
    //   C# ↔ JS 双向通信（WPF 版最复杂的部分，spike 全部简化为 C# 直接调用）：
    //
    //   【JS → C#】CoreWebView2.WebMessageReceived 处理 3 种 postMessage：
    //     1) "preview-suggestion:<i>"  → PreviewSuggestion(i)
    //        WPF: HTML 内 <button onclick='previewSuggestion(i)'> 调 JS 函数
    //             previewSuggestion(i) → postMessage('preview-suggestion:' + i)
    //             → C# CoreWebView2_WebMessageReceived 解析 → PreviewSuggestion(i)
    //        Avalonia spike: 原生 Avalonia <Button Click="PreviewSuggestionButton_Click">
    //                       → C# PreviewSuggestionButton_Click → PreviewSuggestion(i)
    //                       （无 JS 引擎，无 postMessage，无 CoreWebView2_WebMessageReceived）
    //
    //     2) "apply-suggestion:<i>"  → ApplySuggestion(i)
    //        WPF: HTML 内 <button onclick='applySuggestion(i)'> 调 JS 函数
    //             applySuggestion(i) → postMessage('apply-suggestion:' + i)
    //             → C# CoreWebView2_WebMessageReceived 解析 → ApplySuggestion(i)
    //        Avalonia spike: 原生 Avalonia <Button Click="ApplySuggestionButton_Click">
    //                       → C# ApplySuggestionButton_Click → ApplySuggestion(i)
    //
    //     3) "scroll-at-bottom:1|0"  → _streamingUserAtBottom = (value == "1")
    //        WPF: HTML 内 scroll 事件 → sendAtBottom() → postMessage('scroll-at-bottom:' + (1|0))
    //             → C# CoreWebView2_WebMessageReceived 维护 _streamingUserAtBottom
    //             渲染前快照决定 _pendingStreamingScrollToEnd，NavigationCompleted 后 scrollTo 底部
    //        Avalonia spike: 完全删除（MarkdownScrollViewer 自带 ScrollViewer 原生管理滚动位置，
    //                       流式渲染时无需 postMessage 维护用户滚动状态，无竞态）
    //
    //   【C# → JS】ExecuteScriptAsync 调用（WPF 版仅 1 种）：
    //     - ExecuteScriptAsync("window.scrollTo(0, document.documentElement.scrollHeight)")
    //       在 NavigationCompleted 后执行，流式渲染时滚到底部跟随最新内容
    //       Avalonia spike: 删除（spike 阶段不实现自动滚动；Phase 5.4b 接入 ScrollViewer.ScrollToEnd）
    //
    //   【JS 函数注入】HTML 文档内注入的 JS 函数（WPF 版 RenderAiReviewOutput 末尾）：
    //     - previewSuggestion(i)  → postMessage("preview-suggestion:" + i)
    //     - applySuggestion(i)     → postMessage("apply-suggestion:" + i)
    //     - sendAtBottom()         → scroll 事件 → postMessage("scroll-at-bottom:" + (1|0))
    //       Avalonia spike: 全部删除（无 HTML 文档，无 JS 引擎，改用 Avalonia 原生 Button Click 事件）
    //
    //   Avalonia 版改用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer，链路简化为：
    //     markdown → viewer.Markdown = markdown（Markdig 解析 → Avalonia 控件树）
    //     原 HTML 拼接（CreateStatusHtml / CreateReviewBodyHtml / CreateSuggestionsHtml /
    //     CreateAllReviewResultsHtml / scrollScript / previewSuggestion/applySuggestion JS）
    //     全部不再需要，markdown 文档直接用 GFM 写出（# 标题、```diff 代码块、表格等）。
    //     suggestion 操作改用原生 Avalonia Button + ListBox（Click → C# 方法直接调）。
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 完全弃用 WebView2，改用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer
    //      - markdown → viewer.Markdown = markdown（无需 Bt.bt_md_to_html + HTML + CSS）
    //      - 内置 GFM 表格、代码块（含 ```diff）、链接、列表支持
    //      - 内置 light/dark 主题检测（无需 CSS @media prefers-color-scheme）
    //      - 内置 ScrollViewer（无需 WebView2 内部滚动 + scroll-at-bottom postMessage）
    //   2. C#↔JS 双向通信简化为 C# 直接调用（见上方详细对照）
    //   3. spike 版构造函数保留 WPF 签名（RepositoryUserControl + AiCodeReviewTarget + AiAgent），
    //      仅暂存字段 + InitializeComponent + 占位渲染，不真正调 RetryAiReview
    //   4. spike 版用单一 MarkdownScrollViewer 渲染整个检视结果（markdown 字符串累积），
    //      替代 WPF 版 HTML 拼接（CreateStatusHtml/CreateReviewBodyHtml/CreateSuggestionsHtml 等）
    //   5. spike 版用 ListBox 替代 WPF FileListUserControl（spike 不实现树形增量构建算法）
    //   6. spike 版用 ListBox + 原生 Button 替代 WPF HTML 内动态生成 suggestion div + JS 按钮
    //   7. spike 新增 FollowUpTextBox + FollowUpSendButton（WPF 版无输入区）
    //   8. 跳过 InitializeWebView（无 WebView2）
    //   9. 跳过 TryRenderStreamingPreview 节流（spike 直接刷新 viewer.Markdown）
    //  10. 跳过 InitializeModelComboBox 后台拉取 OpenAiService.ListModels（spike 下拉为空占位）
    //  11. 跳过 ModelComboBox_SelectionChanged 持久化 ForkPlusSettings
    //  12. 跳过 SendAiReviewCompletedNotification（NotificationManager 是 WPF-only）
    //  13. 跳过 GridSplitter 列宽持久化
    //  14. 跳过 ApplySuggestion 文件写入 + InvalidateAndRefresh（spike 仅状态栏打印）
    //  15. 跳过 PreviewSuggestion + AiSuggestionPreviewWindow（spike 仅状态栏打印）
    //  16. 跳过 ExtractSuggestions JSON 解析（spike 仅维护空 _suggestions 列表）
    //  17. 跳过 NotificationCenter.ApplicationThemeChanged 主题切换订阅
    //  18. 跳过 PreferencesLocalization / ILocalizableControl 翻译
    //  19. 用 Dispatcher.UIThread.Post 替代 WPF Dispatcher.Async / BeginInvoke
    //
    // 本 spike 版暂不迁移（留 Phase 5.4b 或更后）：
    //   - OpenAiService.CreateFromAiReviewSettings / CodeReview / CodeReviewFiles
    //   - JobQueue / JobMonitor / Job / JobFlags.Hidden
    //   - RepositoryUserControl.JobQueue / InvalidateAndRefresh
    //   - ReviewWithAiAgent / MakeCodeReviewShellCommand
    //   - ReviewWithOpenAi / ReviewFilesWithOpenAi
    //   - BuildFileReviewContext / ReadFileForReview / TrimForPrompt
    //   - GetRangePatchGitCommand / GetWorkingDirectoryFileChangesGitCommand
    //   - ShowFileDiff（spike 不显示 diff，仅在 markdown viewer 显示"diff placeholder"）
    //   - 完整的 OnStreamingChunk + TryRenderStreamingPreview 节流渲染
    //   - ApplySuggestion 文件写入 + InvalidateAndRefresh
    //   - PreviewSuggestion + AiSuggestionPreviewWindow
    //   - ExtractSuggestions + RemoveSuggestionBlocks + MergeSuggestions + MergeFileReviewMarkdown
    //   - ConvertMarkdownToHtml + GetCss + RenderAiReviewOutput HTML 拼接
    //   - CreateStatusHtml / CreateReviewBodyHtml / CreateAllReviewResultsHtml /
    //     CreateSuggestionsHtml / CreateSelectedFileReviewHtml / ExtractFileReviewMarkdown
    //   - TryParseMarkdownHeading / HeadingMatchesReviewFile / NormalizeReviewPath
    //   - InitializeModelComboBox + ModelComboBox_SelectionChanged
    //   - SendAiReviewCompletedNotification
    //   - 窗口状态持久化（OnSourceInitialized / OnLocationChanged / Window_SizeChanged）
    //   - 列宽持久化（RestoreAiResultColumnWidth / SaveAiResultColumnWidth / SaveFileReviewTreeColumnWidth）
    //   - FileReviewFileListUserControl_ContextMenuOpening（右键菜单 retry 单文件）
    //   - ApplicationThemeChanged / UpdateWebViewTheme
    //   - 滚动到底部 _pendingStreamingScrollToEnd / _streamingUserAtBottom
    //
    // 本 spike 版验证：
    //   - Markdown.Avalonia.Tight MarkdownScrollViewer 可接收流式 chunk 更新
    //   - 多 chunk 累积渲染场景下 markdown 文档渲染正常
    //   - GFM 表格 + 代码块（含 ```diff）+ 链接渲染正常
    //   - 主题自动检测 light/dark（DynamicResource 跟随主题切换）
    //   - 整个 WebView2 替代链路工作（markdown → Avalonia 控件树，无浏览器内核）
    //   - C#↔JS 双向通信简化为 C# 直接调用工作（按钮 Click → C# 方法 → stub 处理）
    //   - 公共构造函数签名与 WPF 版一致（调用方可零改动切换到 Avalonia 版）
    //   - 标题栏 / 文件列表 / Markdown 渲染区 / 状态栏 / 建议操作区 / 输入区 布局正确
    public partial class AiCodeReviewWindow : CustomWindow
    {
        // 对照 WPF 私有嵌套类 AiReviewSuggestion（spike 保留以维持数据形状一致）
        private class AiReviewSuggestion
        {
            public string File { get; set; }
            public int Line { get; set; }
            public string Comment { get; set; }
            public string OldText { get; set; }
            public string NewText { get; set; }
        }

        // 对照 WPF 依赖字段（spike 保留以维持构造函数签名一致）
        private readonly RepositoryUserControl _repositoryUserControl;
        private readonly AiCodeReviewTarget _target;
        private readonly AiAgent _aiAgent;

        // 对照 WPF 状态字段（spike 保留以维持渲染逻辑形状）
        private readonly List<AiReviewSuggestion> _suggestions = new List<AiReviewSuggestion>();
        private string _aiReviewMarkdown = "";
        private string _selectedFileReviewPath;

        // 对照 WPF 流式渲染字段（spike 保留以维持流式逻辑形状）
        private readonly StringBuilder _streamingMarkdown = new StringBuilder();
        private readonly object _streamingLock = new object();
        private bool _streamingActive;
        private bool _isClosed;

        /// <summary>
        /// 构造函数。对照 WPF: public AiCodeReviewWindow(RepositoryUserControl repositoryUserControl,
        /// AiCodeReviewTarget target, [Null] AiAgent aiAgent)。
        /// spike 版：仅暂存 _repositoryUserControl + _target + _aiAgent + InitializeComponent +
        /// 占位渲染（ShowWelcomeMessage + InitializeFileReviewList stub），不调 RetryAiReview / InitializeWebView /
        /// InitializeModelComboBox / ApplyLocalization（Phase 5.4b 接入）。
        /// </summary>
        public AiCodeReviewWindow(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, AiAgent aiAgent)
        {
            _repositoryUserControl = repositoryUserControl;
            _target = target;
            _aiAgent = aiAgent;

            InitializeComponent();

            // 对照 WPF: base.Title = PreferencesLocalization.FormatCurrent(...) + ApplyTargetTitleLocalization
            // spike 版用字面量英文（Phase 5.4b 接入 PreferencesLocalization）
            Title = "AI Code Review";
            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "AI Code Review";
            }

            // 对照 WPF: RetryAiReview(_target, replaceAll: true) - spike 版改为 ShowWelcomeMessage 占位
            // （真正 RetryAiReview 在 Phase 5.4b 接入 OpenAiService / JobQueue 后启用）
            ShowWelcomeMessage();

            // 对照 WPF: if (target is AiCodeReviewTarget.Files fileTarget) InitializeFileReviewList(fileTarget)
            // spike 版：用 ListBox 占位填充文件路径列表
            InitializeFileReviewList(target);
        }

        /// <summary>
        /// 应用本地化。对照 WPF: public void ApplyLocalization() (ILocalizableControl)。
        /// spike 版：仅 stub（Phase 0 抽 ILocalizationService 后接入）。
        /// </summary>
        public void ApplyLocalization()
        {
            // Phase 5.4b 接入：
            //   PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
            //   RetryButton.Content = PreferencesLocalization.Current("Retry");
            //   RetryButton.ToolTip = PreferencesLocalization.Current("Retry AI Review");
            //   StopButton.Content = PreferencesLocalization.Current("Stop");
            //   StopButton.ToolTip = PreferencesLocalization.Current("Stop the current AI task and abort its request");
            //   ApplyTargetTitleLocalization();
            //   if (AiResponseWebView?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(_aiReviewHtml))
            //   { _fileReviewHtmlCache.Clear(); RenderAiReviewOutput(); }
        }

        // 对照 WPF: private void RetryButton_Click(object sender, RoutedEventArgs e)
        // spike 版：仅触发 RetryAiReview stub（不真正请求 AI）
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryAiReview(_target, replaceAll: true);
        }

        // 对照 WPF: private void StopButton_Click(object sender, RoutedEventArgs e)
        // spike 版：仅切 UI 状态（Phase 5.4b 接入 _aiReviewJob.Monitor.Cancel）
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Phase 5.4b 接入：_aiReviewJob?.Monitor.Cancel();
            ClearStatus();
            if (StatusTextBlock != null) StatusTextBlock.Text = "Stopped (spike stub)";
            if (RetryButton != null) RetryButton.IsEnabled = true;
        }

        // 对照 WPF: private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        // spike 版：仅 stub（Phase 5.4b 接入 ForkPlusSettings.Default.AiReviewSelectedModel 持久化）
        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Phase 5.4b 接入：
            //   if (ModelComboBox.SelectedItem == null) return;
            //   string selected = (string)ModelComboBox.SelectedItem;
            //   if (string.IsNullOrWhiteSpace(selected)) return;
            //   if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel,
            //       StringComparison.OrdinalIgnoreCase)) return;
            //   ForkPlusSettings.Default.AiReviewSelectedModel = selected;
            //   ForkPlusSettings.Default.Save();
        }

        // 对照 WPF: private void FileReviewFileListUserControl_SelectionChanged(object sender, FileListEventArgs e)
        // spike 版：用 ListBox.SelectionChanged 替代 FileListUserControl.SelectionChanged
        // 选中文件时仅刷新 _selectedFileReviewPath（Phase 5.4b 接入 ShowFileDiff 真正显示 diff）
        private void FileReviewListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileReviewListBox?.SelectedItem is string path)
            {
                _selectedFileReviewPath = path;
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "Selected file: " + path + " (spike stub - diff not loaded)";
                }
            }
        }

        // spike 新增：SuggestionsListBox.SelectionChanged - 选中 suggestion 时启用 Preview/Apply 按钮
        // 对照 WPF：HTML 内 <button onclick='previewSuggestion(i)'> 是按 i 索引直接调；spike 改为 ListBox 选中
        private void SuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = SuggestionsListBox?.SelectedIndex ?? -1;
            bool hasSelection = idx >= 0 && idx < _suggestions.Count;
            if (PreviewSuggestionButton != null) PreviewSuggestionButton.IsEnabled = hasSelection;
            if (ApplySuggestionButton != null) ApplySuggestionButton.IsEnabled = hasSelection;
        }

        // spike 新增：PreviewSuggestionButton_Click → PreviewSuggestion(index)
        // 对照 WPF: C# ↔ JS 双向通信中 JS → C# 的 "preview-suggestion:<i>" postMessage 路径
        //   WPF: HTML <button onclick='previewSuggestion(i)'> → JS postMessage('preview-suggestion:' + i)
        //        → C# CoreWebView2_WebMessageReceived 解析 → PreviewSuggestion(i)
        //   Avalonia spike: <Button Click="PreviewSuggestionButton_Click"> → C# 直接 PreviewSuggestion(i)
        private void PreviewSuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = SuggestionsListBox?.SelectedIndex ?? -1;
            if (idx >= 0)
            {
                PreviewSuggestion(idx);
            }
        }

        // spike 新增：ApplySuggestionButton_Click → ApplySuggestion(index)
        // 对照 WPF: C# ↔ JS 双向通信中 JS → C# 的 "apply-suggestion:<i>" postMessage 路径
        //   WPF: HTML <button onclick='applySuggestion(i)'> → JS postMessage('apply-suggestion:' + i)
        //        → C# CoreWebView2_WebMessageReceived 解析 → ApplySuggestion(i)
        //   Avalonia spike: <Button Click="ApplySuggestionButton_Click"> → C# 直接 ApplySuggestion(i)
        private void ApplySuggestionButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = SuggestionsListBox?.SelectedIndex ?? -1;
            if (idx >= 0)
            {
                ApplySuggestion(idx);
            }
        }

        // spike 新增：FollowUpTextBox KeyDown - Enter 发送，Shift+Enter 换行
        // 对照 AiDevelopmentWindow.axaml.cs 的 InputTextBox_KeyDown 约定
        private void FollowUpTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var modifiers = e.KeyModifiers;
                bool shiftPressed = (modifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
                bool ctrlPressed = (modifiers & KeyModifiers.Control) == KeyModifiers.Control;
                if (!shiftPressed && !ctrlPressed)
                {
                    e.Handled = true;
                    SendFollowUp();
                }
            }
        }

        // spike 新增：FollowUpTextBox TextChanged - 根据输入框内容启用/禁用 Send 按钮
        private void FollowUpTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFollowUpSendButton();
        }

        // spike 新增：FollowUpSendButton_Click → SendFollowUp
        private void FollowUpSendButton_Click(object sender, RoutedEventArgs e)
        {
            SendFollowUp();
        }

        // spike 新增：SendFollowUp - 暂存到状态栏打印（WPF 版无输入区，Phase 5.4b 接入实际功能）
        private void SendFollowUp()
        {
            if (FollowUpTextBox == null) return;
            string text = FollowUpTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = "Follow-up queued (spike stub): " + text;
            }
            FollowUpTextBox.Text = "";
            UpdateFollowUpSendButton();
        }

        // spike 新增：UpdateFollowUpSendButton - 根据输入框内容启用/禁用 Send 按钮
        private void UpdateFollowUpSendButton()
        {
            if (FollowUpSendButton != null && FollowUpTextBox != null)
            {
                FollowUpSendButton.IsEnabled = !string.IsNullOrWhiteSpace(FollowUpTextBox.Text);
            }
        }

        // 对照 WPF: private void RetryAiReview(AiCodeReviewTarget target, bool replaceAll)
        // spike 版：仅 stub - 切 UI 状态 + 占位渲染（不真正请求 AI）
        // Phase 5.4b 接入：取消旧 _aiReviewJob + PrepareAiReviewUi + ReviewWithAiAgent / ReviewWithOpenAi / ReviewFilesWithOpenAi
        private void RetryAiReview(AiCodeReviewTarget target, bool replaceAll)
        {
            if (target == null)
            {
                return;
            }
            PrepareAiReviewUi(replaceAll, target);

            // spike 版：模拟一个假 AI 响应占位（验证 markdown 渲染链路 + C#↔JS 简化）
            // Phase 5.4b 替换为真实 ReviewWithAiAgent / ReviewWithOpenAi / ReviewFilesWithOpenAi
            Dispatcher.UIThread.Post(() =>
            {
                if (_isClosed) return;
                if (StatusTextBlock != null)
                    StatusTextBlock.Text = "Generating (spike stub - no real AI request)...";

                // 模拟流式 chunk：分两段写入，验证 _streamingMarkdown 累积渲染
                OnStreamingChunk("# AI Code Review\n\n");
                OnStreamingChunk("> _Phase 5.4a spike stub - real AI review will be wired in Phase 5.4b via `OpenAiService.CodeReview` / `MakeCodeReviewShellCommand`._\n\n");
                OnStreamingChunk("```diff\n-- placeholder diff (spike) --\n++ diff rendering will be enabled in Phase 5.4b\n```\n\n");
                OnStreamingChunk("**Findings:**\n\n1. (spike placeholder) No real findings yet.\n2. (spike placeholder) Connect OpenAiService in Phase 5.4b.\n");

                ApplyAiReviewResult(target, _streamingMarkdown.ToString(), _streamingMarkdown.ToString(), "", replaceAll);
            });
        }

        // 对照 WPF: private void PrepareAiReviewUi(bool replaceAll, AiCodeReviewTarget target)
        // spike 版：仅切 UI 状态 + 重置流式缓冲
        private void PrepareAiReviewUi(bool replaceAll, AiCodeReviewTarget target)
        {
            if (RetryButton != null) RetryButton.IsEnabled = false;
            if (StopButton != null) StopButton.IsVisible = true;
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = true;
            if (replaceAll)
            {
                if (AiResponseFallback != null) AiResponseFallback.IsVisible = false;
                _suggestions.Clear();
                _aiReviewMarkdown = "";
                lock (_streamingLock)
                {
                    _streamingMarkdown.Clear();
                }
            }
            _streamingActive = true;
            if (StatusTextBlock != null) StatusTextBlock.Text = "Queued...";
        }

        // 对照 WPF: private void OnStreamingChunk(string chunk)
        // spike 版：追加到 _streamingMarkdown + 直接刷新 MarkdownScrollViewer（不做 400ms 节流）
        // Phase 5.4b 接入 TryRenderStreamingPreview 节流以减少渲染压力
        private void OnStreamingChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk) || !_streamingActive)
            {
                return;
            }
            lock (_streamingLock)
            {
                _streamingMarkdown.Append(chunk);
            }
            Dispatcher.UIThread.Post(() =>
            {
                string md;
                lock (_streamingLock)
                {
                    md = _streamingMarkdown.ToString();
                }
                RenderMarkdown(md);
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = $"Generating... ({md.Length} chars)";
                }
            });
        }

        // 对照 WPF: private void StopStreamingRender()
        // spike 版：仅切 UI 状态
        private void StopStreamingRender()
        {
            _streamingActive = false;
        }

        // 对照 WPF: private void ApplyAiReviewResult(AiCodeReviewTarget target, string displayMarkdown, string rawMarkdown, string html, bool replaceAll)
        // spike 版：仅缓存 _aiReviewMarkdown + ClearStatus + ShowMarkdownOutput stub
        // Phase 5.4b 接入：ExtractSuggestions(rawMarkdown) + 若 Files target + !replaceAll 合并 markdown/suggestions
        private void ApplyAiReviewResult(AiCodeReviewTarget target, string displayMarkdown, string rawMarkdown, string html, bool replaceAll)
        {
            ClearStatus();
            // Phase 5.4b 接入：
            //   List<AiReviewSuggestion> newSuggestions = ExtractSuggestions(rawMarkdown);
            //   if (!replaceAll && target is AiCodeReviewTarget.Files files) { ... 合并 ... }
            //   else { _suggestions = newSuggestions; }
            ShowMarkdownOutput(displayMarkdown, html, preserveStatusMessage: !replaceAll);
        }

        // 对照 WPF: private void ShowMarkdownOutput(string markdown, string html, bool preserveStatusMessage = false)
        // spike 版：缓存 _aiReviewMarkdown + RenderAiReviewOutput
        private void ShowMarkdownOutput(string markdown, string html, bool preserveStatusMessage = false)
        {
            _aiReviewMarkdown = markdown ?? "";
            // spike 版：html 参数忽略（Markdown.Avalonia 直接渲染 markdown，无需 HTML）
            RenderAiReviewOutput();
        }

        // 对照 WPF: private void RenderAiReviewOutput()
        // spike 版：直接 viewer.Markdown = _aiReviewMarkdown（无需 HTML 拼接）
        // WPF 版拼 HTML 文档（CSS + CreateStatusHtml + CreateReviewBodyHtml + CreateSuggestionsHtml +
        // CreateAllReviewResultsHtml + previewSuggestion/applySuggestion JS）→ NavigateToString，
        // spike 全部跳过
        private void RenderAiReviewOutput()
        {
            if (RetryButton != null) RetryButton.IsEnabled = true;
            if (AiResponseFallback != null) AiResponseFallback.IsVisible = false;
            RenderMarkdown(_aiReviewMarkdown);
        }

        // 对照 WPF: private void ShowError(string error)
        // spike 版：用 TextBlock 占位替代 WPF FallbackUserControl + WebView2 HTML 渲染
        private void ShowError(string error)
        {
            StopStreamingRender();
            if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
            if (StopButton != null) StopButton.IsVisible = false;
            if (StatusTextBlock != null) StatusTextBlock.Text = "";
            if (RetryButton != null) RetryButton.IsEnabled = true;

            var viewer = this.FindControl<MarkdownScrollViewer>("AiResponseMarkdownViewer");
            if (viewer != null) viewer.IsVisible = false;
            if (AiResponseFallback != null)
            {
                AiResponseFallback.IsVisible = true;
                AiResponseFallback.Text = error ?? "";
            }
        }

        // 对照 WPF: private void UpdateStatus(string message)
        // spike 版：更新状态栏文字 + 显示进度条
        private void UpdateStatus(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isClosed) return;
                if (StatusTextBlock != null) StatusTextBlock.Text = message ?? "";
                if (StatusProgressBar != null) StatusProgressBar.IsVisible = true;
                if (StopButton != null) StopButton.IsVisible = true;
            });
        }

        // 对照 WPF: private void ClearStatus()
        // spike 版：停止流式预览 + 清状态栏 + 隐藏进度条
        private void ClearStatus()
        {
            StopStreamingRender();
            Dispatcher.UIThread.Post(() =>
            {
                if (_isClosed) return;
                if (StatusTextBlock != null) StatusTextBlock.Text = "";
                if (StatusProgressBar != null) StatusProgressBar.IsVisible = false;
                if (StopButton != null) StopButton.IsVisible = false;
            });
        }

        // 对照 WPF: private void PreviewSuggestion(int index)
        // spike 版：仅状态栏打印（Phase 5.4b 接入 AiSuggestionPreviewWindow.ShowDialog）
        // 注：WPF 版由 CoreWebView2_WebMessageReceived 解析 "preview-suggestion:<i>" postMessage 后调用；
        //     spike 版由 PreviewSuggestionButton_Click 直接调用，无 postMessage 中转。
        private void PreviewSuggestion(int index)
        {
            if (_suggestions == null || index < 0 || index >= _suggestions.Count)
            {
                return;
            }
            AiReviewSuggestion suggestion = _suggestions[index];
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = "Preview suggestion #" + index +
                    " for " + suggestion.File + ":" + suggestion.Line + " (spike stub)";
            }
            // Phase 5.4b 接入：
            //   AiSuggestionPreviewWindow window = new AiSuggestionPreviewWindow(
            //       _repositoryUserControl, suggestion.File, suggestion.Comment,
            //       suggestion.OldText, suggestion.NewText) { Owner = this };
            //   if (window.ShowDialog().GetValueOrDefault()) ApplySuggestion(index);
        }

        // 对照 WPF: private void ApplySuggestion(int index)
        // spike 版：仅状态栏打印（Phase 5.4b 接入文件写入 + InvalidateAndRefresh）
        // 注：WPF 版由 CoreWebView2_WebMessageReceived 解析 "apply-suggestion:<i>" postMessage 后调用；
        //     spike 版由 ApplySuggestionButton_Click 直接调用，无 postMessage 中转。
        private void ApplySuggestion(int index)
        {
            if (_suggestions == null || index < 0 || index >= _suggestions.Count)
            {
                return;
            }
            AiReviewSuggestion suggestion = _suggestions[index];
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = "Apply suggestion #" + index +
                    " to " + suggestion.File + ":" + suggestion.Line + " (spike stub - no file write)";
            }
            // Phase 5.4b 接入：
            //   string filePath = _repositoryUserControl.GitModule.MakePath(suggestion.File);
            //   ... 读取 + 找 OldText + 替换 + 写文件 ...
            //   _suggestions.RemoveAt(index);
            //   RenderAiReviewOutput();
            //   _repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
        }

        // 对照 WPF: private void InitializeFileReviewList(AiCodeReviewTarget.Files target)
        // spike 版：用 ListBox 占位填充文件路径列表（WPF 用 FileListUserControl 树形增量构建）
        private void InitializeFileReviewList(AiCodeReviewTarget target)
        {
            if (FileReviewListBox == null) return;
            FileReviewListBox.Items.Clear();
            if (target is AiCodeReviewTarget.Files files)
            {
                // spike 版：仅展示文件路径字符串（Phase 5.4b 接入 FileListUserControl 树形构建）
                foreach (var changedFile in files.ChangedFiles ?? Array.Empty<ForkPlus.Git.ChangedFile>())
                {
                    if (changedFile?.Path != null)
                    {
                        FileReviewListBox.Items.Add(changedFile.Path);
                    }
                }
            }
            else
            {
                // 非 Files target（Branch/ShaRange）：左侧无文件列表，显示占位说明
                FileReviewListBox.Items.Add("(spike stub - revision details placeholder)");
                FileReviewListBox.IsEnabled = false;
            }
        }

        // spike 新增：ShowWelcomeMessage - 占位 markdown 提示用户窗口已就绪
        // 对照 WPF：构造函数末尾 RetryAiReview 会触发真实 AI 请求；spike 改为静态占位 markdown
        private void ShowWelcomeMessage()
        {
            string md = "# AI Code Review\n\n" +
                        "> _Phase 5.4a spike stub - real AI review will be wired in Phase 5.4b via `OpenAiService.CodeReview` / `MakeCodeReviewShellCommand`._\n\n" +
                        "Click **Retry** to simulate a review (spike stub).\n\n" +
                        "```diff\n-- diff rendering placeholder (spike)\n++ Phase 5.4b will show real git diff here\n```\n";
            _aiReviewMarkdown = md;
            RenderMarkdown(md);
        }

        // spike 版：刷新 MarkdownScrollViewer.Markdown（对照 WPF RenderAiReviewOutput + NavigateToString）
        // 注：AvaloniaNameSourceGenerator 无法解析第三方命名空间 md:MarkdownScrollViewer
        // （xmlns:md="https://github.com/whistyun/Markdown.Avalonia.Tight"），故不生成
        // AiResponseMarkdownViewer 字段。这里运行时通过 FindControl 查找控件
        // （与 AiTextResultWindow.axaml.cs / AiDevelopmentWindow.axaml.cs 同样约定）。
        private void RenderMarkdown(string markdown)
        {
            try
            {
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
                Console.WriteLine($"[AiCodeReviewWindow] Render markdown failed: {ex.Message}");
                ShowError(ex.Message);
            }
        }

        // 对照 WPF: protected override void OnKeyDown(KeyEventArgs e) - Esc 关闭窗口
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else
            {
                base.OnKeyDown(e);
            }
        }

        // 对照 WPF: protected override void OnClosed(EventArgs e)
        // spike 版：标记 _isClosed + 停止流式渲染（Phase 5.4b 接入 _aiReviewJob.Monitor.Cancel + AiResponseWebView.Dispose）
        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            StopStreamingRender();
            // Phase 5.4b 接入：
            //   _aiReviewJob?.Monitor.Cancel();
            //   _fileReviewDiffJob?.Monitor.Cancel();
            //   AiResponseWebView?.Dispose();
            //   ActivateMainWindow();
            base.OnClosed(e);
        }
    }
}
