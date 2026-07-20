using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.7：Avalonia 版 FileListUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileListUserControl.xaml.cs（847 行）：
    //   - 4 个公共事件：ItemDoubleClick / ItemsDrop / SelectionChanged / ColumnHeaderSizeChanged
    //   - 公共属性：EnableMultiSelection / Mode / Items / SelectedItems /
    //     ExpandedSelectedItems / FilterString / ContainsVisibleItems
    //   - 11 个公共方法：SetItemSource / SetItemSourceAsync / Refresh /
    //     SelectFile / FocusSelectedElement / SelectPreviousFile / SelectNextFile /
    //     SelectFirstAvailableFile 等
    //   - 增量树构建算法（ArrayDiff.Diff + ApplyAddedEntries + ApplyRemovedEntries +
    //     BinarySearch + FindOrCreateFolder + DeleteItem 递归）
    //   - 大列表后台构建（>= 5000 项时 Task.Run）
    //   - FileListMode 三模式切换（Tree/List/CombinedList）
    //
    // 装入路径（WPF）：
    //   RevisionChangesUserControl.xaml Row 2 → FileListUserControl
    //   StageFileUserControl.xaml / CommitUserControl.xaml.cs / AiCodeReviewWindow.xaml 也用
    //
    // 本 spike 版暂不迁移：
    //   - FileListTreeView 自定义控件（用 TreeView 占位）
    //   - AutoTooltipTextBlock 自定义控件（用 TextBlock 占位）
    //   - GridView 2 列布局（Avalonia 无 GridView，spike 用空 TreeView）
    //   - DataTrigger（ChangeTypeIcon null 时隐藏 Image）
    //   - 增量树构建算法（ArrayDiff.Diff 等）
    //   - 大列表后台构建（Task.Run）
    //   - FileListMode 三模式切换
    //   - 拖拽 Drop 事件
    //
    // 本 spike 版验证：
    //   - 顶层 TreeView 占位可见
    public partial class FileListUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF 4 个公共事件）=====
        // spike 版只声明不触发
        public event EventHandler<EventArgs> ItemDoubleClick;
        public event EventHandler<EventArgs> ItemsDrop;
        public event EventHandler<EventArgs> SelectionChanged;
        public event EventHandler<EventArgs> ColumnHeaderSizeChanged;

        // ===== 公共属性（对照 WPF）=====
        // spike 版用简单类型占位，真实类型待 Phase 3.7 后续子阶段补
        public bool EnableMultiSelection { get; set; }
        public string Mode { get; private set; } = "Tree"; // 对照 WPF FileListMode.Tree
        public IReadOnlyList<object> Items { get; private set; } = new List<object>();
        public IReadOnlyList<object> SelectedItems { get; private set; } = new List<object>();
        public IReadOnlyList<object> ExpandedSelectedItems { get; private set; } = new List<object>();
        public string FilterString { get; set; }
        public bool ContainsVisibleItems { get; private set; }

        public FileListUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF 11 个公共方法签名，body stub）=====

        // 对照 WPF: public void SetItemSource(ChangedFile[] source, bool forceRefresh, bool restoreSelection)
        //   同步增量大批量更新（含 BulkRebuildChangeThreshold=256 阈值）
        public void SetItemSource(object source, bool forceRefresh, bool restoreSelection)
        {
            Console.WriteLine($"[FileList] SetItemSource (spike placeholder): forceRefresh={forceRefresh}, restoreSelection={restoreSelection}");
        }

        // 对照 WPF: public Task SetItemSourceAsync(...)
        //   异步版本，变更数 >= 5000 用 Task.Run 后台构建树
        public System.Threading.Tasks.Task SetItemSourceAsync(object source, bool forceRefresh, bool restoreSelection)
        {
            Console.WriteLine($"[FileList] SetItemSourceAsync (spike placeholder): forceRefresh={forceRefresh}");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        // 对照 WPF: public void Refresh()
        public void Refresh()
        {
            Console.WriteLine("[FileList] Refresh (spike placeholder)");
        }

        // 对照 WPF: public void SelectFile(string filePath)
        public void SelectFile(string filePath)
        {
            Console.WriteLine($"[FileList] SelectFile (spike placeholder): {filePath}");
        }

        // 对照 WPF: public void FocusSelectedElement()
        public void FocusSelectedElement()
        {
            Console.WriteLine("[FileList] FocusSelectedElement (spike placeholder)");
        }

        // 对照 WPF: public void SelectPreviousFile()
        public void SelectPreviousFile()
        {
            Console.WriteLine("[FileList] SelectPreviousFile (spike placeholder)");
        }

        // 对照 WPF: public void SelectNextFile()
        public void SelectNextFile()
        {
            Console.WriteLine("[FileList] SelectNextFile (spike placeholder)");
        }

        // 对照 WPF: public void SelectFirstAvailableFile()
        public void SelectFirstAvailableFile()
        {
            Console.WriteLine("[FileList] SelectFirstAvailableFile (spike placeholder)");
        }
    }
}
