using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Git;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.8：Avalonia 版 StageFileUserControl（完整迁移版 — 单文件行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/StageFileUserControl.xaml.cs（498 行）：
    //   WPF 版是双列表控件（unstaged + staged 两个 FileListUserControl）：
    //   - 13 个公共属性（AllUnstagedFiles/AllStagedFiles/ExpandedUnstagedFiles/.../StagedItemsCount/
    //     ContainsUnmergedItems/FileListsMode/Enabled/IsStagedListSelected/IsUnstagedListSelected/IsFiltered）
    //   - 7 个公共事件（SelectionChanged/StagedFilesItemSourceChanged/ShowDiffPopup/Unstage/Stage/
    //     StageAll/UnstageAll/UnstagedFilesContextMenuOpening/StagedFilesContextMenuOpening/FileListSettingsMenuOpened）
    //   - 8 个公共方法（SetData/SetDataAsync/ApplyLocalization/RefreshStageAllButton/RefreshStageButtons/
    //     FocusActiveListView/SelectNext/SelectPrevious/RefreshUnstagedStatusLabel）
    //   - 双 FileListUserControl 拖拽 stage/unstage（ItemsDrop）
    //   - FilterTextBox 过滤（DelayedAction 0.1s 防抖）
    //   - FileListMode 三模式切换（Tree/List/CombinedList）
    //   - PreviewKeyDown：Ctrl+F 显示过滤 / Space 显示 diff popup / Escape 隐藏过滤
    //   - NotificationCenter.FileListModeChanged 订阅
    //   - LargeFileListAutoSelectionThreshold=5000（大列表跳过自动选中）
    //
    // Avalonia 版差异（spike 简化为单文件行控件）：
    //   - 双列表（unstaged + staged FileListUserControl）→ 单文件行（CheckBox + 文件名 + 状态 emoji）
    //   - task spec API：SetFile(ChangedFile) / IsStaged 属性 / IsStagedChanged 事件
    //   - 状态图标 PNG → emoji TextBlock（M=📝 / A=✨ / D=🗑 / R=🔀）
    //   - 拖拽 stage/unstage → 不迁移（单行无需拖拽）
    //   - FilterTextBox 过滤 → 不迁移
    //   - FileListMode 三模式 → 不迁移
    //   - NotificationCenter 不可访问 → 不订阅 FileListModeChanged
    //   - ChangedFile 在 ForkPlus.Git 命名空间（Core 工程，Avalonia 工程可访问）
    //
    // 本 spike 版实现：
    //   - SetFile(ChangedFile file)：真实设置文件名 + 状态 emoji + 复选框状态
    //   - IsStaged 属性：读写 StagedCheckBox.IsChecked
    //   - IsStagedChanged 事件：复选框状态变化时触发
    //   - GetStatusEmoji(ChangeType)：ChangeType → emoji 映射
    //   - Clear()：清空显示
    //
    // 装入路径（WPF）：CommitUserControl.xaml Col 0 → StageFileUserControl
    //   （CommitUserControl.axaml 当前用 Border 占位，本控件独立可被 DI 解析后注入）
    public partial class StageFileUserControl : UserControl
    {
        // ===== 公共事件（对照 task spec）=====
        // 对照 WPF: public event EventHandler Stage / Unstage（双列表版的事件）
        // spike 版统一为 IsStagedChanged（单行复选框状态变化）
        public event EventHandler<RoutedEventArgs> IsStagedChanged;

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private ChangedFile _file; // 对照 WPF 双列表中的 ChangedFile 项
        private bool _suppressIsStagedChanged; // 防止 SetFile 时程序化设置 IsStaged 触发事件

        // ===== 公共属性（对照 task spec）=====

        // IsStaged 属性 — 复选框状态
        // 对照 WPF: ChangedFile.Staged（只读）+ 双列表版通过 stage/unstage 操作改变
        // spike 版：可读写，直接映射 StagedCheckBox.IsChecked
        public bool IsStaged
        {
            get
            {
                if (StagedCheckBox != null)
                {
                    return StagedCheckBox.IsChecked ?? false;
                }
                return _file != null && _file.Staged;
            }
            set
            {
                if (StagedCheckBox != null)
                {
                    _suppressIsStagedChanged = true;
                    StagedCheckBox.IsChecked = value;
                    _suppressIsStagedChanged = false;
                }
            }
        }

        // 当前显示的文件（对照 WPF 双列表中的选中项）
        public ChangedFile File => _file;

        // ===== 构造函数（对照 WPF 无参构造，spike 注入 IServiceProvider）=====
        public StageFileUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== SetFile(ChangedFile)（对照 task spec，替代 WPF SetData 双列表绑定）=====
        // 对照 WPF: public void SetData(ChangedFile[] unstagedFiles, ChangedFile[] stagedFiles, ...)
        //   WPF 版批量设置两个 FileListUserControl 的数据源。
        //   spike 版单文件：设置文件名 + 状态 emoji + 复选框状态。
        public void SetFile(ChangedFile file)
        {
            _file = file;

            if (file == null)
            {
                Clear();
                return;
            }

            // 设置文件名（对照 WPF FileListUserControl 内的文件路径显示）
            if (FileNameTextBlock != null)
            {
                // 对照 WPF ChangedFile.Path（完整路径）
                FileNameTextBlock.Text = file.Path ?? string.Empty;
            }

            // 设置状态图标 emoji（对照 WPF FileListUserControl 内的 ChangeTypeIcon PNG）
            if (StatusIconTextBlock != null)
            {
                StatusIconTextBlock.Text = GetStatusEmoji(file.ChangeType);
            }

            // 设置复选框状态（对照 WPF ChangedFile.Staged）
            if (StagedCheckBox != null)
            {
                _suppressIsStagedChanged = true;
                StagedCheckBox.IsChecked = file.Staged;
                _suppressIsStagedChanged = false;
            }
        }

        // ===== Clear()（spike 新增，清空显示）=====
        public void Clear()
        {
            _file = null;
            if (FileNameTextBlock != null)
            {
                FileNameTextBlock.Text = "(no file)";
            }
            if (StatusIconTextBlock != null)
            {
                StatusIconTextBlock.Text = string.Empty;
            }
            if (StagedCheckBox != null)
            {
                _suppressIsStagedChanged = true;
                StagedCheckBox.IsChecked = false;
                _suppressIsStagedChanged = false;
            }
        }

        // ===== StagedCheckBox 事件（对照 task spec IsStagedChanged 事件）=====
        // 对照 WPF: CheckBox Checked/Unchecked → IsCheckedChanged（Avalonia API）
        //   WPF 双列表版用 StageButton_Click / UnstageButton_Click 触发 Stage/Unstage 事件
        //   spike 版用复选框直接触发 IsStagedChanged 事件
        private void StagedCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressIsStagedChanged) return;
            IsStagedChanged?.Invoke(this, e);
        }

        // ===== ChangeType → emoji 映射（对照 task spec: M=📝 / A=✨ / D=🗑 / R=🔀）=====
        // 对照 WPF FileListUserControl 内的 ChangeTypeIcon PNG 资源映射
        private static string GetStatusEmoji(ChangeType changeType)
        {
            switch (changeType)
            {
                case ChangeType.Modified:    return "📝"; // M
                case ChangeType.Added:       return "✨"; // A
                case ChangeType.Deleted:     return "🗑"; // D
                case ChangeType.Renamed:     return "🔀"; // R
                case ChangeType.Copied:      return "📋"; // C
                case ChangeType.TypeChanged: return "🔄"; // T
                case ChangeType.Unmerged:    return "⚠";  // U (冲突)
                case ChangeType.Untracked:   return "❓"; // 未跟踪
                case ChangeType.Ignored:     return "🚫"; // 已忽略
                case ChangeType.Unknown:     return "❔"; // 未知
                default:                     return "❔";
            }
        }
    }
}
