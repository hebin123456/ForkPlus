using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionFileTreeUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionFileTreeUserControl.xaml.cs（360 行）：
    //   - 公共方法：Refresh(Sha sha) / ShowRevisionDetails(string filePath)
    //   - 公共属性：RevisionDetailsUserControl（反向引用）
    //   - 异步 Task：GetRevisionFileTreeGitCommand（git ls-tree）+ UpdateFileDetails
    //   - 3 个 CommandBindings（OpenFileInDefaultEditor / CopyFilePaths / CopyAbsoluteFilePaths）
    //   - CreateFileTreeViewContextMenuItems（约 100 行右键菜单）
    //   - _pendingFilePath 竞态处理（RootItem 未就绪时缓存）
    //
    // 装入路径（WPF）：
    //   RevisionDetailsUserControl.xaml Row 2 → RevisionFileTreeUserControl
    //   由 RevisionDetails.ShowRevisionDetails → UpdateTabContent(FileTree tab) → Refresh(sha) 触发
    //
    // Avalonia 版差异：
    //   - WPF MultiselectionTreeView → 原生 TreeView + TreeDataTemplate
    //   - WPF RevisionFileTreeViewItem : MultiselectionTreeViewItem → POCO
    //   - WPF MouseDoubleClick → DoubleTapped
    //   - WPF Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   - WPF Task + TaskScheduler.FromCurrentSynchronizationContext → Task.Run + Dispatcher.UIThread.Post
    //
    // spike 简化：
    //   - 用 TreeView 显示文件树，TreeViewItem 绑定 RevisionFileTreeViewItem POCO
    //   - SetRevision(sha) / Refresh() 用 Task.Run 后台填充 + Dispatcher.UIThread.Post 回 UI
    //   - ShowRevisionDetails(filePath) 展开到指定文件路径
    //   - 右键菜单 / CommandBindings / git ls-tree 真实调用暂不实现
    public partial class RevisionFileTreeUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型 RevisionDetailsUserControl 待后续阶段补
        public object RevisionDetailsUserControl { get; set; }

        // 当前 sha（spike 用 string 占位，真实类型 Sha）
        private string _sha;
        // 待展开的文件路径（RootItem 未就绪时缓存）
        private string _pendingFilePath;

        public RevisionFileTreeUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: Refresh(Sha sha)
        //   异步执行 GetRevisionFileTreeGitCommand（git ls-tree），构建根 TreeView 节点
        //   spike 版：用 Task.Run 后台构建 + Dispatcher.UIThread.Post 回 UI 线程
        public void Refresh(object sha)
        {
            _sha = sha?.ToString();
            Console.WriteLine($"[RevisionFileTree] Refresh: sha={_sha}");

            // spike 版：构建一个示例根节点（真实 git ls-tree 调用留待后续阶段）
            Task.Run(() =>
            {
                var root = new RevisionFileTreeViewItem("root", "", "Directory");

                // spike 占位：添加示例子节点（真实数据来自 git ls-tree）
                root.Children.Add(new RevisionFileTreeViewItem("src", "src", "Directory"));
                root.Children.Add(new RevisionFileTreeViewItem("README.md", "README.md", "File"));

                Dispatcher.UIThread.Post(() =>
                {
                    if (FilesTreeView != null)
                    {
                        FilesTreeView.Items.Clear();
                        // 添加根节点的子节点作为顶层 TreeView 项
                        foreach (var child in root.Children)
                        {
                            FilesTreeView.Items.Add(child);
                        }
                    }

                    // 若有待展开的文件路径（来自 ShowRevisionDetails），现在执行展开
                    if (_pendingFilePath != null)
                    {
                        ShowRevisionDetails(_pendingFilePath);
                        _pendingFilePath = null;
                    }
                });
            });
        }

        // 对照 WPF: ShowRevisionDetails(string filePath)
        //   展开到指定文件路径（处理 RootItem 未就绪的 _pendingFilePath 竞态）
        public void ShowRevisionDetails(string filePath)
        {
            Console.WriteLine($"[RevisionFileTree] ShowRevisionDetails: {filePath}");

            // TreeView 可能尚未就绪（Refresh 是异步的），保存路径等异步回调完成后展开
            if (FilesTreeView == null || FilesTreeView.Items.Count == 0)
            {
                _pendingFilePath = filePath;
                return;
            }

            // spike 版：简单展开，不递归（真实逻辑需要按 '/' 分割路径逐层展开）
            string[] pathComponents = filePath.Split('/');
            ExpandPath(pathComponents, FilesTreeView.Items);
        }

        // spike 新增：Initialize(object repositoryUserControl)
        //   注入父控件引用（对照 WPF 通过 RevisionDetailsUserControl 属性注入）
        public void Initialize(object repositoryUserControl)
        {
            Console.WriteLine("[RevisionFileTree] Initialize (spike placeholder)");
            RevisionDetailsUserControl = repositoryUserControl;
        }

        // ===== 事件处理 =====

        // 对照 WPF: FilesTreeView_MouseDoubleClick
        //   WPF 版双击展开/折叠目录节点
        //   spike 版：简单切换展开状态
        private void FilesTreeView_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView treeView && treeView.SelectedItem is RevisionFileTreeViewItem item)
            {
                item.IsExpanded = !item.IsExpanded;
            }
        }

        // ===== 辅助方法 =====

        // 递归展开路径到指定文件（对照 WPF Expand 方法）
        private void ExpandPath(string[] pathComponents, System.Collections.IEnumerable items)
        {
            if (pathComponents.Length == 0) return;

            string current = pathComponents[0];
            foreach (var item in items)
            {
                if (item is RevisionFileTreeViewItem treeItem && treeItem.Title == current)
                {
                    if (treeItem.ItemType == "Directory")
                    {
                        treeItem.IsExpanded = true;
                        if (pathComponents.Length > 1)
                        {
                            ExpandPath(SubArray(pathComponents, 1), treeItem.Children);
                        }
                    }
                    break;
                }
            }
        }

        private static T[] SubArray<T>(T[] array, int start)
        {
            T[] result = new T[array.Length - start];
            for (int i = start; i < array.Length; i++)
                result[i - start] = array[i];
            return result;
        }
    }
}
