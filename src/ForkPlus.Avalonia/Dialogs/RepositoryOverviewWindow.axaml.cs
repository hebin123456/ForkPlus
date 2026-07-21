using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.5a：Avalonia 版 RepositoryOverviewWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RepositoryOverviewWindow.xaml.cs（412 行）：
    //   - public partial class RepositoryOverviewWindow : CustomWindow, ITreemapDelegate
    //   - 字段：
    //     * JobQueue _jobQueue / GitModule _gitModule
    //     * ITreemapDataSource _dataSource / RepositoryOverviewData _repositoryOverviewData
    //     * 静态 Typeface / Brush / Pen / BitmapImage FolderIcon
    //     * TextDrawer _titleGlyphDrawer / _secondaryLabelGlyphDrawer
    //     * Brush _labelBrush / _secondaryLabelBrush / Pen _borderPen / _selectedBorderPen / _hoverBorderPen
    //     * double _borderRadius = 3.0 / _headerHeight = 20.0 / Size _itemPadding
    //   - 构造函数 (RepositoryUserControl, GitModule gitModule)：
    //     * Title = "{repoName} - Repository Overview"
    //     * RepositoryNameTextBlock.Text = gitModule.Path
    //     * TargetReferenceGitPointView.Value = activeBranch
    //     * Treemap.Delegate = this + Treemap.SelectionChanged += 文件路径点击链路
    //     * DateRangeButton.DateRangeChanged += RefreshData(dateRange)
    //     * JobQueue.Add 调 GetRepositoryOverviewDataGitCommand.Execute
    //   - RefreshData(CalendarDateRange)：
    //     * repositoryOverviewData.Files → ReadCommitsCountDataSourceItems 递归层级
    //     * Treemap.DataSource = dataSource
    //   - ITreemapDelegate.GetItemTitle / DrawChildInRect / CreateTooltip：
    //     * 自绘 Treemap：DrawRoundedRectangle + DrawImage(FolderIcon/FileIcon) + TextDrawer.DrawText
    //   - ApplicationThemeChanged → RefreshBrushes + InvalidateVisual
    //   - OnKeyDown: Esc → Close
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 构造函数重写：解耦 RepositoryUserControl，改为 (GitModule gitModule)
    //   2. 不实现自绘 Treemap 图（ITreemapDelegate + DrawingContext）
    //      spike 版用 ItemsControl + WrapPanel + Border 网格布局替代
    //   3. 不实现 Treemap.SelectionChanged 文件路径点击链路
    //      spike 版只显示 contributor 维度统计
    //   4. 不实现 DateRangeButton 日期范围切换（显示全仓库范围）
    //   5. 不实现 Fallback Loading 遮罩（spike 用 StatusTextBlock）
    //   6. spike 版新增：顶部摘要栏（commits / contributors / branches / tags）
    //   7. spike 版新增：Contributor ItemsControl + DataTemplate
    //   8. spike 版用 Task.Run + Dispatcher.UIThread.Post 后台计算
    //   9. 跳过 RefreshBrushes / ApplicationThemeChanged（DynamicResource 主题 brush 自动跟随）
    //  10. 跳过 IconTools.GetImageSourceForPath / TargetReferenceGitPointView
    //  11. 跳过 CommitsUserControl / AuthorsUserControl 列表
    //
    // 本 spike 版暂不迁移（留 Phase 5.5b 或更后）：
    //   - 自绘 Treemap 图（ITreemapDelegate + DrawingContext + TextDrawer）
    //   - 文件路径聚合统计（CommitsCountDataSource.Item 递归层级）
    //   - Treemap.SelectionChanged 文件点击 → revisions + authors 详情
    //   - DateRangeButton 日期范围切换
    //   - Fallback Loading 进度遮罩
    //   - NotificationCenter.ApplicationThemeChanged 主题切换订阅
    //
    // 本 spike 版验证：
    //   - GetRepositoryOverviewDataGitCommand 后台调用工作（拿 Shas + UserIdentities）
    //   - contributor 维度统计（Name + CommitCount + 百分比 + 颜色块）渲染正常
    //   - 顶部摘要栏（commits / contributors / branches / tags）显示正确
    //   - Task.Run + Dispatcher.UIThread.Post 异步链路工作
    public partial class RepositoryOverviewWindow : CustomWindow
    {
        // 对照 WPF: GitModule _gitModule
        private readonly GitModule _gitModule;

        // spike 版：contributor 列表数据源（ItemsControl 绑定）
        private readonly ObservableCollection<ContributorItemViewModel> _contributors
            = new ObservableCollection<ContributorItemViewModel>();

        // spike 版：contributor 颜色调色板（10 种颜色循环分配）
        private static readonly string[] ContributorPalette =
        {
            "#4A90E2", "#E27D60", "#85DCBA", "#E8A87C", "#C38D9E",
            "#41B3A3", "#F47B7B", "#7FB069", "#9B59B6", "#F4A261"
        };

        // 构造函数（spike 版签名）：
        // 对照 WPF: (RepositoryUserControl repositoryUserControl, GitModule gitModule)
        // Avalonia: (GitModule gitModule)
        public RepositoryOverviewWindow(GitModule gitModule)
        {
            InitializeComponent();

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));

            // 对照 WPF: Title = string.Format(Translate("{0} - Repository Overview"), gitModule.RepositoryName)
            Title = FormatCurrent("{0} - Repository Overview", _gitModule.RepositoryName);

            // 对照 WPF: RepositoryNameTextBlock.Text = gitModule.Path
            RepositoryNameTextBlock.Text = _gitModule.RepositoryName;
            RepositoryPathTextBlock.Text = _gitModule.Path ?? string.Empty;
            ActiveBranchTextBlock.Text = string.Empty;

            // 绑定 ItemsControl
            TreemapItemsControl.ItemsSource = _contributors;

            // 对照 WPF: Fallback.Show() + FallbackTitle = "Loading..."
            StatusTextBlock.Text = Translate("Loading...");

            // 后台计算统计（对照 WPF JobQueue.Add）
            LoadStatisticsAsync();
        }

        // spike 版：后台加载统计数据
        // 对照 WPF: JobQueue.Add("Read repository overview", delegate(JobMonitor monitor) {...})
        private void LoadStatisticsAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    var monitor = new JobMonitor();

                    // 1. 拉取 RepositoryOverviewData（Shas + Authors + UserIdentities）
                    var overviewResult = new GetRepositoryOverviewDataGitCommand().Execute(_gitModule, monitor);
                    if (!overviewResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusTextBlock.Text = overviewResult.Error?.FriendlyDescription ?? Translate("Failed to load overview");
                        });
                        return;
                    }

                    RepositoryOverviewData data = overviewResult.Result;

                    // 2. 计算 contributor commit 数（按 Authors[] 索引聚合到 UserIdentity）
                    var contributorStats = ComputeContributorStats(data);

                    // 3. 统计 branches / tags 数（git for-each-ref）
                    int branchCount = CountRefs("refs/heads/");
                    int tagCount = CountRefs("refs/tags/");

                    // 4. UI 线程更新
                    Dispatcher.UIThread.Post(() =>
                    {
                        ApplyStatistics(data, contributorStats, branchCount, tagCount);
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("RepositoryOverviewWindow load statistics failed", ex);
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusTextBlock.Text = Translate("Failed to load overview");
                    });
                }
            });
        }

        // spike 版：计算每个 contributor 的 commit 数
        // 对照 WPF: 通过 Authors[] + UserIdentities[] 聚合
        private static List<(UserIdentity Identity, int Count)> ComputeContributorStats(RepositoryOverviewData data)
        {
            if (data.Authors == null || data.UserIdentities == null)
            {
                return new List<(UserIdentity, int)>();
            }

            var counts = new Dictionary<int, int>();
            foreach (int authorIndex in data.Authors)
            {
                if (authorIndex >= 0 && authorIndex < data.UserIdentities.Length)
                {
                    if (!counts.TryGetValue(authorIndex, out int c))
                    {
                        counts[authorIndex] = 1;
                    }
                    else
                    {
                        counts[authorIndex] = c + 1;
                    }
                }
            }

            var list = new List<(UserIdentity Identity, int Count)>();
            foreach (var kvp in counts)
            {
                list.Add((data.UserIdentities[kvp.Key], kvp.Value));
            }
            // 按 commit 数降序排序
            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            return list;
        }

        // spike 版：用 git for-each-ref 统计 branches/tags 数
        // 对照 WPF: repositoryUserControl.RepositoryData.References.LocalBranches.Length / Tags.Length
        private int CountRefs(string prefix)
        {
            try
            {
                GitRequestResult result = new GitRequest(_gitModule).Command("for-each-ref", "--format=%(refname)", prefix).Execute(silent: true);
                if (!result.Success || string.IsNullOrEmpty(result.Stdout))
                {
                    return 0;
                }
                int count = 0;
                string[] lines = result.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (Exception ex)
            {
                Log.Error("RepositoryOverviewWindow CountRefs(" + prefix + ") failed", ex);
                return 0;
            }
        }

        // spike 版：把统计数据写到 UI
        // 对照 WPF: repositoryOverviewWindow._repositoryOverviewData = overviewResponse.Result; +
        //          DateRangeButton.MinDate/MaxDate + Fallback.Hide()
        private void ApplyStatistics(
            RepositoryOverviewData data,
            List<(UserIdentity Identity, int Count)> contributorStats,
            int branchCount,
            int tagCount)
        {
            // 顶部摘要
            int totalCommits = data.Shas?.Length ?? 0;
            CommitCountTextBlock.Text = totalCommits.ToString();
            ContributorCountTextBlock.Text = contributorStats.Count.ToString();
            BranchCountTextBlock.Text = branchCount.ToString();
            TagCountTextBlock.Text = tagCount.ToString();

            // contributor Treemap 简化版
            _contributors.Clear();
            if (contributorStats.Count == 0 || totalCommits == 0)
            {
                StatusTextBlock.Text = Translate("No commits found");
                return;
            }

            for (int i = 0; i < contributorStats.Count; i++)
            {
                var (identity, count) = contributorStats[i];
                double percentage = (double)count / totalCommits;
                string color = ContributorPalette[i % ContributorPalette.Length];
                var brush = new SolidColorBrush(Color.Parse(color));

                _contributors.Add(new ContributorItemViewModel
                {
                    Name = string.IsNullOrEmpty(identity.Name) ? identity.Email ?? "(unknown)" : identity.Name,
                    CommitCount = count,
                    Percentage = percentage,
                    ColorBrush = brush,
                    // spike 版：Width 按比例（最小 80，最大 320）
                    Width = Math.Max(80, Math.Min(320, 80 + percentage * 1500)),
                    Height = 60,
                    Tooltip = FormatCurrent("{0} ({1} commits, {2:P1})", identity.Name ?? identity.Email ?? "(unknown)", count, percentage)
                });
            }

            StatusTextBlock.Text = FormatCurrent("Loaded {0} contributors, {1} commits", contributorStats.Count, totalCommits);
        }

        // 对照 WPF: private static string Translate(string text)
        // spike 版：用 ServiceLocator.Localization.Translate 替代 PreferencesLocalization.Translate
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }

        // 对照 WPF: PreferencesLocalization.FormatCurrent
        // spike 版：用 ServiceLocator.Localization.FormatCurrent 替代
        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }

    // spike 版：contributor 列表项 ViewModel（供 ItemsControl + DataTemplate 绑定）
    // 对照 WPF: 自绘 Treemap 中每个 item 用 CommitsCountDataSource.Item 表达
    //          spike 版用 ViewModel + DataTemplate 替代自绘
    public class ContributorItemViewModel
    {
        // contributor 显示名（Name，缺失时 fallback 到 Email）
        public string Name { get; set; } = string.Empty;

        // commit 数
        public int CommitCount { get; set; }

        // 占总 commit 数的比例（0.0 ~ 1.0）
        public double Percentage { get; set; }

        // spike Treemap 颜色块（每个 contributor 一个颜色）
        public IBrush ColorBrush { get; set; } = Brushes.Gray;

        // spike Treemap Border 宽高（按 commit 数比例）
        public double Width { get; set; } = 80;
        public double Height { get; set; } = 60;

        // 显示文本：commit 数
        public string CommitCountText => CommitCount.ToString();

        // 鼠标悬停 tooltip
        public string Tooltip { get; set; } = string.Empty;
    }
}
