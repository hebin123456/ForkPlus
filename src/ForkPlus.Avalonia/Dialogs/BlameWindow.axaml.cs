using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ForkPlus.Git;
using ForkPlus.Git.Interaction;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 BlameWindow（spike 简化版，对照 WPF BlameWindow.xaml.cs 647 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/BlameWindow.xaml.cs：
    //   - public partial class BlameWindow : CustomWindow
    //   - 内部类: UndoManager（_items / _currentIndex / CurrentItem / IsUndoPossible / IsRedoPossible /
    //     Add / Undo / Redo，spike 版省略）
    //   - 静态字段: Revision DummyRevision（spike 版省略）
    //   - 字段: RepositoryUserControl _repositoryUserControl / DelayedAction<BlameArgs> _refreshBlame
    //           RevisionViewModel[] _revisions / RevisionViewModel _selectedRevision
    //           bool _initialized / bool _startUpFinished / UndoManager _undoManager
    //   - 构造函数 (RepositoryUserControl, string filePath, Sha? shaToSelect, Reference targetReference)
    //     * TextDiffControl 配置 + RevisionListFallbackUserControl 显示 "Loading..."
    //     * Initialize(filePath, shaToSelect, targetReference) → Task 启动 GetFirstRevisionGitCommand
    //       + GetFileHistoryGitCommand → RevisionsComboBox.ItemsSource + _refreshBlame.InvokeNow(BlameArgs)
    //   - RefreshBlame(BlameArgs args):
    //     * GetRevisionFileChangesGitCommand → TextDiffControl.SetDiff(diff, tabWidth, entireFile, DiffLocation.Revision)
    //     * GetBlameGitCommand.Execute(gitModule, args.Filepath, $"args.Sha~") → CreateBlameItems → BlameListBox.ItemsSource
    //   - CreateBlameItems(blameChunks, visualPatch, newCommit) + Expand(blameChunks)
    //   - RevisionsComboBox_SelectionChanged: _refreshBlame.InvokeWithDelay(BlameArgs)
    //   - UndoButton/RedoButton: _undoManager.Undo()/Redo()
    //   - OnKeyDown: Escape → Close; Ctrl+G → ShowGoToLineWindow; Alt+Left/Right → Undo/Redo
    //   - BlameListBox_ContextMenuOpening: ShowRevisionInSeparateWindow / Reveal in Fork / SaveFile /
    //     CopyRevisionSha / CopyRevisionInfo / CustomCommand 菜单项
    //   - RevisionListScrollViewer_ScrollChanged → TextDiffControl.ScrollToVerticalOffset（双向同步滚动）
    //   - OnSourceInitialized / OnLocationChanged / Window_SizeChanged → ForkPlusSettings.BlameWindowLocationState
    //
    // Avalonia 版 spike 简化项（与 WPF 行为差异）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. 构造函数签名：(GitModule gitModule, string filePath, string? refSpec = null,
    //      Action<string>? onCommitSelected = null) 替代 (RepositoryUserControl, string, Sha?, Reference)
    //   3. TextDiffControl（WPF 自定义 UserControl，承载 diff 渲染）→ AvaloniaEdit.TextEditor（只读 + 行号）
    //      spike 版不做 diff 着色 / 双向滚动同步 / 滚动条 diff map
    //   4. RevisionsComboBox + BlameListBox + RevisionTimeLine + FallbackUserControl →
    //      spike 版用 ItemsControl 显示 BlameLine 列表（按出现顺序，去重合并连续相同 Sha）
    //   5. GetFirstRevisionGitCommand + GetFileHistoryGitCommand + GetRevisionFileChangesGitCommand +
    //      GetBlameGitCommand → spike 版直接调 `git blame --porcelain <refSpec> -- <path>` 一次性获取
    //      blame 元信息 + 文件内容（porcelain 输出每行内容前缀为 \t）
    //   6. CreateBlameItems + VisualPatch + VisualChunk 对齐 → spike 版仅内联 BlameLine POCO
    //      并按 LineNumber 顺序渲染到 ItemsControl
    //   7. 行高亮：WPF 用 BlameItemViewModel + Background renderer 高亮同一 commit 的所有行
    //      spike 版用 AvaloniaEdit.Rendering.DocumentColorizingTransformer（ColorizeLine）给同一 Sha
    //      的所有行加 Background（颜色按 Sha 哈希到固定调色板）
    //   8. UndoManager / UndoButton / RedoButton → spike 版省略
    //   9. ContextMenu 菜单项 → spike 版省略
    //  10. ForkPlusSettings.BlameWindowLocationState 持久化 → spike 版跳过
    //  11. GoToLineWindow (Ctrl+G) → spike 版省略
    //  12. RevisionsListBoxItem_MouseDoubleClick → onCommitSelected 回调通知调用方
    //  13. Dispatcher.Async → Dispatcher.UIThread.Post
    //  14. new Task().Start() → Task.Run
    //  15. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //
    // 内联 POCO 类:
    //   - BlameLine: Line / Sha / Author / AuthorDate / Committer / CommitterDate / Summary / Content
    //     + 派生属性 ShaDisplay / AuthorDateDisplay（用于 DataTemplate 绑定）
    //   - BlameHighlightColorizer: DocumentColorizingTransformer 子类，按选中 Sha 高亮所有行
    public partial class BlameWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：内联简化 BlameLine POCO（WPF 用 BlameItemViewModel + BlameItemBodyViewModel）
        // 字段对照 WPF: Revision.Sha / Revision.Author.Name / Revision.AuthorDate /
        //              Revision.Message（summary）+ 内联 Committer/CommitterDate（WPF 通过 RevisionDetails 拿）
        public sealed class BlameLine
        {
            public int Line { get; set; }
            public string Sha { get; set; }
            public string Author { get; set; }
            public DateTime AuthorDate { get; set; }
            public string Committer { get; set; }
            public DateTime CommitterDate { get; set; }
            public string Summary { get; set; }
            public string Content { get; set; }

            // 派生属性用于 DataTemplate 绑定
            public string ShaDisplay => string.IsNullOrEmpty(Sha) ? "" : (Sha.Length >= 8 ? Sha.Substring(0, 8) : Sha);
            public string AuthorDateDisplay => AuthorDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        // spike 版：内联 BlameHighlightColorizer（DocumentColorizingTransformer 子类）
        // 对照 WPF: 通过 BlameItemViewModel + Background renderer 高亮同一 commit 的所有行
        // Avalonia: 用 AvaloniaEdit.Rendering.DocumentColorizingTransformer.ColorizeLine 给同一 Sha 的所有行加 Background
        private sealed class BlameHighlightColorizer : DocumentColorizingTransformer
        {
            private readonly Func<int, string> _getShaAtLine;
            private string _highlightSha;
            private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xD7, 0x00));

            public BlameHighlightColorizer(Func<int, string> getShaAtLine)
            {
                _getShaAtLine = getShaAtLine;
            }

            public void SetHighlightSha(string sha)
            {
                _highlightSha = sha;
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                if (string.IsNullOrEmpty(_highlightSha) || _getShaAtLine == null) return;
                int lineNumber = line.LineNumber; // 1-based
                string sha = _getShaAtLine(lineNumber);
                if (sha != _highlightSha) return;
                int start = line.Offset;
                int end = line.Offset + line.Length;
                if (end <= start) return;
                ChangeLinePart(start, end, v =>
                {
                    v.TextRunProperties.SetBackgroundBrush(HighlightBrush);
                });
            }
        }

        private readonly GitModule _gitModule;
        private readonly string _filePath;
        private readonly string _refSpec;
        private readonly Action<string> _onCommitSelected;

        private ObservableCollection<BlameLine> _blameLines = new ObservableCollection<BlameLine>();
        // 行号(1-based) → Sha 映射，用于 Colorizer 查找
        private readonly Dictionary<int, string> _lineToSha = new Dictionary<int, string>();
        // 选中 Sha 的所有 BlameLine（用于 ItemsControl 选中项 Background 高亮）
        private string _selectedSha;
        private readonly BlameHighlightColorizer _colorizer;

        public BlameWindow(GitModule gitModule, string filePath, string? refSpec = null, Action<string>? onCommitSelected = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _refSpec = refSpec;
            _onCommitSelected = onCommitSelected;

            // 对照 WPF: base.Title = PathHelper.GetReadableFileName(filePath) + " - " + Translate("Blame")
            string title = PathHelper.GetReadableFileName(filePath) + " - " + Translate("Blame");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Show file blame information (each line's last commit).");
            CancelButtonTitle = Translate("Close");
            ShowSubmitButton = false;

            // 对照 WPF: FileNameTextBlock.FilePath = filePath;
            FilePathTextBlock.Text = filePath;

            // 绑定 ItemsControl 数据源
            BlameItemsControl.ItemsSource = _blameLines;

            // 选中项点击触发 onCommitSelected 回调
            BlameItemsControl.PointerReleased += BlameItemsControl_PointerReleased;

            // 安装 Colorizer 到 AvaloniaEdit TextView.LineTransformers
            // 对照 WPF: BlameListBox 行高亮（同一 commit 的所有行通过 BlameItemViewModel 共享 Revision）
            // spike 版用 DocumentColorizingTransformer 给同一 Sha 的所有行加 Background
            _colorizer = new BlameHighlightColorizer(lineNumber =>
            {
                return _lineToSha.TryGetValue(lineNumber, out var sha) ? sha : null;
            });
            try
            {
                FileContentEditor.TextArea.TextView.LineTransformers.Add(_colorizer);
            }
            catch (Exception ex)
            {
                // spike 兜底：API 不可用则跳过高亮
                Log.Warn("BlameWindow: failed to install BlameHighlightColorizer", ex);
            }

            // 后台启动 git blame --porcelain 解析
            // 对照 WPF: Initialize(filePath, shaToSelect, targetReference) → new Task().Start()
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            LoadBlameAsync();
        }

        // 对照 WPF: private void Initialize(string filePath, Sha? sha, Reference targetReference)
        // spike 版：直接调 `git blame --porcelain <refSpec> -- <path>` 一次性获取
        private void LoadBlameAsync()
        {
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading blame..."));
            Task.Run(delegate
            {
                try
                {
                    var args = new List<string> { "blame", "--porcelain" };
                    if (!string.IsNullOrEmpty(_refSpec))
                    {
                        args.Add(_refSpec);
                    }
                    args.Add("--");
                    args.Add(_filePath);
                    var gitCommand = new GitCommand(args.ToArray());
                    var result = new GitRequest(_gitModule).Command(gitCommand).Execute(silent: true);
                    if (!result.Success)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            ShowErrorFallback(result.Stderr.Length > 0 ? result.Stderr : result.Stdout);
                        });
                        return;
                    }
                    var lines = ParseBlamePorcelain(result.Stdout);
                    Dispatcher.UIThread.Post(delegate
                    {
                        ApplyBlameLines(lines);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        ShowErrorFallback(ex.Message);
                    });
                }
            });
        }

        // spike 版：内联简化 git blame --porcelain 解析器
        // 对照 WPF: GetBlameGitCommand.ParseRevisionHeader（仅解析 author/author-mail/author-time/summary）
        // spike 版额外解析 committer/committer-time/committer-mail（WPF 不解析）
        // porcelain 输出格式：
        //   <sha> <orig-line> <final-line> [<num-lines>]
        //   author <name>
        //   author-mail <<email>>
        //   author-time <unix-ts>
        //   author-tz <timezone>
        //   committer <name>
        //   committer-mail <<email>>
        //   committer-time <unix-ts>
        //   committer-tz <timezone>
        //   summary <message>
        //   filename <file>
        //   \t<line content>
        private List<BlameLine> ParseBlamePorcelain(string output)
        {
            var result = new List<BlameLine>();
            if (string.IsNullOrEmpty(output)) return result;

            // 按原始换行分割（保留空行）
            string[] rawLines = output.Split('\n');
            int i = 0;
            while (i < rawLines.Length)
            {
                string header = rawLines[i];
                if (string.IsNullOrEmpty(header))
                {
                    i++;
                    continue;
                }
                // 头行格式: <sha> <orig-line> <final-line> [<num-lines>]
                string[] parts = header.Split(' ');
                if (parts.Length < 3)
                {
                    i++;
                    continue;
                }
                string sha = parts[0];
                if (!int.TryParse(parts[2], out int lineNumber))
                {
                    i++;
                    continue;
                }

                // 跳过头行，开始读 header keys
                i++;
                string author = "";
                string authorEmail = "";
                long authorTime = 0;
                string committer = "";
                string committerEmail = "";
                long committerTime = 0;
                string summary = "";
                string content = "";

                while (i < rawLines.Length)
                {
                    string line = rawLines[i];
                    if (line.StartsWith("\t"))
                    {
                        // 行内容（去掉前缀 \t）
                        content = line.Substring(1);
                        i++;
                        break;
                    }
                    if (line.StartsWith("author ", StringComparison.Ordinal))
                    {
                        author = line.Substring("author ".Length);
                    }
                    else if (line.StartsWith("author-mail ", StringComparison.Ordinal))
                    {
                        authorEmail = line.Substring("author-mail ".Length).Trim('<', '>');
                    }
                    else if (line.StartsWith("author-time ", StringComparison.Ordinal))
                    {
                        long.TryParse(line.Substring("author-time ".Length), out authorTime);
                    }
                    else if (line.StartsWith("committer ", StringComparison.Ordinal))
                    {
                        committer = line.Substring("committer ".Length);
                    }
                    else if (line.StartsWith("committer-mail ", StringComparison.Ordinal))
                    {
                        committerEmail = line.Substring("committer-mail ".Length).Trim('<', '>');
                    }
                    else if (line.StartsWith("committer-time ", StringComparison.Ordinal))
                    {
                        long.TryParse(line.Substring("committer-time ".Length), out committerTime);
                    }
                    else if (line.StartsWith("summary ", StringComparison.Ordinal))
                    {
                        summary = line.Substring("summary ".Length);
                    }
                    i++;
                }

                result.Add(new BlameLine
                {
                    Line = lineNumber,
                    Sha = sha,
                    Author = string.IsNullOrEmpty(author) ? authorEmail : author,
                    AuthorDate = DateTimeOffset.FromUnixTimeSeconds(authorTime).LocalDateTime,
                    Committer = string.IsNullOrEmpty(committer) ? committerEmail : committer,
                    CommitterDate = DateTimeOffset.FromUnixTimeSeconds(committerTime).LocalDateTime,
                    Summary = summary,
                    Content = content
                });
            }
            return result;
        }

        // 对照 WPF: BlameListBox.ItemsSource = CreateBlameItems(blameChunks, visualPatch, newCommit)
        // spike 版：按 LineNumber 排序后渲染到 ItemsControl + 文件内容渲染到 AvaloniaEdit
        private void ApplyBlameLines(List<BlameLine> lines)
        {
            _blameLines.Clear();
            _lineToSha.Clear();
            var sorted = lines.OrderBy(l => l.Line).ToList();
            // 文件内容：拼接所有行（用 \n 分隔）
            var contentBuilder = new System.Text.StringBuilder();
            // 用于 ItemsControl：合并连续相同 Sha 的行（仅显示首个出现的 commit 信息）
            string lastSha = null;
            foreach (var line in sorted)
            {
                _lineToSha[line.Line] = line.Sha;
                contentBuilder.Append(line.Content);
                contentBuilder.Append('\n');
                if (line.Sha != lastSha)
                {
                    _blameLines.Add(line);
                    lastSha = line.Sha;
                }
            }
            // 渲染文件内容到 AvaloniaEdit
            FileContentEditor.Document.Text = contentBuilder.ToString();
            // 选中状态清空
            _selectedSha = null;
            _colorizer?.SetHighlightSha(null);

            SetStatus(ForkPlusDialogStatus.None, "");
            StatusText.Text = FormatTranslate("{0} lines, {1} commits",
                sorted.Count, _blameLines.Count);
        }

        // 对照 WPF: ShaButton_Click + RevisionsListBoxItem_MouseDoubleClick
        // spike 版：点击 ItemsControl 中的 commit 项触发 onCommitSelected + 高亮同一 Sha 的行
        private void BlameItemsControl_PointerReleased(object? sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            // 通过命中测试找到点击的 ContentPresenter
            var control = e.Source as Visual;
            while (control != null && !(control is ContentPresenter) && !(control is ContentControl))
            {
                control = control.GetVisualParent();
            }
            if (control == null) return;
            var dataContext = control is ContentPresenter cp ? cp.DataContext
                          : control is ContentControl cc ? cc.DataContext
                          : null;
            if (dataContext is BlameLine line)
            {
                SelectCommit(line.Sha);
            }
        }

        // 对照 WPF: ShaButton_Click → RevisionsComboBox.SelectedItem = FirstItem(sha == blameChunk.RevisionSha)
        // spike 版：选中同一 Sha 的所有 BlameLine 行 + 高亮文件内容中相同 Sha 的行
        private void SelectCommit(string sha)
        {
            _selectedSha = sha;
            _colorizer?.SetHighlightSha(sha);
            // 强制 TextView 重新着色
            try
            {
                FileContentEditor.TextArea.TextView.Redraw();
            }
            catch (Exception ex)
            {
                Log.Warn("BlameWindow: TextView.Redraw failed", ex);
            }
            // 触发回调通知调用方
            try
            {
                _onCommitSelected?.Invoke(sha);
            }
            catch (Exception ex)
            {
                Log.Error("BlameWindow onCommitSelected callback failed", ex);
            }
        }

        // 对照 WPF: ShowErrorFallback
        private void ShowErrorFallback(string errorString)
        {
            SetStatus(ForkPlusDialogStatus.Error, errorString);
            StatusText.Text = Translate("Error") + ": " + errorString;
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
