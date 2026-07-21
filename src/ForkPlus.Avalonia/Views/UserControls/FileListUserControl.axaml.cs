using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 FileListUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileListUserControl.xaml.cs（847 行）：
    //   - 4 个公共事件：ItemDoubleClick / ItemsDrop / SelectionChanged / ColumnHeaderSizeChanged
    //   - 公共属性：EnableMultiSelection / Mode / Items / SelectedItems /
    //     ExpandedSelectedItems / FilterString / ContainsVisibleItems
    //   - 11 个公共方法：SetItemSource / SetItemSourceAsync / Refresh /
    //     SelectFile / FocusSelectedElement / SelectPreviousFile / SelectNextFile /
    //     SelectFirstAvailableFile 等
    //   - 增量树构建算法（ArrayDiff.Diff + ApplyAddedEntries + ApplyRemovedEntries）
    //   - 大列表后台构建（>= 5000 项时 Task.Run）
    //   - FileListMode 三模式切换（Tree/List/CombinedList）
    //
    // 装入路径（WPF）：
    //   RevisionChangesUserControl.xaml Row 2 → FileListUserControl
    //   StageFileUserControl.xaml / CommitUserControl.xaml.cs / AiCodeReviewWindow.xaml 也用
    //
    // Avalonia 版差异：
    //   - WPF FileListTreeView（继承 MultiselectionTreeView）→ 原生 TreeView
    //   - WPF FileListItem : MultiselectionTreeViewItem → POCO（FileListItem.cs）
    //   - WPF GridView 2 列 → TreeDataTemplate 单列
    //   - WPF DataTrigger → emoji 绑定
    //   - WPF MouseDoubleClick → DoubleTapped
    //
    // spike 简化：
    //   - 用 TreeView 显示文件树，FileListItem POCO 绑定
    //   - Refresh(ChangedFile[]) 用 ObservableCollection 填充 TreeView
    //   - SetFilter(string) 按路径过滤（spike 用 IsVisible 标记）
    //   - FileSelected 事件在选择变化时触发
    //   - 增量树构建 / 虚拟化 / 拖拽 / 三模式切换暂不实现
    public partial class FileListUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        // 对照 WPF: public event EventHandler<FileListEventArgs> SelectionChanged
        public event EventHandler<EventArgs> FileSelected;
        public event EventHandler<EventArgs> ItemDoubleClick;
        public event EventHandler<EventArgs> SelectionChanged;

        // ===== 公共属性（对照 WPF）=====
        public bool EnableMultiSelection { get; set; }
        public string Mode { get; set; } = "Tree"; // 对照 WPF FileListMode.Tree
        public IReadOnlyList<object> Items { get; private set; } = new List<object>();
        public object SelectedItem { get; private set; }
        public string FilterString { get; set; }
        public bool ContainsVisibleItems { get; private set; }

        // Avalonia TreeView 没有 SelectedIndex（WPF 有），spike 版用私有字段跟踪
        private int _selectedIndex = -1;

        public FileListUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // spike 新增：Initialize(object repositoryUserControl)
        //   注入父控件引用
        public void Initialize(object repositoryUserControl)
        {
            Console.WriteLine("[FileList] Initialize (spike placeholder)");
        }

        // 对照 WPF: public void Refresh(ChangedFile[])
        //   spike 版：从 ChangedFile[] 构建 FileListItem 树并填充 TreeView
        public void Refresh(object changedFiles)
        {
            Console.WriteLine("[FileList] Refresh (spike)");

            if (TreeView == null) return;

            TreeView.Items.Clear();

            // spike 版：changedFiles 可能是 ChangedFile[] 或 object[]
            // 真实类型 ChangedFile 有 Path / ChangeType / IsDirectory 属性
            // spike 用反射简化处理
            if (changedFiles is System.Collections.IEnumerable enumerable)
            {
                foreach (var file in enumerable)
                {
                    var path = file?.GetType().GetProperty("Path")?.GetValue(file) as string;
                    var changeType = file?.GetType().GetProperty("ChangeType")?.GetValue(file)?.ToString();
                    var isDir = file?.GetType().GetProperty("IsDirectory")?.GetValue(file) as bool?;

                    if (path != null)
                    {
                        var item = new FileListItem(path, changeType ?? "", isDir ?? false);
                        TreeView.Items.Add(item);
                    }
                }
            }

            ContainsVisibleItems = TreeView.Items.Count > 0;
        }

        // 对照 WPF: public void SetFilter(string filterString)
        //   spike 版：按路径过滤（标记 IsVisible）
        public void SetFilter(string filterString)
        {
            FilterString = filterString;
            Console.WriteLine($"[FileList] SetFilter: {filterString}");

            if (string.IsNullOrEmpty(filterString))
            {
                // 清除过滤：所有项可见
                foreach (var item in TreeView.Items)
                {
                    if (item is FileListItem fileItem)
                    {
                        fileItem.IsVisible = true;
                    }
                }
            }
            else
            {
                // 按路径过滤
                foreach (var item in TreeView.Items)
                {
                    if (item is FileListItem fileItem)
                    {
                        fileItem.IsVisible = fileItem.Path?.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
            }
        }

        // 对照 WPF: public void SelectFile(string filePath)
        public void SelectFile(string filePath)
        {
            Console.WriteLine($"[FileList] SelectFile: {filePath}");
            if (TreeView == null || string.IsNullOrEmpty(filePath)) return;

            foreach (var item in TreeView.Items)
            {
                if (item is FileListItem fileItem && fileItem.Path == filePath)
                {
                    TreeView.SelectedItem = fileItem;
                    break;
                }
            }
        }

        // 对照 WPF: public void SelectFirstAvailableFile()
        //   spike: Avalonia TreeView 无 SelectedIndex 属性，用 SelectedItem 占位
        public void SelectFirstAvailableFile()
        {
            Console.WriteLine("[FileList] SelectFirstAvailableFile");
            if (TreeView != null && TreeView.Items.Count > 0)
            {
                TreeView.SelectedItem = TreeView.Items[0];
            }
        }

        // 对照 WPF: public void SelectPreviousFile()
        //   spike: Avalonia TreeView 无 SelectedIndex，spike 版 no-op
        public void SelectPreviousFile()
        {
            Console.WriteLine("[FileList] SelectPreviousFile (spike no-op: Avalonia TreeView has no SelectedIndex)");
        }

        // 对照 WPF: public void SelectNextFile()
        //   spike: Avalonia TreeView 无 SelectedIndex，spike 版 no-op
        public void SelectNextFile()
        {
            Console.WriteLine("[FileList] SelectNextFile (spike no-op: Avalonia TreeView has no SelectedIndex)");
        }

        // 对照 WPF: public void FocusSelectedElement()
        public void FocusSelectedElement()
        {
            Console.WriteLine("[FileList] FocusSelectedElement (spike placeholder)");
        }

        // ===== 事件处理 =====

        // 对照 WPF: SelectionChanged 事件
        private void TreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = TreeView?.SelectedItem;
            FileSelected?.Invoke(this, EventArgs.Empty);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: MouseDoubleClick
        private void TreeView_DoubleTapped(object sender, RoutedEventArgs e)
        {
            ItemDoubleClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
