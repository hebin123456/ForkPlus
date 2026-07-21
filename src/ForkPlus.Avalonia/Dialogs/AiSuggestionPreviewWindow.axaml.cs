using System;
using System.Text;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 AiSuggestionPreviewWindow（真实迁移版，对照 WPF AiSuggestionPreviewWindow.xaml.cs 134 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AiSuggestionPreviewWindow.xaml.cs：
    //   - public partial class AiSuggestionPreviewWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / string _file / string _oldText / string _newText
    //   - 构造函数 (RepositoryUserControl, string file, string comment, string oldText, string newText):
    //     * DialogTitle = Translate("Suggestion Preview")
    //     * DialogDescription = string.IsNullOrWhiteSpace(comment) ? file : file + "\n" + comment
    //     * SubmitButtonTitle = Translate("Apply suggestion")
    //     * CancelButtonTitle = Translate("Close")
    //     * Loaded += AiSuggestionPreviewWindow_Loaded
    //   - AiSuggestionPreviewWindow_Loaded:
    //     * try PreviewDiffControl.RepositoryUserControl = _repositoryUserControl;
    //           PreviewDiffControl.Content = GitCommandResult<DiffContent>.Success(CreateDiffContent(...))
    //     * catch (fallback) PreviewDiffControl.Hide(); FallbackTextBox.Text = CreateFallbackText(...); FallbackTextBox.Show()
    //   - OnSubmit: CloseWithOk()
    //   - CreateDiffContent(RepositoryUserControl, file, oldText, newText):
    //     * ChangedFile changedFile = new ChangedFile(file ?? "suggestion-preview.txt", Modified, None)
    //     * string diff = CreateUnifiedDiff(changedFile.Path, oldText, newText)
    //     * int tabWidth = repositoryUserControl?.GitModule?.Settings?.TabWidth ?? 4
    //     * return new TextDiffContent(changedFile, diff, tabWidth, entireFile: false)
    //   - CreateUnifiedDiff(file, oldText, newText): 构造 unified diff 字符串
    //     (diff --git / index / ---/+++ / @@ -a,b +c,d @@ / -/+ lines + "No newline at end of file")
    //   - SplitDiffLines(text, out endsWithNewLine): 按 \n 分割（处理 \r\n / \r）
    //   - AppendLines(builder, prefix, lines, endsWithNewLine): 追加 prefix + line，末尾无换行时追加 "\\ No newline at end of file"
    //   - DiffRangeStart(lineCount): lineCount == 0 ? 0 : 1
    //   - EscapeDiffPath(path): Replace('\\','/') + Replace \n/\r 为 _
    //   - CreateFallbackText(oldText, newText): "Original:\n" + oldText + "\n\nSuggested:\n" + newText
    //   - Translate(text): PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreviewDiffControl 是 WPF 自定义控件，spike 版用 DiffPreviewTextBox（只读 + 等宽字体 + Wrap + AcceptsReturn）
    //      直接显示 CreateUnifiedDiff 生成的字符串
    //   3. 移除 RepositoryUserControl 依赖（spike 版构造函数不接受 RepositoryUserControl）
    //   4. 移除 try/catch fallback 逻辑（直接显示 unified diff 字符串，无需 fallback）
    //   5. tabWidth 用参数传入（默认 4），不依赖 gitModule.Settings.TabWidth
    //   6. 完整移植 CreateUnifiedDiff / SplitDiffLines / AppendLines / DiffRangeStart / EscapeDiffPath / CreateFallbackText
    //      静态方法（CreateFallbackText 保留以备 Phase 4.x.c 接入 fallback 控件时复用）
    //   7. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   8. Loaded += （Avalonia 同名事件，参数 EventArgs）
    //   9. 构造函数签名：(string file, string? comment, string oldText, string newText, int tabWidth = 4)
    public partial class AiSuggestionPreviewWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly string _file;
        private readonly string _oldText;
        private readonly string _newText;
        private readonly int _tabWidth;

        // 构造函数签名与 WPF 不同：移除 RepositoryUserControl 依赖
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版用 tabWidth 参数替代 gitModule.Settings.TabWidth）
        public AiSuggestionPreviewWindow(string file, string? comment, string oldText, string newText, int tabWidth = 4)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _file = file;
            _oldText = oldText;
            _newText = newText;
            _tabWidth = tabWidth;

            // 对照 WPF: DialogTitle = Translate("Suggestion Preview")
            string title = Translate("Suggestion Preview");
            Title = title;
            DialogTitle = title;

            // 对照 WPF: DialogDescription = string.IsNullOrWhiteSpace(comment) ? file : file + "\n" + comment
            DialogDescription = string.IsNullOrWhiteSpace(comment) ? (file ?? "") : (file ?? "") + "\n" + comment;

            // 对照 WPF: SubmitButtonTitle = Translate("Apply suggestion")
            SubmitButtonTitle = Translate("Apply suggestion");
            // 对照 WPF: CancelButtonTitle = Translate("Close")
            CancelButtonTitle = Translate("Close");

            // 对照 WPF: Loaded += AiSuggestionPreviewWindow_Loaded
            Loaded += AiSuggestionPreviewWindow_Loaded;
        }

        // 对照 WPF: private void AiSuggestionPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        // Avalonia 11: Loaded 事件参数为 EventArgs
        // spike 版：直接显示 unified diff 字符串，移除 try/catch fallback
        private void AiSuggestionPreviewWindow_Loaded(object? sender, EventArgs e)
        {
            // 对照 WPF: PreviewDiffControl.Content = GitCommandResult<DiffContent>.Success(CreateDiffContent(...))
            // spike 版：PreviewDiffControl 是 WPF 控件，spike 版用 DiffPreviewTextBox 直接显示 unified diff 字符串
            string diff = CreateUnifiedDiff(_file, _oldText, _newText);
            DiffPreviewTextBox.Text = diff;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            CloseWithOk();
        }

        // 对照 WPF: private static string CreateFallbackText(string oldText, string newText)
        // spike 版保留：当前未使用（移除 fallback 路径），但完整移植以备 Phase 4.x.c 接入 fallback 控件时复用
        private static string CreateFallbackText(string oldText, string newText)
        {
            return "Original:\n"
                + (oldText ?? "")
                + "\n\nSuggested:\n"
                + (newText ?? "");
        }

        // 对照 WPF: private static string CreateUnifiedDiff(string file, string oldText, string newText)
        // 完整移植：构造 unified diff 字符串
        private static string CreateUnifiedDiff(string file, string oldText, string newText)
        {
            string[] oldLines = SplitDiffLines(oldText, out bool oldEndsWithNewLine);
            string[] newLines = SplitDiffLines(newText, out bool newEndsWithNewLine);
            StringBuilder builder = new StringBuilder();
            string path = EscapeDiffPath(file);
            builder.Append("diff --git forkSrcPrefix/").Append(path).Append(" forkDstPrefix/").Append(path).AppendLine();
            builder.Append("index 0000000000000000000000000000000000000000..0000000000000000000000000000000000000000 100644").AppendLine();
            builder.Append("--- forkSrcPrefix/").Append(path).AppendLine();
            builder.Append("+++ forkDstPrefix/").Append(path).AppendLine();
            builder.Append("@@ -").Append(DiffRangeStart(oldLines.Length)).Append(",").Append(oldLines.Length)
                .Append(" +").Append(DiffRangeStart(newLines.Length)).Append(",").Append(newLines.Length)
                .Append(" @@").AppendLine();
            AppendLines(builder, "-", oldLines, oldEndsWithNewLine);
            AppendLines(builder, "+", newLines, newEndsWithNewLine);
            return builder.ToString();
        }

        // 对照 WPF: private static string[] SplitDiffLines(string text, out bool endsWithNewLine)
        private static string[] SplitDiffLines(string text, out bool endsWithNewLine)
        {
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            endsWithNewLine = normalized.EndsWith("\n", StringComparison.Ordinal);
            if (normalized.Length == 0)
            {
                return new string[0];
            }
            string withoutFinalNewLine = endsWithNewLine ? normalized.Substring(0, normalized.Length - 1) : normalized;
            if (withoutFinalNewLine.Length == 0)
            {
                return new[] { "" };
            }
            return withoutFinalNewLine.Split('\n');
        }

        // 对照 WPF: private static void AppendLines(StringBuilder builder, string prefix, string[] lines, bool endsWithNewLine)
        private static void AppendLines(StringBuilder builder, string prefix, string[] lines, bool endsWithNewLine)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                builder.Append(prefix).AppendLine(lines[i]);
                if (i == lines.Length - 1 && !endsWithNewLine)
                {
                    builder.AppendLine("\\ No newline at end of file");
                }
            }
        }

        // 对照 WPF: private static int DiffRangeStart(int lineCount)
        private static int DiffRangeStart(int lineCount)
        {
            return lineCount == 0 ? 0 : 1;
        }

        // 对照 WPF: private static string EscapeDiffPath(string path)
        private static string EscapeDiffPath(string path)
        {
            return (path ?? "suggestion-preview.txt").Replace('\\', '/').Replace("\n", "_").Replace("\r", "_");
        }

        // 对照 WPF: private static string Translate(string text)
        // PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage) → ServiceLocator.Localization.Translate
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
    }
}
