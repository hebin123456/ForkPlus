using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionDetailsUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionDetailsUserControl.xaml.cs（595 行）：
    //   - 公共方法：Initialize(repo, mode) / ShowRevisionDetails(target, fileToSelect) /
    //     ShowInFileTreeTab(filePath) / HighlightSearchMatches(query) / ApplyLocalization
    //   - 公共事件：RevisionDetailsUpdated
    //   - 异步 Job：_loadFullRevisionDetailsJob（git 命令取 FullRevisionDetails）
    //   - Tab 切换动画（DoubleAnimation + TranslateTransform）
    //   - RevisionDetailsUserControlMode 枚举：MainWindow / DetachedWindow / AiReview / InteractiveRebase
    //
    // 装入路径（WPF）：
    //   RepositoryContentUserControl.xaml Row 3 → RevisionDetailsUserControl
    //   由 RevisionListViewUserControl.SelectionChanged 触发刷新链
    //
    // Avalonia 版差异：
    //   - WPF Visibility.Collapsed/Visible → Avalonia IsVisible=false/true
    //   - WPF Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   - WPF JobQueue → Task.Run + Dispatcher.UIThread.Post
    //   - WPF AvalonEdit.TextEditor → AvaloniaEdit.TextEditor
    //   - WPF Tab 切换动画 → spike 不迁移
    //   - WPF ContentContainer → Grid 替代
    //
    // spike 简化：
    //   - 用 Grid 显示作者/日期/message（Commit tab）
    //   - 用 AvaloniaEdit.TextEditor 显示 diff（Changes tab）
    //   - FileTree tab 用 ContentControl 占位
    //   - SetRevision(revision) / Refresh() / SetMode(mode) 公共方法
    //   - Tab 指示器动画 / 异步 Job / ApplyLocalization 暂不迁移
    public partial class RevisionDetailsUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler<EventArgs> RevisionDetailsUpdated;

        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型待后续阶段补
        public object RepositoryUserControl { get; private set; }
        public string Mode { get; private set; } = "MainWindow";

        // 当前 revision（spike 用 object 占位，真实类型 RevisionViewModel）
        public object CurrentRevision { get; private set; }

        public RevisionDetailsUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl, RevisionDetailsUserControlMode mode)
        //   替代构造器 DI，由 RepositoryContentUserControl 在装入时调用
        public void Initialize(object repositoryUserControl, string mode)
        {
            Console.WriteLine($"[RevisionDetails] Initialize: mode={mode}");
            RepositoryUserControl = repositoryUserControl;
            Mode = mode ?? "MainWindow";
            RefreshLayout();
        }

        // spike 新增：SetRevision(RevisionViewModel revision)
        //   对照 WPF: ShowRevisionDetails(RevisionDiffTarget target, string fileToSelect)
        //   spike 版：用 object 占位参数，从 revision 反射读取属性更新 UI
        public void SetRevision(object revision)
        {
            CurrentRevision = revision;
            Console.WriteLine($"[RevisionDetails] SetRevision: {revision}");

            // spike 版：从 revision 反射读取属性更新 UI
            if (revision != null)
            {
                var type = revision.GetType();
                var authorProp = type.GetProperty("Author")?.GetValue(revision);
                var dateProp = type.GetProperty("Date")?.GetValue(revision);
                var shaProp = type.GetProperty("Sha")?.GetValue(revision);
                var subjectProp = type.GetProperty("Subject")?.GetValue(revision);
                var messageProp = type.GetProperty("Body")?.GetValue(revision) ??
                                   type.GetProperty("Message")?.GetValue(revision);

                if (AuthorTextBlock != null)
                    AuthorTextBlock.Text = authorProp?.ToString() ?? "(unknown)";
                if (DateTextBlock != null)
                    DateTextBlock.Text = dateProp?.ToString() ?? "(unknown)";
                if (ShaTextBlock != null)
                    ShaTextBlock.Text = shaProp?.ToString() ?? "(no sha)";
                if (SubjectTextBlock != null)
                    SubjectTextBlock.Text = subjectProp?.ToString() ?? "(no subject)";
                if (MessageTextBlock != null)
                    MessageTextBlock.Text = messageProp?.ToString() ?? "";
            }

            RevisionDetailsUpdated?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: public void ShowRevisionDetails(RevisionDiffTarget target, string fileToSelect = null)
        //   spike 版：委托到 SetRevision（target 为 object 占位，fileToSelect 暂不处理）
        public void ShowRevisionDetails(object target, string fileToSelect = null)
        {
            SetRevision(target);
        }

        // 对照 WPF: public void Refresh()
        //   刷新当前 revision 的详情
        public void Refresh()
        {
            Console.WriteLine("[RevisionDetails] Refresh");
            if (CurrentRevision != null)
            {
                SetRevision(CurrentRevision);
            }
        }

        // spike 新增：SetMode(RevisionDetailsUserControlMode mode)
        //   对照 WPF: RefreshLayout()
        //   spike 版：根据 mode 切换 FileTree tab 可见性等
        public void SetMode(string mode)
        {
            Mode = mode ?? "MainWindow";
            Console.WriteLine($"[RevisionDetails] SetMode: {Mode}");
            RefreshLayout();
        }

        // 对照 WPF: public void ShowInFileTreeTab(string filePath)
        //   切换到 FileTree tab + 展开到指定文件
        public void ShowInFileTreeTab(string filePath)
        {
            Console.WriteLine($"[RevisionDetails] ShowInFileTreeTab: {filePath}");
            SwitchToFileTreeTab();
        }

        // 对照 WPF: public void HighlightSearchMatches(RevisionSearchQuery query)
        public void HighlightSearchMatches(object query)
        {
            Console.WriteLine($"[RevisionDetails] HighlightSearchMatches: {query}");
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[RevisionDetails] ApplyLocalization (spike placeholder)");
        }

        // ===== Tab 切换（对照 WPF 3 个 RadioButton 事件）=====

        private void CommitTabRadioButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RevisionDetails] Commit tab");
            SwitchToCommitTab();
        }

        private void ChangesTabRadioButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RevisionDetails] Changes tab");
            SwitchToChangesTab();
        }

        private void FileTreeTabRadioButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RevisionDetails] FileTree tab");
            SwitchToFileTreeTab();
        }

        // 对照 WPF: ShowRevisionInSeparateWindowButton_Click
        private void ShowRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
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

        // 对照 WPF: RefreshLayout()
        //   根据 Mode 调整布局（spike 版：InteractiveRebase 时隐藏 FileTree tab）
        private void RefreshLayout()
        {
            if (Mode == "InteractiveRebase")
            {
                if (FileTreeTabRadioButton != null) FileTreeTabRadioButton.IsVisible = false;
            }
            else
            {
                if (FileTreeTabRadioButton != null) FileTreeTabRadioButton.IsVisible = true;
            }
        }
    }
}
