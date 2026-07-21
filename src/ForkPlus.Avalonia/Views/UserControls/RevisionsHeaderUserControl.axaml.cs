using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionsHeaderUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionsHeaderUserControl.xaml.cs（123 行）：
    //   - SetSubmoduleRevisions(SubmoduleDiffContent)：显示 src/dst revision 详情
    //   - SetRevisions(Revision, BugtrackerLinkDefinition[], RevisionDetails, bool)：显示 revision 对比
    //   - UpdateControls：更新 AvatarImage/AuthorTextBlock/ShaTextBlock/SubjectTextBlock 等
    //   - GetCustomLabelString：Sha → 缩写字符串
    //   - SwapRevisionsButton：交换 src/dst 顺序
    //   - 依赖 AvatarImage / Theme.Diff.AddedBrush/RemovedBrush（WPF-only）
    //
    // Avalonia 版差异（spike 简化）：
    //   - task spec 作用：commit 列表表头（列标题 + 排序），与 WPF 源（revision 详情对比）不同
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / SetSortColumn(string) / SortChanged
    //   - WPF AvatarImage → emoji TextBlock（👤 作者）
    //   - WPF Theme.Diff.AddedBrush/RemovedBrush → 硬编码颜色
    //   - WPF UpdateControls 复杂逻辑 → spike 简化为基本按钮（task spec 简化策略）
    //   - spike 简化：复杂控件简化为基本按钮（task spec 简化策略）
    //
    // spike 简化：
    //   - Initialize(object repositoryUserControl) 方法（task spec 关键 API）
    //   - SetSortColumn(string) 方法（task spec 关键 API）
    //   - SortChanged 事件（task spec 关键 API）
    //   - 列标题用 Button（点击切换排序方向）
    //   - 当前排序列显示 ▲/▼ 箭头
    public partial class RevisionsHeaderUserControl : UserControl
    {
        // ===== 公共事件（task spec 关键 API）=====
        // 排序变更事件（对照 task spec: SortChanged）
        // EventArgs 携带排序列名 + 排序方向
        public event EventHandler<SortChangedEventArgs> SortChanged;

        // ===== 排序参数 EventArgs（spike 新增）=====
        public class SortChangedEventArgs : EventArgs
        {
            public string ColumnName { get; set; }
            public bool Ascending { get; set; }
        }

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // 当前排序列（对照 task spec: SetSortColumn）
        private string _sortColumn = "Date";
        // 当前排序方向
        private bool _ascending = false;

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public RevisionsHeaderUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            UpdateSortIndicators();
        }

        // ===== Initialize(object)（task spec 关键 API）=====
        // 对照 WPF: 无独立 Initialize（依赖通过 DataContext 注入）
        // spike 版：Initialize 方法注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetSortColumn(string)（task spec 关键 API）=====
        // 设置当前排序列（不触发 SortChanged 事件，仅更新 UI 指示器）
        public void SetSortColumn(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return;
            _sortColumn = columnName;
            UpdateSortIndicators();
        }

        // ===== SetSortDirection(bool)（spike 新增）=====
        // 设置排序方向（不触发 SortChanged 事件，仅更新 UI 指示器）
        public void SetSortDirection(bool ascending)
        {
            _ascending = ascending;
            UpdateSortIndicators();
        }

        // ===== ColumnButton_Click（spike 新增，列标题点击排序）=====
        // 对照 task spec: 排序功能
        // 点击列标题：同列切换方向，不同列切换到该列（默认降序）
        private void ColumnButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string columnName)
            {
                if (columnName == _sortColumn)
                {
                    // 同列：切换方向
                    _ascending = !_ascending;
                }
                else
                {
                    // 不同列：切换到该列，默认降序
                    _sortColumn = columnName;
                    _ascending = false;
                }

                UpdateSortIndicators();

                // 触发 SortChanged 事件（task spec 关键 API）
                SortChanged?.Invoke(this, new SortChangedEventArgs
                {
                    ColumnName = _sortColumn,
                    Ascending = _ascending
                });
            }
        }

        // ===== UpdateSortIndicators（spike 新增，更新排序箭头）=====
        // 当前排序列显示 ▲（升序）/ ▼（降序），其他列不显示
        private void UpdateSortIndicators()
        {
            string arrow = _ascending ? "▲" : "▼";
            string none = "";

            if (ShaColumnButton != null) ShaColumnButton.Content = "SHA" + (_sortColumn == "Sha" ? " " + arrow : none);
            if (AuthorColumnButton != null) AuthorColumnButton.Content = "Author" + (_sortColumn == "Author" ? " " + arrow : none);
            if (SubjectColumnButton != null) SubjectColumnButton.Content = "Subject" + (_sortColumn == "Subject" ? " " + arrow : none);
            if (DateColumnButton != null) DateColumnButton.Content = "Date" + (_sortColumn == "Date" ? " " + arrow : none);
        }
    }
}
