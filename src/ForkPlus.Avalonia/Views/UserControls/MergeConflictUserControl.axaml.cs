using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 MergeConflictUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/MergeConflictUserControl.xaml.cs（491 行）：
    //   - SetConflict(RepositoryUserControl, DiffContent, RepositoryState, ChangedFile, bool resolved)
    //   - ConflictVersionsContainer + ConflictResolvedContainer（两态切换）
    //   - LocalCheckBox + RemoteCheckBox（选择解决版本）
    //   - ResolveButton / ResolveInExternalMergerButton / AiResolveButton
    //   - UpdateResolveButton：按勾选状态切换按钮文本/启用状态
    //   - FileDiffControl（内嵌 diff 显示）
    //   - DstRevisionsListBox / SrcRevisionsListBox（冲突双方的 commit 列表）
    //   - AiResolveButton_Click：AI 解决冲突（OpenAiService 流式请求）
    //   - ResolveButton_Click：按 Local/Remote/Both 选择解决方式
    //   - ResolveInExternalMergerButton_Click：调外部 merge 工具
    //   - PreferencesLocalization → ServiceLocator.Localization
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF RepositoryUserControl → object（spike 占位）
    //   - WPF Image FileIcon → emoji TextBlock（📄 文件图标）
    //   - WPF DiffContent/RepositoryState → 不迁移（spike 用 ChangedFile）
    //   - WPF AiResolveButton（OpenAiService）→ 不迁移 AI 逻辑（spike 占位按钮）
    //   - WPF FileDiffControl 内嵌 → 不迁移（spike 用 ListBox 列表）
    //   - WPF DstRevisionsListBox/SrcRevisionsListBox → 不迁移（spike 不显示）
    //   - WPF Dispatcher.Invoke → Dispatcher.UIThread.Post（Avalonia API 规则）
    //   - WPF Image.Show()/Hide()/Collapse() → IsVisible=true/false/false
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / SetConflicts(ChangedFile[]) /
    //     ResolveConflict(ChangedFile) / FileSelected 事件
    //   - WPF Visibility.Collapsed/Visible → IsVisible=false/true
    //
    // spike 简化：
    //   - Initialize(object) 方法（task spec 关键 API）
    //   - SetConflicts(ChangedFile[]) 方法（task spec 关键 API，替代 WPF SetConflict 单文件）
    //   - ResolveConflict(ChangedFile) 方法（task spec 关键 API）
    //   - FileSelected 事件（task spec 关键 API）
    //   - 冲突文件列表用 ListBox 显示
    //   - 解决选项用 CheckBox + Button（Local/Remote/Both）
    public partial class MergeConflictUserControl : UserControl
    {
        // ===== 公共事件（task spec 关键 API）=====
        // 文件选中事件（对照 task spec: FileSelected）
        public event EventHandler<ChangedFile> FileSelected;

        // 冲突解决事件（spike 新增，对照 WPF ResolveButton_Click 结果）
        public event EventHandler<ChangedFile> ConflictResolved;

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // 当前冲突文件列表（task spec: SetConflicts 数据入口）
        private ChangedFile[] _conflicts;
        // 当前选中的冲突文件
        private ChangedFile _selectedFile;

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public MergeConflictUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== Initialize(object)（task spec 关键 API）=====
        // 对照 WPF: SetConflict 内注入 _repositoryUserControl
        // spike 版：Initialize 方法注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetConflicts(ChangedFile[])（task spec 关键 API）=====
        // 对照 WPF: SetConflict(RepositoryUserControl, DiffContent, RepositoryState, ChangedFile, bool)
        //   WPF 版单文件设置，spike 版批量设置冲突文件列表
        public void SetConflicts(ChangedFile[] conflicts)
        {
            _conflicts = conflicts ?? Array.Empty<ChangedFile>();

            // 更新冲突计数显示
            if (ConflictCountTextBlock != null)
            {
                ConflictCountTextBlock.Text = _conflicts.Length > 0
                    ? Translate($"{_conflicts.Length} conflict(s)")
                    : "";
            }

            // 更新文件列表
            if (ConflictFilesListBox != null)
            {
                ConflictFilesListBox.ItemsSource = _conflicts;
            }

            // 默认选中第一个文件
            if (_conflicts.Length > 0 && ConflictFilesListBox != null)
            {
                ConflictFilesListBox.SelectedIndex = 0;
            }
        }

        // ===== ResolveConflict(ChangedFile)（task spec 关键 API）=====
        // 对照 WPF: ResolveButton_Click 内调 ResolveConflictGitCommand
        // spike 版：触发 ConflictResolved 事件（调用方处理实际 git 命令）
        public void ResolveConflict(ChangedFile file)
        {
            if (file == null) return;

            // 对照 WPF: UpdateResolveButton 按 Local/Remote 选择解决方式
            bool useLocal = LocalCheckBox?.IsChecked ?? false;
            bool useRemote = RemoteCheckBox?.IsChecked ?? false;

            // spike 版：触发事件，由调用方处理实际 git ResolveConflictGitCommand
            ConflictResolved?.Invoke(this, file);
        }

        // ===== Clear()（spike 新增，清空显示）=====
        public void Clear()
        {
            _conflicts = null;
            _selectedFile = null;
            if (ConflictFilesListBox != null)
            {
                ConflictFilesListBox.ItemsSource = null;
            }
            if (FileNameTextBlock != null)
            {
                FileNameTextBlock.Text = "(no conflict file)";
            }
            if (ConflictCountTextBlock != null)
            {
                ConflictCountTextBlock.Text = "";
            }
            if (LocalCheckBox != null) LocalCheckBox.IsChecked = false;
            if (RemoteCheckBox != null) RemoteCheckBox.IsChecked = false;
            if (AiResolveButton != null) AiResolveButton.IsVisible = false;
        }

        // ===== ConflictFilesListBox_SelectionChanged（spike 新增）=====
        // 对照 task spec: FileSelected 事件
        private void ConflictFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                _selectedFile = listBox.SelectedItem as ChangedFile;
                if (_selectedFile != null)
                {
                    // 更新文件名显示
                    if (FileNameTextBlock != null)
                    {
                        FileNameTextBlock.Text = _selectedFile.Path ?? "(unknown file)";
                    }

                    // 触发 FileSelected 事件（task spec 关键 API）
                    FileSelected?.Invoke(this, _selectedFile);

                    // 默认勾选 Local + Remote（对照 WPF: IsMergeAllowed 时双勾选）
                    if (LocalCheckBox != null) LocalCheckBox.IsChecked = true;
                    if (RemoteCheckBox != null) RemoteCheckBox.IsChecked = true;
                    UpdateResolveButton();
                }
            }
        }

        // ===== MergeCheckBox_Changed（对照 WPF）=====
        // WPF: CheckBox Checked/Unchecked → IsCheckedChanged（Avalonia API）
        private void MergeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateResolveButton();
        }

        // ===== ResolveButton_Click（对照 WPF）=====
        // WPF: 按 Local/Remote/Both 选择解决方式 + 调 ResolveConflictGitCommand
        // spike: 调用 ResolveConflict 方法（触发事件）
        private void ResolveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile != null)
            {
                ResolveConflict(_selectedFile);
            }
        }

        // ===== AiResolveButton_Click（对照 WPF，spike 占位）=====
        // WPF: OpenAiService 流式请求解决冲突
        // spike: 不迁移 AI 逻辑，按钮默认隐藏
        private void AiResolveButton_Click(object sender, RoutedEventArgs e)
        {
            // spike 版不实现 AI 解决逻辑
            // 对照 WPF: AiResolveButton_Click（148-327 行，OpenAiService 流式请求）
        }

        // ===== UpdateResolveButton（对照 WPF，spike 简化）=====
        // WPF: 按 Local/Remote/Both 切换按钮文本 + 启用状态 + 外部 merge 工具按钮
        // spike: 简化文本切换
        private void UpdateResolveButton()
        {
            if (ResolveButton == null) return;
            bool useLocal = LocalCheckBox?.IsChecked ?? false;
            bool useRemote = RemoteCheckBox?.IsChecked ?? false;

            if (useLocal && useRemote)
            {
                // 对照 WPF: ResolveButton.Content = "Merge"
                ResolveButton.Content = Translate("Merge");
                ResolveButton.IsEnabled = true;
            }
            else if (useLocal)
            {
                // 对照 WPF: ResolveButton.Content = "Choose {Local}"
                ResolveButton.Content = Translate("Choose Local");
                ResolveButton.IsEnabled = true;
            }
            else if (useRemote)
            {
                // 对照 WPF: ResolveButton.Content = "Choose {Remote}"
                ResolveButton.Content = Translate("Choose Remote");
                ResolveButton.IsEnabled = true;
            }
            else
            {
                // 对照 WPF: ResolveButton.Content = "Merge" + Disable
                ResolveButton.Content = Translate("Merge");
                ResolveButton.IsEnabled = false;
            }
        }

        // ===== Translate(string)（对照 WPF: PreferencesLocalization.Current）=====
        // WPF: PreferencesLocalization.Current(text) / FormatCurrent
        // spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
