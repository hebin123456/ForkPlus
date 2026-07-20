using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.6：Avalonia 版 RevisionFileTreeUserControl 骨架（spike 简化版）。
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
    // 本 spike 版暂不迁移：
    //   - MultiselectionTreeView 自定义控件（用 TreeView 占位）
    //   - FileContentControl 自定义控件（用 ContentControl 占位）
    //   - RevisionFileTreeViewItem 自定义 TreeViewItem（不迁移）
    //   - 异步 Task（GetRevisionFileTreeGitCommand + UpdateFileDetails）
    //   - 3 个 CommandBindings
    //   - 右键菜单构造方法（约 100 行）
    //   - _pendingFilePath 竞态处理
    //   - GridSplitter 持久化列宽
    //
    // 本 spike 版验证：
    //   - Grid 2 列布局正确显示
    //   - 左侧 TreeView 占位可见
    //   - 右侧 FileContent 占位可见
    //   - GridSplitter 可拖动
    public partial class RevisionFileTreeUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型 RevisionDetailsUserControl 待 Phase 3.6 后续子阶段补
        public object RevisionDetailsUserControl { get; set; }

        public RevisionFileTreeUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF 2 个公共方法签名，body stub）=====

        // 对照 WPF: public void Refresh(Sha sha)
        //   异步执行 GetRevisionFileTreeGitCommand（git ls-tree），构建根 TreeView 节点
        public void Refresh(object sha)
        {
            Console.WriteLine($"[RevisionFileTree] Refresh (spike placeholder): sha={sha}");
        }

        // 对照 WPF: public void ShowRevisionDetails(string filePath)
        //   展开到指定文件路径（处理 RootItem 未就绪的 _pendingFilePath 竞态）
        public void ShowRevisionDetails(string filePath)
        {
            Console.WriteLine($"[RevisionFileTree] ShowRevisionDetails (spike placeholder): {filePath}");
        }
    }
}
