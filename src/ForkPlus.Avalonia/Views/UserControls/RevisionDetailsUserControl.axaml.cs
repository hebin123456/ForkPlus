using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.6：Avalonia 版 RevisionDetailsUserControl 骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionDetailsUserControl.xaml.cs（595 行）：
    //   - 公共方法：Initialize(repo, mode) / ShowRevisionDetails(target, fileToSelect) /
    //     ShowInFileTreeTab(filePath) / HighlightSearchMatches(query) / ApplyLocalization
    //   - 公共事件：RevisionDetailsUpdated
    //   - 异步 Job：_loadFullRevisionDetailsJob（git 命令取 FullRevisionDetails）
    //   - Tab 切换动画（DoubleAnimation + TranslateTransform，spike 不迁移）
    //   - RevisionDetailsUserControlMode 枚举：MainWindow / DetachedWindow / AiReview / InteractiveRebase
    //
    // 装入路径（WPF）：
    //   RepositoryContentUserControl.xaml Row 2 → RevisionDetailsUserControl
    //   由 RevisionListViewUserControl.SelectionChanged 触发刷新链
    //
    // 本 spike 版暂不迁移：
    //   - ContentContainer 自定义控件（用 Grid 替代）
    //   - RevisionsHeaderUserControl（自定义，用 ContentControl 占位）
    //   - RevisionSummaryUserControl / RevisionChangesUserControl / RevisionFileTreeUserControl
    //     完整实现（3 个都是独立 spike，用 ContentControl 占位）
    //   - Tab 指示器动画
    //   - ShowRevisionInSeparateWindowButton 命令绑定
    //   - 异步 Job（_loadFullRevisionDetailsJob）
    //   - ApplyLocalization
    //
    // 本 spike 版验证：
    //   - Grid 3 行布局正确显示
    //   - 3 个 RadioButton (Commit/Changes/FileTree) 可切换
    //   - 3 个子 UserControl 占位容器可见性随 RadioButton 切换
    public partial class RevisionDetailsUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler<EventArgs> RevisionDetailsUpdated;

        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型待 Phase 3.6 后续子阶段补
        public object RepositoryUserControl { get; private set; }
        public string Mode { get; private set; } = "MainWindow";

        public RevisionDetailsUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF 5 个公共方法签名，body stub）=====

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl, RevisionDetailsUserControlMode mode)
        //   替代构造器 DI，由 RepositoryContentUserControl 在装入时调用
        public void Initialize(object repositoryUserControl, string mode)
        {
            Console.WriteLine($"[RevisionDetails] Initialize (spike placeholder): mode={mode}");
            RepositoryUserControl = repositoryUserControl;
            Mode = mode ?? "MainWindow";
        }

        // 对照 WPF: public void ShowRevisionDetails(RevisionDiffTarget target, string fileToSelect = null)
        //   主入口：根据 RevisionListView 选中的 commit，触发 _loadFullRevisionDetailsJob
        public void ShowRevisionDetails(object target, string fileToSelect = null)
        {
            Console.WriteLine($"[RevisionDetails] ShowRevisionDetails (spike placeholder): target={target}, fileToSelect={fileToSelect}");
        }

        // 对照 WPF: public void ShowInFileTreeTab(string filePath)
        //   切换到 FileTree tab + 展开到指定文件
        public void ShowInFileTreeTab(string filePath)
        {
            Console.WriteLine($"[RevisionDetails] ShowInFileTreeTab (spike placeholder): {filePath}");
            SwitchToFileTreeTab();
        }

        // 对照 WPF: public void HighlightSearchMatches(RevisionSearchQuery query)
        //   委托给 RevisionSummaryUserControl.HighlightSearchMatches
        public void HighlightSearchMatches(object query)
        {
            Console.WriteLine($"[RevisionDetails] HighlightSearchMatches (spike placeholder): {query}");
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[RevisionDetails] ApplyLocalization (spike placeholder)");
        }

        // ===== Tab 切换（对照 WPF 3 个 RadioButton 事件）=====

        private void CommitTabRadioButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[RevisionDetails] Commit tab");
            SwitchToCommitTab();
        }

        private void ChangesTabRadioButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[RevisionDetails] Changes tab");
            SwitchToChangesTab();
        }

        private void FileTreeTabRadioButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[RevisionDetails] FileTree tab");
            SwitchToFileTreeTab();
        }

        // 对照 WPF: ShowRevisionInSeparateWindowButton_Click
        private void ShowRevisionInSeparateWindowButton_Click(object sender, EventArgs e)
        {
            Console.WriteLine("[RevisionDetails] ShowRevisionInSeparateWindow (spike placeholder)");
        }

        // ===== Tab 切换辅助方法（对照 WPF UpdateTabVisibility）=====
        private void SwitchToCommitTab()
        {
            if (RevisionSummaryContainer != null) RevisionSummaryContainer.IsVisible = true;
            if (RevisionChangesContainer != null) RevisionChangesContainer.IsVisible = false;
            if (RevisionFileTreeContainer != null) RevisionFileTreeContainer.IsVisible = false;
        }

        private void SwitchToChangesTab()
        {
            if (RevisionSummaryContainer != null) RevisionSummaryContainer.IsVisible = false;
            if (RevisionChangesContainer != null) RevisionChangesContainer.IsVisible = true;
            if (RevisionFileTreeContainer != null) RevisionFileTreeContainer.IsVisible = false;
        }

        private void SwitchToFileTreeTab()
        {
            if (RevisionSummaryContainer != null) RevisionSummaryContainer.IsVisible = false;
            if (RevisionChangesContainer != null) RevisionChangesContainer.IsVisible = false;
            if (RevisionFileTreeContainer != null) RevisionFileTreeContainer.IsVisible = true;
        }
    }
}
