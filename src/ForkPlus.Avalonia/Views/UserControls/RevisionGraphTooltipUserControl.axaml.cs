using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionGraphTooltipUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionGraphTooltipUserControl.xaml.cs（132 行）：
    //   - 构造函数 RevisionGraphTooltipUserControl(RepositoryUserControl, Sha)
    //   - Refresh()：异步加载 RevisionStorage + RevisionsDataSource.Reload
    //   - RefreshHeight()：DoubleAnimation 高度动画（QuadraticEase）
    //   - RevisionListView（自定义 RevisionListView，显示 commit 列表）
    //   - GraphCellView_ExpandToggle：折叠/展开 merge commit
    //   - ApplicationThemeChanged 订阅
    //   - FallbackMessageTextBlock + BusyIndicator（错误/加载状态）
    //   - HeightChanged 事件
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF 自定义 Popup + DoubleAnimation 高度动画 → ToolTip.SetTip（task spec 简化）
    //   - WPF RevisionsDataSource + RevisionListView → 简化 TextBlock 显示 commit 详情
    //   - WPF GraphCellView_ExpandToggle → 不迁移（spike 无 graph cell）
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post（Avalonia API 规则）
    //   - WPF Visibility.Collapsed/Visible → IsVisible=false/true
    //   - WPF BeginAnimation(DoubleAnimation) → 不迁移（spike 无高度动画）
    //   - WPF Image.Show()/Hide()/Collapse() → IsVisible=true/false/false
    //   - task spec 关键 API：SetRevision(RevisionViewModel revision)
    //
    // spike 简化：
    //   - SetRevision(RevisionViewModel revision) 方法（task spec 关键 API）
    //   - 用 ToolTip.SetTip 替代自定义 Popup（task spec 简化策略）
    //   - 简化 TextBlock 显示 commit 详情（SHA + 作者 + 日期 + 主题 + body）
    //   - BusyIndicator + FallbackMessage 保留（加载/错误状态）
    public partial class RevisionGraphTooltipUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF: HeightChanged）=====
        // spike 版保留事件签名但不触发（无高度动画）
        public event EventHandler<double> HeightChanged;

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // 当前 revision（task spec 关键 API 数据入口）
        public RevisionViewModel Revision { get; private set; }

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public RevisionGraphTooltipUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== Initialize(object)（spike 新增，注入父控件）=====
        // 对照 WPF: 构造函数注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetRevision(RevisionViewModel)（task spec 关键 API）=====
        // 对照 WPF: Refresh() 异步加载 RevisionStorage + RevisionsDataSource.Reload
        // spike 版：直接显示传入的 RevisionViewModel（不异步加载 git）
        public void SetRevision(RevisionViewModel revision)
        {
            Revision = revision;
            if (revision == null)
            {
                ShowFallback("No revision data");
                return;
            }

            // 隐藏 loading + fallback，显示 commit 详情
            HideBusy();
            HideFallback();

            if (SubjectTextBlock != null)
            {
                SubjectTextBlock.Text = revision.Subject ?? string.Empty;
                SubjectTextBlock.IsVisible = true;
            }
            if (ShaTextBlock != null)
            {
                ShaTextBlock.Text = revision.AbbreviatedSha ?? revision.Sha ?? string.Empty;
            }
            if (AuthorTextBlock != null)
            {
                AuthorTextBlock.Text = revision.Author ?? string.Empty;
            }
            if (DateTextBlock != null)
            {
                DateTextBlock.Text = revision.AuthorDate.ToString("yyyy-MM-dd HH:mm:ss");
            }
            if (BodyTextBlock != null)
            {
                if (!string.IsNullOrEmpty(revision.Body))
                {
                    BodyTextBlock.Text = revision.Body;
                    BodyTextBlock.IsVisible = true;
                }
                else
                {
                    BodyTextBlock.IsVisible = false;
                }
            }

            // spike 版不触发 HeightChanged（无高度动画）
        }

        // ===== ShowLoading()（对照 WPF: BusyIndicator.Show()）=====
        // WPF: BusyIndicator.Show() → IsVisible=true（Avalonia API）
        public void ShowLoading()
        {
            // 对照 WPF: FallbackMessageTextBlock.Collapse()
            HideFallback();
            if (BusyIndicator != null)
            {
                BusyIndicator.IsVisible = true;
            }
            // 对照 WPF: RevisionListView.Collapse()
            HideRevisionDetails();
        }

        // ===== ShowFallback(string)（对照 WPF: FallbackMessageTextBlock.Show()）=====
        // WPF: BusyIndicator.Collapse() + FallbackMessageTextBlock.Show()
        public void ShowFallback(string message)
        {
            HideBusy();
            HideRevisionDetails();
            if (FallbackMessageTextBlock != null)
            {
                FallbackMessageTextBlock.Text = message ?? string.Empty;
                FallbackMessageTextBlock.IsVisible = true;
            }
        }

        // ===== Refresh()（对照 WPF: Refresh()）=====
        // WPF: 异步调 GetRevisionStorageGitCommand + RevisionsDataSource.Reload
        // spike 版：显示 loading（不实际调 git，由 SetRevision 注入数据）
        public void Refresh()
        {
            ShowLoading();
            // spike 版不调 git 命令，等待 SetRevision 注入数据
            // 对照 WPF: Dispatcher.Async → Dispatcher.UIThread.Post（Avalonia API 规则）
            Dispatcher.UIThread.Post(() =>
            {
                if (Revision == null)
                {
                    ShowFallback("Waiting for revision data...");
                }
            });
        }

        // ===== 私有辅助方法 =====
        private void HideBusy()
        {
            if (BusyIndicator != null)
            {
                BusyIndicator.IsVisible = false;
            }
        }

        private void HideFallback()
        {
            if (FallbackMessageTextBlock != null)
            {
                FallbackMessageTextBlock.IsVisible = false;
            }
        }

        private void HideRevisionDetails()
        {
            if (SubjectTextBlock != null) SubjectTextBlock.IsVisible = false;
            if (ShaTextBlock != null) ShaTextBlock.IsVisible = false;
            if (AuthorTextBlock != null) AuthorTextBlock.IsVisible = false;
            if (DateTextBlock != null) DateTextBlock.IsVisible = false;
            if (BodyTextBlock != null) BodyTextBlock.IsVisible = false;
        }
    }
}
