using System.Collections.ObjectModel;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionFileTreeViewItem POCO（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionFileTreeViewItem.cs（64 行）：
    //   WPF 版继承 MultiselectionTreeViewItem（自定义 TreeViewItem），含：
    //   - FileTreeItem 属性（git ls-tree 返回的文件树节点）
    //   - FileTypeIcon（ImageSource，按扩展名加载 PNG 图标）
    //   - ShowExpander override（Directory 类型才显示展开箭头）
    //   - OnExpanding override（异步调 git ls-tree 加载子节点）
    //
    // Avalonia 版差异：
    //   - 不继承 TreeViewItem，改为纯 POCO（Avalonia TreeView 用 DataTemplate 绑定）
    //   - WPF ImageSource FileTypeIcon → string IconEmoji（spike 用 emoji 替代 PNG）
    //   - WPF ShowExpander override → IsDirectory 属性（TreeView ItemTemplate 用 IsVisible 绑定）
    //   - WPF OnExpanding git ls-tree → 外部 Refresh 方法填充 Children
    //
    // spike 简化：
    //   - Folder=📁 / File=(空，用文件名自身)
    //   - Children 用 ObservableCollection 支持 UI 自动更新
    //   - 不调 git 命令，由 RevisionFileTreeUserControl.Refresh 填充
    public class RevisionFileTreeViewItem
    {
        // 显示名称（文件名或目录名）
        public string Title { get; set; } = string.Empty;

        // 完整路径（相对仓库根目录）
        public string FilePath { get; set; } = string.Empty;

        // 节点类型：File / Directory / Submodule
        public string ItemType { get; set; } = "File";

        // emoji 图标（spike 用 emoji 替代 PNG）
        public string IconEmoji => ItemType switch
        {
            "Directory" => "📁",
            "Submodule" => "📦",
            _ => "📄"
        };

        // 子节点（TreeView HierarchicalDataTemplate 绑定）
        public ObservableCollection<RevisionFileTreeViewItem> Children { get; set; }
            = new ObservableCollection<RevisionFileTreeViewItem>();

        // 是否已展开
        public bool IsExpanded { get; set; }

        public RevisionFileTreeViewItem()
        {
        }

        public RevisionFileTreeViewItem(string title, string filePath, string itemType = "File")
        {
            Title = title;
            FilePath = filePath;
            ItemType = itemType;
        }
    }
}
