using System;
using System.IO;
using Avalonia.Controls;
using Markdown.Avalonia;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.1a：Avalonia 版 GitMmReferenceWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitMmReferenceWindow.xaml.cs（245 行）：
    //   - public partial class GitMmReferenceWindow : ForkPlusDialogWindow
    //   - 构造函数：InitializeComponent + DialogTitle="git mm Reference" +
    //     DialogDescription="Command reference for git mm start, sync, and upload." +
    //     CancelButtonTitle="Close" + ShowSubmitButton=false + Loaded += InitializeManualWebView
    //   - LoadManual()：按 UI 语言回退加载 Docs/gitmm.*.md（顺序：
    //     gitmm.{lang}.md → gitmm.en.md → gitmm.zh-Hans.md → gitmm.md）
    //   - InitializeManualWebView()：LoadManual + MarkdownToHtml + WebView2.NavigateToString
    //   - MarkdownToHtml(markdown)：自写 GFM 表格解析（IsTableRow / IsTableSeparator /
    //     SplitTableRow / AppendHtmlTable）+ Bt.bt_md_to_html P/Invoke 转换非表格部分
    //   - CreateHtmlDocument(bodyHtml)：内联 CSS（含 dark mode @media prefers-color-scheme）
    //   - ShowFallback(message)：WebView2.Collapse + FallbackUserControl.Show
    //   - WebView2 禁用右键（ContextMenuRequested += Handled=true）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 用 Markdown.Avalonia.Tight 的 MarkdownScrollViewer 替代整个 WebView2 链路
    //   2. 跳过 MarkdownToHtml / CreateHtmlDocument / GFM 表格解析
    //      （Markdown.Avalonia 内部用 Markdig 完整支持 GFM 表格）
    //   3. 跳过 Bt.bt_md_to_html P/Invoke（Markdown.Avalonia 跨平台，无需 native 调用）
    //   4. 跳过 WebView2EnvironmentHelper（Avalonia 无 WebView 初始化需求）
    //   5. 跳过 FallbackUserControl（spike 用 TextBlock 占位）
    //   6. 跳过主题切换订阅（Markdown.Avalonia 自动检测 light/dark）
    //   7. spike 版 LoadManual 仅加载英文版（Phase 5.1b 接入 ForkPlusSettings.Default.UiLanguage）
    //
    // 本 spike 版暂不迁移（留 Phase 5.1b）：
    //   - PreferencesLocalization.Translate（spike 用字面量英文）
    //   - 多语言 Docs/gitmm.*.md 加载（spike 仅 gitmm.en.md / gitmm.md）
    //   - 主题切换订阅（NotificationCenter.ApplicationThemeChanged）
    //   - FallbackUserControl 完整 UI（spike 用 TextBlock）
    //
    // 本 spike 版验证：
    //   - Markdown.Avalonia.Tight 11.0.3 可加载 markdown 字符串
    //   - GFM 表格 + 代码块 + 链接渲染正常
    //   - WebView2 完整替代（markdown → Avalonia 控件树，无浏览器内核）
    public partial class GitMmReferenceWindow : ForkPlusDialogWindow
    {
        public GitMmReferenceWindow()
        {
            InitializeComponent();

            // 对照 WPF: DialogTitle = Translate("git mm Reference")
            DialogTitle = "git mm Reference";
            // 对照 WPF: DialogDescription = Translate("Command reference for git mm start, sync, and upload.")
            DialogDescription = "Command reference for git mm start, sync, and upload.";
            // 对照 WPF: CancelButtonTitle = Translate("Close")
            CancelButtonTitle = "Close";
            // 对照 WPF: ShowSubmitButton = false（只有 Close 按钮）
            ShowSubmitButton = false;

            Loaded += GitMmReferenceWindow_Loaded;
        }

        private void GitMmReferenceWindow_Loaded(object? sender, EventArgs e)
        {
            InitializeManualView();
        }

        // 对照 WPF: private async Task InitializeManualWebView()
        // spike 版：同步加载 markdown 字符串赋给 MarkdownScrollViewer.Markdown
        private void InitializeManualView()
        {
            try
            {
                string markdown = LoadManual();
                if (string.IsNullOrEmpty(markdown))
                {
                    ShowFallback("git mm reference document was not found.");
                    return;
                }

                // Markdown.Avalonia 直接渲染 markdown 字符串（无需 HTML 转换）
                // AvaloniaNameSourceGenerator 无法解析第三方命名空间 md:MarkdownScrollViewer
                // （xmlns:md="https://github.com/whistyun/Markdown.Avalonia"），故不生成
                // ManualMarkdownViewer 字段。这里运行时通过 FindControl 查找控件。
                var viewer = this.FindControl<MarkdownScrollViewer>("ManualMarkdownViewer");
                if (viewer != null)
                {
                    viewer.Markdown = markdown;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitMmReferenceWindow] Failed to load manual: {ex.Message}");
                ShowFallback(ex.Message);
            }
        }

        // 对照 WPF: internal static string LoadManual()
        // spike 版：简化为加载英文版回退（Phase 5.1b 接入 ForkPlusSettings.Default.UiLanguage 多语言）
        internal static string LoadManual()
        {
            string docsDirectory = Path.Combine(AppContext.BaseDirectory, "Docs");
            string[] candidates =
            {
                // Phase 5.1b 在此加：Path.Combine(docsDirectory, "gitmm." + lang + ".md"),
                Path.Combine(docsDirectory, "gitmm.en.md"),
                Path.Combine(docsDirectory, "gitmm.zh-Hans.md"),
                Path.Combine(docsDirectory, "gitmm.md")
            };
            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            return string.Empty;
        }

        // 对照 WPF: private void ShowFallback(string message)
        // spike 版：用 TextBlock 占位替代 FallbackUserControl
        private void ShowFallback(string message)
        {
            // ManualMarkdownViewer 通过 FindControl 查找（见 InitializeManualView 注释）
            var viewer = this.FindControl<MarkdownScrollViewer>("ManualMarkdownViewer");
            if (viewer != null) viewer.IsVisible = false;
            if (ManualFallback != null)
            {
                ManualFallback.IsVisible = true;
                ManualFallback.Text = message;
            }
        }
    }
}
