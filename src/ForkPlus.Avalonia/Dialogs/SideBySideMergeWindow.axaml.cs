using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using ForkPlus.Git;
using ForkPlus.Git.Interaction;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 SideBySideMergeWindow（spike 简化版，对照 WPF SideBySideMergeWindow.xaml.cs 1049 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/SideBySideMergeWindow.xaml.cs：
    //   - public partial class SideBySideMergeWindow : ForkPlusDialogWindow
    //   - 内部 enum MergeMode { Undefined, Text, Binary }（spike 版省略，仅处理 Text）
    //   - 静态字段: UnifiedMergeParser _mergeConflictParser（spike 版内联简化解析器）
    //   - 字段: GitModule _gitModule / RepositoryState _repositoryState / ChangedFile _changedFile
    //           MergeConflict _mergeConflict / bool _stopCheckBoxEvents / bool _startUpFinished
    //           MergeMode _mergeMode / DiffContent _fileContent
    //           DateTime _lastLastScrollTime / MergeCodeEditor _lastUpdatedEditor / bool _refreshInProgress
    //           bool _aiResolving
    //   - IsSubmitAllowed:
    //     * Text mode: _mergeConflict != null && _mergeConflict.IsResolved
    //     * Binary mode: AllLocal / AllRemote 单选互斥
    //   - 构造函数 (RepositoryUserControl, RepositoryState, ChangedFile)
    //     * FileMergeControl.RepositoryUserControl / LocalMergeEditor.ViewMode = Local
    //     * RemoteMergeEditor.ViewMode = Remote / MergedMergeEditor.ViewMode = Merged
    //     * MergeCodeEditor 事件: MergeLineAdded / MergeLineRemoved / MergeChunkAdded / MergeChunkRemoved
    //     * TextArea.TextView.ScrollOffsetChanged → OnScrollOffsetChanged（三向同步滚动）
    //     * MergedMergeEditor.IsReadOnly = false / MergedMergeEditor.Document.Changing → Document_Changing
    //     * NotificationCenter.CodeEditorFontSizeChanged → RefreshCodeEditorFontSize
    //     * Loaded → Refresh()
    //   - OnSubmit:
    //     * Text: MergeConflictView.Create(Merged).StringValue → ResolveMergeConflictGitCommand → Close(gitResult)
    //     * Binary: AllLocal → ResolveConflictGitCommand(Local) / AllRemote → ResolveConflictGitCommand(Remote)
    //   - Document_Changing: 合并内容编辑校验（防止删除对齐行 / 内容不匹配）
    //   - AllRemoteCheckBox_Changed / AllLocalCheckBox_Changed: SelectAll(select, origin)
    //   - MergeEditor_MergeLineAdded / MergeLineRemoved / MergeChunkAdded / MergeChunkRemoved
    //   - Refresh: GetWorkingDirectoryFileChangesGitCommand → UnmergedDiffContent
    //     * Text: UnifiedMergeParser.TryParse → _mergeConflict + RefreshMergeEditorViews + SelectFirstConflictedChunk
    //     * Binary: FileMergeControl.Content = gitCommandResult
    //   - RefreshTopCheckBoxButtons: GetRevisionDetailsGitCommand → LocalSubjectTextBlock / RemoteSubjectTextBlock
    //   - RefreshMergeEditorViews: SetMergeConflictView(Local/Remote/Merged)
    //   - RefreshMergedView / RefreshMergeStatusControls: ConflictStatus 显示 resolved/total
    //   - RefreshCheckboxes: AllRemote / AllLocal 三态复选框同步
    //   - NextChunkButton_Click / PreviousChunkButton_Click: 滚动到下一个/上一个冲突块
    //   - LayoutOrientationToggleButton_Changed: 切换 Horizontal / Vertical 布局
    //   - AiResolveButton_Click: OpenAiService 解决冲突（流式响应 + 用户确认）
    //   - OnClosed: ForkPlusSettings.Default.Save()
    //
    // Avalonia 版 spike 简化项（与 WPF 行为差异）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. 构造函数签名：(GitModule gitModule, string filePath, Action<string>? onResolved = null)
    //      替代 (RepositoryUserControl, RepositoryState, ChangedFile)
    //   3. 三向合并视图（左/中/右 MergeCodeEditor）→ spike 版用单个 AvaloniaEdit.TextEditor
    //      通过顶部 ComboBox 切换 Current / Ours / Theirs / Merged 视图模式
    //   4. MergeCodeEditor + MergeConflictView + MergeConflict + UnifiedMergeParser →
    //      spike 版内联 MergeViewMode 枚举 + 简单冲突标记解析（<<<<<<< / ======= / >>>>>>>）
    //   5. ResolveMergeConflictGitCommand → spike 版直接写文件 + git add + onResolved 回调
    //   6. RepositoryState / ChangedFile / DiffContent / UnmergedDiffContent → spike 版直接读磁盘文件
    //   7. AI Resolve (OpenAiService) → spike 版省略
    //   8. LayoutOrientationToggleButton（Horizontal/Vertical 切换）→ spike 版省略（单 editor 无需布局切换）
    //   9. NextChunkButton / PreviousChunkButton → spike 版省略
    //  10. RefreshTopCheckBoxButtons（本地/远程 commit 信息面板）→ spike 版省略
    //  11. AllLocalCheckBox / AllRemoteCheckBox → spike 版用 ComboBox 视图模式替代（Ours/Theirs）
    //  12. ForkPlusSettings.SideBySideMergeWindowLocationState 持久化 → spike 版跳过
    //  13. NotificationCenter.CodeEditorFontSizeChanged 订阅 → spike 版省略
    //  14. Dispatcher.Invoke / Dispatcher.BeginInvoke → Dispatcher.UIThread.Post
    //  15. PreferencesLocalization.Current / FormatCurrent → ServiceLocator.Localization.Translate / FormatCurrent
    //  16. OnClosed override → Closed += 事件
    //
    // 内联 POCO / 枚举类:
    //   - MergeViewMode: Current / Ours / Theirs / Merged 枚举（对照 WPF MergeConflictPart + ViewMode）
    //   - ConflictBlock: 简化的冲突块结构（StartLine / OursLines / TheirsLines / EndLine）
    public partial class SideBySideMergeWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：内联 MergeViewMode 枚举（对照 WPF MergeConflictPart + MergeCodeEditor.ViewMode）
        // Current = 当前编辑器内容（用户可编辑）
        // Ours = 本地版本（<<<<<<< ours 块内容）
        // Theirs = 远程版本（>>>>>>> theirs 块内容）
        // Merged = 合并后内容（去掉冲突标记，保留 ours + theirs）
        private enum MergeViewMode
        {
            Current,
            Ours,
            Theirs,
            Merged
        }

        // spike 版：内联简化冲突块结构（对照 WPF MergeConflict.ConflictChunk）
        private sealed class ConflictBlock
        {
            public int StartLine;   // <<<<<<< 行号（0-based）
            public List<string> OursLines = new List<string>();
            public List<string> TheirsLines = new List<string>();
            public int SeparatorLine; // ======= 行号
            public int EndLine;     // >>>>>>> 行号
        }

        private readonly GitModule _gitModule;
        private readonly string _filePath;
        private readonly Action<string> _onResolved;

        // 原始文件内容（带冲突标记）
        private string _originalContent = "";
        // 当前视图模式
        private MergeViewMode _currentViewMode = MergeViewMode.Current;
        // 解析出的冲突块列表
        private List<ConflictBlock> _conflictBlocks = new List<ConflictBlock>();
        // 用户编辑的当前内容（切换到 Current 时显示）
        private string _currentContent = "";
        // 防止 ComboBox SelectionChanged 在程序设置 SelectedIndex 时递归触发
        private bool _suppressViewModeChanged;
        // 已保存标志（防止重复保存）
        private bool _saved;

        // 视图模式友好名称（用于 ComboBox 显示）
        private static readonly string[] ViewModeDisplayNames =
        {
            "Current",
            "Ours",
            "Theirs",
            "Merged"
        };

        public SideBySideMergeWindow(GitModule gitModule, string filePath, Action<string>? onResolved = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _onResolved = onResolved;

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Resolve")
            DialogTitle = Translate("Resolve Merge Conflicts");
            DialogDescription = Translate("Edit the merged content and save to resolve conflicts.");
            SubmitButtonTitle = Translate("Save");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Resolve Merge Conflicts");

            // 对照 WPF: RefreshHeader(changedFile) → FilePathTextBlock.FilePath = changedFile.Path
            FilePathTextBlock.Text = filePath;

            // 初始化 ComboBox 选项
            foreach (var name in ViewModeDisplayNames)
            {
                ViewModeComboBox.Items.Add(name);
            }
            ViewModeComboBox.SelectedIndex = (int)MergeViewMode.Current;

            // 对照 WPF: Loaded → Refresh()
            LoadConflictContent();

            // 对照 WPF: OnClosed override → ForkPlusSettings.Default.Save()
            // spike 版：用 Closed += 事件（Avalonia 11 API 差异）
            Closed += SideBySideMergeWindow_Closed;
        }

        // 对照 WPF: Refresh → GetWorkingDirectoryFileChangesGitCommand → UnmergedDiffContent
        // spike 版：直接读磁盘文件 _gitModule.MakePath(_filePath)
        private void LoadConflictContent()
        {
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading conflict file..."));
            string absolutePath;
            try
            {
                absolutePath = _gitModule.MakePath(_filePath);
            }
            catch (Exception ex)
            {
                ShowErrorFallback(ex.Message);
                return;
            }
            Task.Run(delegate
            {
                string content = "";
                string error = null;
                try
                {
                    if (File.Exists(absolutePath))
                    {
                        content = File.ReadAllText(absolutePath);
                    }
                    else
                    {
                        error = Translate("File not found: ") + absolutePath;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                Dispatcher.UIThread.Post(delegate
                {
                    if (error != null)
                    {
                        ShowErrorFallback(error);
                        return;
                    }
                    ApplyConflictContent(content);
                });
            });
        }

        // 对照 WPF: Refresh → UnifiedMergeParser.TryParse → _mergeConflict + RefreshMergeEditorViews
        // spike 版：内联简单冲突标记解析（<<<<<<< / ======= / >>>>>>>）
        private void ApplyConflictContent(string content)
        {
            _originalContent = content ?? "";
            _currentContent = _originalContent;
            _conflictBlocks = ParseConflictBlocks(_originalContent);

            // 渲染 Current 视图（带冲突标记，用户可编辑）
            MergeEditor.Document.Text = _originalContent;
            _currentViewMode = MergeViewMode.Current;
            _suppressViewModeChanged = true;
            ViewModeComboBox.SelectedIndex = (int)MergeViewMode.Current;
            _suppressViewModeChanged = false;

            UpdateConflictStatus();
            SetStatus(ForkPlusDialogStatus.None, "");
        }

        // spike 版：内联简单冲突标记解析器
        // 对照 WPF: UnifiedMergeParser.TryParse → MergeConflict.ConflictChunk[]
        // 解析 <<<<<<< / ======= / >>>>>>> 三段冲突标记，返回 ConflictBlock 列表
        private static List<ConflictBlock> ParseConflictBlocks(string content)
        {
            var blocks = new List<ConflictBlock>();
            if (string.IsNullOrEmpty(content)) return blocks;

            string[] lines = content.Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    var block = new ConflictBlock
                    {
                        StartLine = i
                    };
                    i++;
                    // 收集 ours 行
                    while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    {
                        block.OursLines.Add(lines[i]);
                        i++;
                    }
                    if (i >= lines.Length)
                    {
                        // 缺少 ======= 标记，丢弃此块
                        break;
                    }
                    block.SeparatorLine = i;
                    i++;
                    // 收集 theirs 行
                    while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    {
                        block.TheirsLines.Add(lines[i]);
                        i++;
                    }
                    if (i >= lines.Length)
                    {
                        // 缺少 >>>>>>> 标记，丢弃此块
                        break;
                    }
                    block.EndLine = i;
                    blocks.Add(block);
                    i++;
                }
                else
                {
                    i++;
                }
            }
            return blocks;
        }

        // 对照 WPF: RefreshMergeStatusControls → ConflictStatus 显示 resolved/total
        // spike 版：根据当前编辑器内容中是否还有冲突标记显示状态
        private void UpdateConflictStatus()
        {
            int remaining = CountRemainingConflicts(MergeEditor.Document.Text);
            int total = _conflictBlocks.Count;
            if (total == 0)
            {
                ConflictStatusTextBlock.Text = Translate("No conflicts");
                ConflictStatusTextBlock.Foreground = null;
            }
            else if (remaining == 0)
            {
                ConflictStatusTextBlock.Text = Translate("All conflicts resolved");
                ConflictStatusTextBlock.Foreground = null;
            }
            else
            {
                ConflictStatusTextBlock.Text = FormatTranslate("Resolved {0} conflicts of {1}", total - remaining, total);
                ConflictStatusTextBlock.Foreground = (global::Avalonia.Media.IBrush)this.FindResource("ThemeHighlightBrush");
            }
            UpdateSubmitButton();
        }

        // spike 版：统计字符串中剩余的冲突标记数（<<<<<<< 出现次数）
        private static int CountRemainingConflicts(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf("<<<<<<<", idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += 7;
            }
            return count;
        }

        // 对照 WPF: ViewModeComboBox_SelectionChanged → 切换 LocalMergeEditor / RemoteMergeEditor / MergedMergeEditor
        // spike 版：根据 ComboBox 选择切换 AvaloniaEdit 显示内容（Current / Ours / Theirs / Merged）
        public void ViewModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressViewModeChanged) return;
            if (ViewModeComboBox.SelectedIndex < 0) return;
            var newMode = (MergeViewMode)ViewModeComboBox.SelectedIndex;
            if (newMode == _currentViewMode) return;

            // 切换前保存当前编辑器内容到 _currentContent（仅 Current 模式下用户可编辑）
            if (_currentViewMode == MergeViewMode.Current)
            {
                _currentContent = MergeEditor.Document.Text;
            }

            _currentViewMode = newMode;
            // Ours/Theirs/Merged 视图为只读，Current 视图可编辑
            bool isReadOnly = newMode != MergeViewMode.Current;
            MergeEditor.IsReadOnly = isReadOnly;
            MergeEditor.Document.Text = GetContentForMode(newMode);
        }

        // spike 版：根据视图模式生成对应内容
        // Current = _currentContent（用户当前编辑内容）
        // Ours = 仅保留 <<<<<<< ours 块内容
        // Theirs = 仅保留 >>>>>>> theirs 块内容
        // Merged = 去掉冲突标记，ours + theirs 都保留
        private string GetContentForMode(MergeViewMode mode)
        {
            if (mode == MergeViewMode.Current) return _currentContent;
            if (_conflictBlocks.Count == 0) return _originalContent;

            string[] lines = _originalContent.Split('\n');
            var result = new StringBuilder();
            int currentLine = 0;
            foreach (var block in _conflictBlocks)
            {
                // 添加冲突块之前的非冲突行
                while (currentLine < block.StartLine)
                {
                    result.Append(lines[currentLine]);
                    result.Append('\n');
                    currentLine++;
                }
                // 跳过 <<<<<<< 行
                currentLine = block.StartLine + 1;
                if (mode == MergeViewMode.Ours || mode == MergeViewMode.Merged)
                {
                    foreach (var ours in block.OursLines)
                    {
                        result.Append(ours);
                        result.Append('\n');
                    }
                }
                // 跳过 ======= 行
                currentLine = block.SeparatorLine + 1;
                if (mode == MergeViewMode.Theirs || mode == MergeViewMode.Merged)
                {
                    foreach (var theirs in block.TheirsLines)
                    {
                        result.Append(theirs);
                        result.Append('\n');
                    }
                }
                // 跳过 >>>>>>> 行
                currentLine = block.EndLine + 1;
            }
            // 添加剩余的非冲突行
            while (currentLine < lines.Length)
            {
                result.Append(lines[currentLine]);
                result.Append('\n');
                currentLine++;
            }
            // 移除末尾多余的 \n（如果原始内容不以 \n 结尾）
            string text = result.ToString();
            if (!_originalContent.EndsWith("\n") && text.EndsWith("\n"))
            {
                text = text.Substring(0, text.Length - 1);
            }
            return text;
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        // spike 版：剩余冲突数为 0 时允许提交（即所有冲突标记已被移除）
        protected override bool IsSubmitAllowed
        {
            get
            {
                // 切到非 Current 视图时，用户必须先回到 Current 才能保存（保证 _currentContent 同步）
                if (_currentViewMode != MergeViewMode.Current)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Switch to Current view to edit and save"));
                    return false;
                }
                // 当前内容无冲突标记才允许保存
                if (CountRemainingConflicts(MergeEditor.Document.Text) > 0)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Resolve all conflicts before saving"));
                    return false;
                }
                SetStatus(ForkPlusDialogStatus.None, "");
                return true;
            }
        }

        // 对照 WPF: OnSubmit → ResolveMergeConflictGitCommand → Close(gitResult)
        // spike 版：写入文件 + git add <path> + onResolved 回调 + Close
        protected override void OnSubmit()
        {
            if (_saved) return;
            if (!IsSubmitAllowed) return;

            string content = MergeEditor.Document.Text;
            string absolutePath;
            try
            {
                absolutePath = _gitModule.MakePath(_filePath);
            }
            catch (Exception ex)
            {
                ShowErrorFallback(ex.Message);
                return;
            }

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Saving..."));

            Task.Run(delegate
            {
                // 1. 写入文件
                try
                {
                    File.WriteAllText(absolutePath, content);
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        ShowErrorFallback(ex.Message);
                        EnableEditableControls();
                    });
                    return;
                }

                // 2. git add <path>
                try
                {
                    var gitCommand = new GitCommand("add", "--", _filePath);
                    var result = new GitRequest(_gitModule).Command(gitCommand).Execute(silent: true);
                    if (!result.Success)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            ShowErrorFallback(result.Stderr.Length > 0 ? result.Stderr : result.Stdout);
                            EnableEditableControls();
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        ShowErrorFallback(ex.Message);
                        EnableEditableControls();
                    });
                    return;
                }

                // 3. 触发 onResolved 回调 + Close
                Dispatcher.UIThread.Post(delegate
                {
                    _saved = true;
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Saved"));
                    try
                    {
                        _onResolved?.Invoke(_filePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("SideBySideMergeWindow onResolved callback failed", ex);
                    }
                    CloseWithOk();
                });
            });
        }

        // spike 版：手动禁用/启用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            MergeEditor.IsEnabled = false;
            ViewModeComboBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            MergeEditor.IsEnabled = true;
            ViewModeComboBox.IsEnabled = true;
        }

        // 对照 WPF: OnClosed → ForkPlusSettings.Default.Save()
        // spike 版：无设置持久化，仅做日志兜底
        private void SideBySideMergeWindow_Closed(object? sender, EventArgs e)
        {
            // spike 版：无 ForkPlusSettings.SideBySideMergeWindowLocationState 持久化
        }

        // spike 版：监听 AvaloniaEdit 文本变化以更新冲突状态
        // 对照 WPF: Document_Changing → 校验合并内容编辑
        // spike 版简化：仅更新 ConflictStatusTextBlock + UpdateSubmitButton
        protected override void OnInitialized()
        {
            base.OnInitialized();
            // 监听文档变化更新状态
            try
            {
                MergeEditor.Document.TextChanged += (_, _) =>
                {
                    if (_currentViewMode == MergeViewMode.Current)
                    {
                        UpdateConflictStatus();
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warn("SideBySideMergeWindow: failed to subscribe TextChanged", ex);
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
