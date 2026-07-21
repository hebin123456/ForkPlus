using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 DiffEntry POCO（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/DiffEntry.cs（96 行）：
    //   WPF 版继承 INotifyPropertyChanged，含：
    //   - RepositoryUserControl / ChangedFile / FilePath 属性
    //   - FileTypeIcon / ChangeTypeIcon（ImageSource，PNG 图标）
    //   - Content (GitCommandResult<DiffContent>) / IsExpanded 属性
    //   - PropertyChanged 事件
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF DiffEntry 在 ForkPlus.UI.UserControls 命名空间（WPF 工程，Avalonia 不可访问）
    //     → 本 spike POCO 定义在 ForkPlus.Avalonia.Views.UserControls 命名空间
    //   - WPF ImageSource FileTypeIcon/ChangeTypeIcon → string FileTypeEmoji/ChangeTypeEmoji
    //   - WPF RepositoryUserControl（WPF 类型）→ object（spike 占位）
    //   - WPF Content (GitCommandResult<DiffContent>) → object（spike 占位，不调 git）
    //   - 保留 INotifyPropertyChanged + IsExpanded 属性（DiffEntryRowUserControl 依赖）
    //   - 新增 AddedLines / DeletedLines（task spec 要求显示增删行数）
    public class DiffEntry : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private object _content;

        // 完整文件路径（对照 WPF: FilePath => ChangedFile.Path）
        public string FilePath { get; set; } = string.Empty;

        // 变更文件（对照 WPF: ChangedFile，来自 ForkPlus.Git Core 命名空间）
        public ChangedFile ChangedFile { get; set; }

        // 变更类型 emoji（对照 WPF: ChangeTypeIcon ImageSource）
        public string ChangeTypeEmoji { get; set; } = "";

        // 文件类型 emoji（对照 WPF: FileTypeIcon ImageSource）
        public string FileTypeEmoji { get; set; } = "📄";

        // 增加行数（task spec：显示增删行数）
        public int AddedLines { get; set; }

        // 删除行数（task spec：显示增删行数）
        public int DeletedLines { get; set; }

        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; set; }

        // 内容（对照 WPF: Content GitCommandResult<DiffContent>）
        public object Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
                }
            }
        }

        // 是否展开（对照 WPF: IsExpanded，完整迁移 INotifyPropertyChanged）
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DiffEntry()
        {
        }

        public DiffEntry(string filePath, ChangedFile changedFile = null)
        {
            FilePath = filePath;
            ChangedFile = changedFile;
        }
    }
}
