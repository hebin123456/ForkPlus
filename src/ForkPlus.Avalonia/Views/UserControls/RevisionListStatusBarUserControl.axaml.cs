using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.5：Avalonia 版 RevisionListStatusBarUserControl（完整迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionListStatusBarUserControl.xaml.cs（110 行）：
    //   - 公共属性 RepositoryUserControl（父控件注入，用于读 RepositoryData + Commands）
    //   - 私有属性 StatusBarTextBlockMaxWidth = Container.ActualWidth - 145.0（文本截断宽度）
    //   - 构造函数：订阅 NotificationCenter.Current.RepositoryDataUpdated 事件 +
    //     SizeChanged → InvalidateStatusBarTextBlockMeasurement
    //   - RepositoryDataUpdated(sender, args)：
    //       if (args.RepositoryUserControl != RepositoryUserControl) return;
    //       if (repositoryData.Reflog) → 显示 "Reflog mode enabled" + Exit 按钮
    //       else if (FilterReferences.Length != 0) → 显示 "Filtered by:" + 引用列表 + Clear filter 按钮
    //       else → Collapse（隐藏）
    //   - InvalidateStatusBarTextBlockMeasurement()：ReferencesTextBlock.MaxWidth 截断
    //   - ToFriendlyName(string)：剥离 refs/heads/ / refs/remotes/ / refs/tags/ 前缀
    //   - StatusBarButton_Click：ToggleShowReflogInRevisionList / UpdateReferenceFilter.ClearFilter
    //   - Translate(text)：PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化）：
    //   - NotificationCenter 在 WPF 工程（src/ForkPlus/NotificationCenter.cs），Avalonia 工程不可访问
    //     → 不订阅 RepositoryDataUpdated 事件，改为 task spec 指定的 SetStatus/ShowLoading/SetCount API
    //   - PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   - Visibility.Collapsed/Visible → IsVisible=false/true（this.Collapse()/Show() → IsVisible）
    //   - WPF TextButtonStyle → 默认 Button 样式
    //   - SizeChanged → SizeChanged 事件（Avalonia API 一致）
    //   - 新增 LoadingSpinner（ProgressBar IsIndeterminate）+ CountTextBlock（计数显示）
    //
    // 本 spike 版实现：
    //   - SetStatus(string text)：显示状态文本（对照 WPF HeaderTextBlock.Text 设置）
    //   - ShowLoading(bool)：显示/隐藏 loading 指示器
    //   - SetCount(int total, int filtered)：显示总数/过滤数
    //   - SetFilteredBy(string[] references)：显示 "Filtered by:" + 引用列表 + Clear filter 按钮
    //     （对照 WPF RepositoryDataUpdated 中 FilterReferences 分支）
    //   - SetReflogMode()：显示 "Reflog mode enabled" + Exit 按钮
    //     （对照 WPF RepositoryDataUpdated 中 Reflog 分支）
    //   - Hide()：隐藏状态栏（对照 WPF this.Collapse()）
    //   - ToFriendlyName(string)：完整迁移 WPF 逻辑
    //   - StatusBarButton_Click：调用注入的回调（对照 WPF Commands.ToggleShowReflogInRevisionList /
    //     Commands.UpdateReferenceFilter.ClearFilter，spike 用 Action 回调替代）
    //   - InvalidateStatusBarTextBlockMeasurement()：完整迁移 WPF 文本截断逻辑
    //
    // 装入路径（WPF）：RevisionListViewUserControl 底部
    public partial class RevisionListStatusBarUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        // 对照 WPF: public RepositoryUserControl RepositoryUserControl { get; set; }
        // spike 版用 object 占位（Avalonia RepositoryUserControl 是 stub）
        public object RepositoryUserControl { get; set; }

        // spike 新增：StatusBarButton 点击回调（替代 WPF Commands 调用）
        // 调用方注入：reflog 模式时切换 reflog，过滤模式时清除过滤
        public Action StatusBarButtonClick { get; set; }

        // spike 新增：当前模式（用于 StatusBarButton_Click 分发）
        private enum StatusBarMode { None, Reflog, Filtered }
        private StatusBarMode _mode = StatusBarMode.None;

        // 对照 WPF: private double StatusBarTextBlockMaxWidth => Container.ActualWidth - 145.0
        private double StatusBarTextBlockMaxWidth
        {
            get
            {
                if (Container == null) return 200.0;
                return Container.Bounds.Width - 145.0;
            }
        }

        public RevisionListStatusBarUserControl(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            // 对照 WPF: base.SizeChanged += delegate { InvalidateStatusBarTextBlockMeasurement(); }
            SizeChanged += (s, e) => InvalidateStatusBarTextBlockMeasurement();
        }

        // ===== task spec API =====

        // SetStatus(string text) — 显示状态文本
        // 对照 WPF HeaderTextBlock.Text 设置（Reflog mode enabled / Filtered by:）
        public void SetStatus(string text)
        {
            if (HeaderTextBlock != null)
            {
                HeaderTextBlock.Text = Translate(text);
            }
            if (Container != null)
            {
                Container.IsVisible = true;
            }
        }

        // ShowLoading(bool) — 显示/隐藏 loading 指示器
        // spike 新增（WPF 无独立 loading 指示器，本控件在 WPF 是纯文本状态栏）
        public void ShowLoading(bool show)
        {
            if (LoadingSpinner != null)
            {
                LoadingSpinner.IsVisible = show;
            }
            if (Container != null)
            {
                Container.IsVisible = true;
            }
        }

        // SetCount(int total, int filtered) — 显示总数/过滤数
        // spike 新增（WPF 无计数显示，RevisionListView 上方有自己的计数）
        public void SetCount(int total, int filtered)
        {
            if (CountTextBlock == null) return;
            if (filtered >= 0 && filtered < total)
            {
                CountTextBlock.Text = $"{filtered} / {total}";
                CountTextBlock.IsVisible = true;
            }
            else
            {
                CountTextBlock.Text = $"{total}";
                CountTextBlock.IsVisible = total > 0;
            }
            if (Container != null)
            {
                Container.IsVisible = true;
            }
        }

        // ===== WPF 逻辑完整迁移方法 =====

        // 对照 WPF: RepositoryDataUpdated 中 Reflog 分支
        //   HeaderTextBlock.Text = "Reflog mode enabled"
        //   StatusBarButton.Content = "Exit"
        //   ReferencesTextBlock.Text = ""
        //   this.Show()
        public void SetReflogMode()
        {
            _mode = StatusBarMode.Reflog;
            if (HeaderTextBlock != null) HeaderTextBlock.Text = Translate("Reflog mode enabled");
            if (StatusBarButton != null)
            {
                StatusBarButton.Content = Translate("Exit");
                StatusBarButton.IsVisible = true;
            }
            if (ReferencesTextBlock != null)
            {
                ReferencesTextBlock.Text = string.Empty;
                ReferencesTextBlock.IsVisible = false;
            }
            InvalidateStatusBarTextBlockMeasurement();
            if (Container != null) Container.IsVisible = true;
        }

        // 对照 WPF: RepositoryDataUpdated 中 FilterReferences 分支
        //   HeaderTextBlock.Text = "Filtered by:"
        //   ReferencesTextBlock.Text = string.Join(", ", FilterReferences.Select(ToFriendlyName))
        //   StatusBarButton.Content = "Clear filter"
        //   this.Show()
        public void SetFilteredBy(string[] references)
        {
            if (references == null || references.Length == 0)
            {
                Hide();
                return;
            }
            _mode = StatusBarMode.Filtered;
            if (HeaderTextBlock != null) HeaderTextBlock.Text = Translate("Filtered by:");
            if (ReferencesTextBlock != null)
            {
                ReferencesTextBlock.Text = string.Join(", ", references.Select(ToFriendlyName));
                ReferencesTextBlock.IsVisible = true;
            }
            if (StatusBarButton != null)
            {
                StatusBarButton.Content = Translate("Clear filter");
                StatusBarButton.IsVisible = true;
            }
            InvalidateStatusBarTextBlockMeasurement();
            if (Container != null) Container.IsVisible = true;
        }

        // 对照 WPF: this.Collapse()（RepositoryDataUpdated else 分支）
        public void Hide()
        {
            _mode = StatusBarMode.None;
            if (Container != null) Container.IsVisible = false;
        }

        // 对照 WPF: private void InvalidateStatusBarTextBlockMeasurement()
        //   if (ReferencesTextBlock.ActualWidth > 0 && > StatusBarTextBlockMaxWidth)
        //     ReferencesTextBlock.MaxWidth = StatusBarTextBlockMaxWidth
        //     ReferencesTextBlock.InvalidateMeasure()
        private void InvalidateStatusBarTextBlockMeasurement()
        {
            if (ReferencesTextBlock == null) return;
            double actualWidth = ReferencesTextBlock.Bounds.Width;
            if (actualWidth > 0.0 && actualWidth > StatusBarTextBlockMaxWidth)
            {
                ReferencesTextBlock.MaxWidth = StatusBarTextBlockMaxWidth;
                ReferencesTextBlock.InvalidateMeasure();
            }
        }

        // 对照 WPF: private string ToFriendlyName(string fullReference)
        //   剥离 refs/heads/ / refs/remotes/ / refs/tags/ 前缀，尾部 / 改为 *'
        //   完整迁移，零改动
        private static string ToFriendlyName(string fullReference)
        {
            fullReference = fullReference.Replace("refs/heads/", "");
            fullReference = fullReference.Replace("refs/remotes/", "");
            fullReference = fullReference.Replace("refs/tags/", "");
            if (fullReference.EndsWith("/"))
            {
                return "'" + fullReference + "*'";
            }
            return "'" + fullReference + "'";
        }

        // 对照 WPF: private void StatusBarButton_Click(object sender, RoutedEventArgs e)
        //   WPF: repositoryData.Reflog → Commands.ToggleShowReflogInRevisionList.Execute()
        //        else → Commands.UpdateReferenceFilter.ClearFilter(repositoryUserControl)
        //   spike: 调用注入的 StatusBarButtonClick 回调
        private void StatusBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == StatusBarMode.None) return;
            StatusBarButtonClick?.Invoke();
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)
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
