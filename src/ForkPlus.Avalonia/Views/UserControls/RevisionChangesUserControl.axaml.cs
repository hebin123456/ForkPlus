using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionChangesUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionChangesUserControl.xaml.cs（619 行）：
    //   - 构造函数订阅 FileListUserControl.SelectionChanged + FilterTextBox.FilterRequestChanged
    //   - 公共属性：FileListsMode / RevisionDetailsUserControl / SelectedFile
    //   - 公共方法：ApplyLocalization / Refresh(RevisionDiffTarget target, string fileToSelect)
    //   - 私有 _refreshFilterAction / _updateDiffAction（DelayedAction 延迟执行）
    //   - UpdateFileList(fileToSelect) → FileListUserControl.SetItemSource
    //   - UpdateDiff(changedFile) → 异步 GetRevisionFileChangesGitCommand → FileDiffControl.Content
    //   - CreateFileListContextMenuItems（约 150 行右键菜单）
    //   - DiffPopupWindow 弹窗 + 列宽持久化
    //
    // 装入路径（WPF）：
    //   RevisionDetailsUserControl.xaml Row 2 → RevisionChangesUserControl（Changes tab）
    //
    // Avalonia 版差异：
    //   - WPF FileListUserControl → ListBox（spike 简化）
    //   - WPF FileDiffControl → AvaloniaEdit.TextEditor（spike 简化）
    //   - WPF DispatcherTimer / DelayedAction → spike 不实现延迟
    //   - WPF CommandBindings → spike 不实现快捷键
    //   - WPF JobQueue → Task.Run + Dispatcher.UIThread.Post
    //   - 构造函数签名：(IServiceProvider serviceProvider)
    //
    // spike 简化：
    //   - 用 ListBox 显示文件列表
    //   - 用 AvaloniaEdit.TextEditor 显示 diff
    //   - SetRevision(sha) / Refresh() / Initialize(repositoryUserControl) 公共方法
    //   - FileSelected 事件在 ListBox 选择变化时触发
    //   - 右键菜单 / DiffPopupWindow / 列宽持久化暂不实现
    public partial class RevisionChangesUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler<EventArgs> FileSelected;

        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型 RevisionDetailsUserControl
        public object RevisionDetailsUserControl { get; set; }

        // 当前选中的文件（spike 用 FileListItem）
        public object SelectedFile { get; private set; }

        // 当前 sha（spike 用 string 占位，真实类型 Sha）
        public string Sha { get; private set; }

        // 文件列表数据源（spike 用 FileListItem POCO）
        private ObservableCollection<FileListItem> _fileItems;

        public RevisionChangesUserControl(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            Console.WriteLine("[RevisionChanges] Constructed (spike)");
            _fileItems = new ObservableCollection<FileListItem>();
            if (FileListBox != null)
            {
                FileListBox.ItemsSource = _fileItems;
            }
        }

        // ===== 公共方法（对照 WPF + task spec）=====

        // 对照 task spec: Initialize(RepositoryUserControl)
        //   注入父控件引用
        public void Initialize(object repositoryUserControl)
        {
            Console.WriteLine("[RevisionChanges] Initialize");
            RevisionDetailsUserControl = repositoryUserControl;
        }

        // 对照 task spec: SetRevision(string sha)
        //   设置当前 revision 的 sha，触发文件列表刷新
        public void SetRevision(string sha)
        {
            Sha = sha;
            Console.WriteLine($"[RevisionChanges] SetRevision: sha={sha}");
            Refresh();
        }

        // 对照 task spec: Refresh()
        //   刷新文件列表（spike 版：用 Task.Run 后台加载 + Dispatcher.UIThread.Post 回 UI）
        public void Refresh()
        {
            Console.WriteLine($"[RevisionChanges] Refresh: sha={Sha}");

            // spike 版：用 Task.Run 后台加载文件列表（真实数据来自 git diff 命令）
            Task.Run(() =>
            {
                // spike 占位：构建示例文件列表（真实数据来自 GetRevisionFileChangesGitCommand）
                var files = new ObservableCollection<FileListItem>();
                files.Add(new FileListItem("src/Program.cs", "Modified", false));
                files.Add(new FileListItem("src/Services/MyService.cs", "Added", false));
                files.Add(new FileListItem("README.md", "Modified", false));
                files.Add(new FileListItem("old/config.txt", "Deleted", false));

                Dispatcher.UIThread.Post(() =>
                {
                    if (_fileItems != null)
                    {
                        _fileItems.Clear();
                        foreach (var file in files)
                        {
                            _fileItems.Add(file);
                        }
                    }

                    // spike 版：自动选中第一个文件
                    if (FileListBox != null && FileListBox.Items.Count > 0)
                    {
                        FileListBox.SelectedIndex = 0;
                    }
                });
            });
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[RevisionChanges] ApplyLocalization (spike placeholder)");
        }

        // 对照 WPF: Refresh(RevisionDiffTarget target, string fileToSelect)
        //   spike 版：简化为只刷新文件列表
        public void Refresh(object target, string fileToSelect)
        {
            Console.WriteLine($"[RevisionChanges] Refresh(target, fileToSelect): {fileToSelect}");
            Refresh();

            if (fileToSelect != null)
            {
                // spike 版：选中指定文件
                Dispatcher.UIThread.Post(() =>
                {
                    if (FileListBox != null)
                    {
                        foreach (var item in FileListBox.Items)
                        {
                            if (item is FileListItem fileItem && fileItem.Path == fileToSelect)
                            {
                                FileListBox.SelectedItem = fileItem;
                                break;
                            }
                        }
                    }
                });
            }
        }

        // ===== 事件处理 =====

        // 对照 WPF: FileListUserControl_SelectionChanged
        //   spike 版：ListBox 选择变化时更新 diff 显示
        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedFile = FileListBox?.SelectedItem;
            FileSelected?.Invoke(this, EventArgs.Empty);

            // spike 版：更新 diff 显示
            if (SelectedFile is FileListItem fileItem)
            {
                UpdateDiff(fileItem);
            }
        }

        // 对照 WPF: UpdateDiff(ChangedFile changedFile)
        //   spike 版：用 Task.Run 后台加载 diff + Dispatcher.UIThread.Post 回 UI
        private void UpdateDiff(FileListItem fileItem)
        {
            if (fileItem == null)
            {
                if (DiffEditor != null) DiffEditor.Document.Text = "";
                return;
            }

            Console.WriteLine($"[RevisionChanges] UpdateDiff: {fileItem.Path}");

            // spike 版：用 Task.Run 后台加载 diff（真实数据来自 GetRevisionFileChangesGitCommand）
            string filePath = fileItem.Path;
            Task.Run(() =>
            {
                // spike 占位：生成示例 diff 文本（真实数据来自 git diff 命令）
                string diffText = $"diff --git a/{filePath} b/{filePath}\n";
                diffText += $"index 1234567..abcdefg 100644\n";
                diffText += $"--- a/{filePath}\n";
                diffText += $"+++ b/{filePath}\n";
                diffText += "@@ -1,5 +1,8 @@\n";
                diffText += " line 1\n";
                diffText += " line 2\n";
                diffText += "+added line\n";
                diffText += " line 3\n";
                diffText += "-removed line\n";
                diffText += " line 4\n";

                Dispatcher.UIThread.Post(() =>
                {
                    if (DiffEditor != null)
                    {
                        DiffEditor.Document.Text = diffText;
                    }
                });
            });
        }
    }
}
