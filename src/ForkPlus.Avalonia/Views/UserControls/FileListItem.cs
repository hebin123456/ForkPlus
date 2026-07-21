using System.Collections.ObjectModel;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 FileListItem POCO（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileListItem.cs（101 行）：
    //   WPF 版继承 MultiselectionTreeViewItem（自定义 TreeViewItem），含：
    //   - ChangedFile 属性（git 变更文件）
    //   - ChangeTypeIcon / FileTypeIcon（ImageSource，PNG 图标）
    //   - FileName / FolderPath / ToolTip 属性
    //   - MatchFilter override（按路径过滤）
    //   - ByTypeThenByTitlePredicate 排序比较器
    //   - StartDrag / GetDataObject 拖拽支持
    //
    // Avalonia 版差异：
    //   - 不继承 TreeViewItem，改为纯 POCO（Avalonia TreeView 用 DataTemplate 绑定）
    //   - WPF ImageSource ChangeTypeIcon → string ChangeTypeEmoji（spike 用 emoji）
    //   - WPF MatchFilter → 外部 SetFilter 方法处理
    //   - WPF StartDrag/GetDataObject → 暂不实现拖拽
    //
    // spike 简化：
    //   - ChangeType emoji：Added=✨ / Modified=📝 / Deleted=🗑 / Renamed=🔀 / Copied=📋
    //   - Children 用 ObservableCollection 支持 UI 自动更新
    //   - 不调 git 命令，由 FileListUserControl.Refresh 填充
    public class FileListItem
    {
        // 完整路径（相对仓库根目录）
        public string Path { get; set; } = string.Empty;

        // 文件名（Path 的 GetFileName 部分）
        public string FileName { get; set; } = string.Empty;

        // 变更类型 emoji
        public string ChangeTypeEmoji { get; set; } = "";

        // 是否为目录
        public bool IsDirectory { get; set; }

        // 是否可见（用于过滤）
        public bool IsVisible { get; set; } = true;

        // 子节点（TreeView HierarchicalDataTemplate 绑定）
        public ObservableCollection<FileListItem> Children { get; set; }
            = new ObservableCollection<FileListItem>();

        public FileListItem()
        {
        }

        public FileListItem(string path, string changeType = "", bool isDirectory = false)
        {
            Path = path;
            FileName = System.IO.Path.GetFileName(path);
            IsDirectory = isDirectory;
            ChangeTypeEmoji = GetChangeTypeEmoji(changeType);
        }

        // 变更类型 → emoji 映射
        public static string GetChangeTypeEmoji(string changeType)
        {
            return changeType?.ToLowerInvariant() switch
            {
                "added" => "✨",
                "modified" => "📝",
                "deleted" => "🗑",
                "renamed" => "🔀",
                "copied" => "📋",
                "untracked" => "?",
                _ => ""
            };
        }
    }
}
