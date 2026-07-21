using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ContributionHeatmap（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ContributionHeatmap.cs（375 行）：
    //   - WPF ContributionHeatmap : System.Windows.Controls.Grid
    //   - CommitsByDate DependencyProperty（Dictionary<DateTime, DayContributionInfo>）
    //   - 53 周 x 7 天的 GitHub 风格热力图，每个 Border 表示一天
    //   - 5 步色阶调色板（按 maxCommits 比例分配 level 0-4）
    //   - 主题感知调色板（IsDarkBase 切换深色/浅色默认值）
    //   - Heatmap.LevelNColor 资源覆盖（CustomColorsDialog 自定义颜色，WPF TryFindResource）
    //   - 每日 ToolTip 显示 "{0} contributions on {1}" + Top 3 authors
    //   - 底部图例（Less [5 色块] More）+ 摘要（Total / Longest streak / Most active）
    //   - WeakEventManager 订阅 NotificationCenter.ApplicationThemeChanged：主题变化时刷新
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.Grid → Avalonia.Controls.Grid
    //   2. DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF OnCommitsByDateChanged(DependencyObject, DependencyPropertyChangedEventArgs) →
    //      override OnPropertyChanged(AvaloniaPropertyChangedEventArgs) 检查 CommitsByDateProperty
    //   4. WPF Border.ToolTip = string → Avalonia ToolTip.SetTip(border, string)（附加属性 API）
    //   5. WPF Border.CornerRadius = new CornerRadius(2) → spike 用 Rectangle + RadiusX/Y=2
    //   6. WPF Application.Current.TryFindResource(key) 资源查找 → spike 跳过
    //      （直接用 IsDarkBase 默认调色板，不读 Heatmap.LevelNColor 资源）
    //   7. WPF Theme.SecondaryLabelBrush → 硬编码 #888888（spike 兜底，Theme 类未迁移）
    //   8. WPF WeakEventManager NotificationCenter.ApplicationThemeChanged → spike 跳过
    //      （Avalonia 主题切换由外部重新调用 SetData 触发刷新）
    //   9. WPF PreferencesLocalization.Translate(text, lang) → ServiceLocator.Localization.Translate(text, lang)
    //      （Phase 0.3 已把纯字符串逻辑抽到 Core 的 LocalizationService）
    //  10. WPF Brush → Avalonia IBrush（API 一致）
    //  11. WPF SolidColorBrush(c) 不 Freeze → Avalonia SolidColorBrush(c) 无 Freeze 概念
    //
    // spike 简化（task spec 关键 API）：
    //   - SetData(Dictionary<DateTime, DayContributionInfo> data)（task spec 关键 API）
    //   - Refresh() 公共方法（task spec 关键 API）
    //   - spike 用 Canvas + Rectangle 网格绘制（task spec：7 行 x 53 列），
    //     替代 WPF Grid + Border + ColumnDefinitions + RowDefinitions 布局
    public class ContributionHeatmap : Grid
    {
        // task spec 关键 API：CommitsByDate StyledProperty
        // 对照 WPF: CommitsByDateProperty (DependencyProperty.Register, PropertyMetadata null)
        public static readonly StyledProperty<Dictionary<DateTime, DayContributionInfo>> CommitsByDateProperty =
            AvaloniaProperty.Register<ContributionHeatmap, Dictionary<DateTime, DayContributionInfo>>(nameof(CommitsByDate));

        // spike 版硬编码画刷（替代 WPF Theme.SecondaryLabelBrush）
        // 对照 WPF: Foreground = Theme.SecondaryLabelBrush（图例 + 摘要文本）
        private static readonly IBrush SecondaryLabelBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        // 53 周 x 7 天 = 371 单元（覆盖一年）
        // 对照 WPF WeeksCount=53 + DayCount=7
        private const int WeeksCount = 53;
        private const int DayCount = 7;

        // 单元格尺寸（对照 WPF CellSize=11.0 + CellGap=3.0）
        private const double CellSize = 11.0;
        private const double CellGap = 3.0;
        private const double LegendCellSize = 10.0;
        private const int MaxAuthorsShown = 3;

        // spike 版用 Canvas + Rectangle（task spec：7 行 x 53 列网格）
        // 对照 WPF Grid + Border 布局（更复杂）
        // Canvas 显式 Width/Height（Grid+Border 在 Avalonia 中自动布局，
        // 但 Canvas 不会，必须显式给尺寸才能正确渲染所有 Rectangle）
        private readonly Canvas _heatmapCanvas;
        private readonly Rectangle[] _legendCells = new Rectangle[5];
        private readonly TextBlock _summaryText;

        // task spec 关键 API：CommitsByDate 属性
        // 对照 WPF: public Dictionary<DateTime, DayContributionInfo> CommitsByDate
        public Dictionary<DateTime, DayContributionInfo> CommitsByDate
        {
            get => GetValue(CommitsByDateProperty);
            set => SetValue(CommitsByDateProperty, value);
        }

        public ContributionHeatmap()
        {
            // 外层两行：热力图 Canvas + (图例/摘要)
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // spike：Canvas 直接 host 53x7=371 个 Rectangle
            _heatmapCanvas = new Canvas
            {
                Width = WeeksCount * (CellSize + CellGap),
                Height = DayCount * (CellSize + CellGap)
            };
            SetRow(_heatmapCanvas, 0);
            Children.Add(_heatmapCanvas);

            // 底部行：图例 + 摘要，水平排列
            // 对照 WPF: StackPanel bottomPanel (Orientation=Horizontal, Margin=0,8,0,0)
            StackPanel bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };
            SetRow(bottomPanel, 1);
            Children.Add(bottomPanel);

            // 图例：Less [5 色块] More
            // 对照 WPF: StackPanel legendPanel (Orientation=Horizontal, VerticalAlignment=Center)
            StackPanel legendPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock lessLabel = new TextBlock
            {
                Text = TranslateLegend("Less"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0),
                Foreground = SecondaryLabelBrush
            };
            legendPanel.Children.Add(lessLabel);
            for (int k = 0; k < 5; k++)
            {
                // spike 用 Rectangle + RadiusX/Y=2（task spec：Canvas + Rectangle 网格）
                // 对照 WPF: Border (Width=10, Height=10, CornerRadius=2, Margin=0,0,2,0)
                Rectangle legendCell = new Rectangle
                {
                    Width = LegendCellSize,
                    Height = LegendCellSize,
                    RadiusX = 2,
                    RadiusY = 2,
                    Margin = new Thickness(0, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                _legendCells[k] = legendCell;
                legendPanel.Children.Add(legendCell);
            }
            TextBlock moreLabel = new TextBlock
            {
                Text = TranslateLegend("More"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(4, 0, 16, 0),
                Foreground = SecondaryLabelBrush
            };
            legendPanel.Children.Add(moreLabel);
            bottomPanel.Children.Add(legendPanel);

            // 摘要文本
            // 对照 WPF: TextBlock _summaryText (VerticalAlignment=Center, FontSize=11,
            //   TextWrapping=NoWrap, Foreground=Theme.SecondaryLabelBrush)
            _summaryText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = SecondaryLabelBrush
            };
            bottomPanel.Children.Add(_summaryText);
        }

        // 对照 WPF: private static string TranslateLegend(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   Avalonia: ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //            （Phase 0.3 已把纯字符串逻辑抽到 Core 的 LocalizationService）
        private static string TranslateLegend(string text)
        {
            return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
        }

        // 对照 WPF: OnCommitsByDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //   WPF: 通过 PropertyMetadata 的 PropertyChangedCallback 触发 RebuildCells
        //   Avalonia: override OnPropertyChanged 检查 CommitsByDateProperty
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == CommitsByDateProperty)
            {
                RebuildCells();
            }
        }

        // task spec 关键 API：SetData(Dictionary<DateTime, DayContributionInfo> data)
        // 对照 WPF: 直接赋值 CommitsByDate 属性（依赖属性变更触发 RebuildCells）
        // spike 版新增公共方法封装赋值（task spec 要求）
        public void SetData(Dictionary<DateTime, DayContributionInfo> data)
        {
            CommitsByDate = data;
        }

        // task spec 关键 API：Refresh() 公共方法
        // 对照 WPF: 通过 OnPropertyChanged 触发刷新
        // spike 版新增公共方法允许手动触发刷新（task spec 要求，
        //   外部主题切换时直接调用 Refresh 即可，无需重新 SetData）
        public void Refresh()
        {
            RebuildCells();
        }

        // 对照 WPF: private void RebuildCells()
        //   WPF: 清空 _heatmapGrid.Children + 按 week/dow 重建 Border
        //   spike: 清空 _heatmapCanvas.Children + 按 week/dow 重建 Rectangle + Canvas.SetLeft/Top 定位
        private void RebuildCells()
        {
            _heatmapCanvas.Children.Clear();
            Dictionary<DateTime, DayContributionInfo> data = CommitsByDate;
            if (data == null)
            {
                _summaryText.Text = "";
                return;
            }
            DateTime today = DateTime.Today;
            int todayDow = (int)today.DayOfWeek;
            DateTime lastSunday = today.AddDays(-todayDow);
            DateTime startDate = lastSunday.AddDays(-(WeeksCount - 1) * 7);
            int maxCommits = 0;
            foreach (KeyValuePair<DateTime, DayContributionInfo> kvp in data)
            {
                if (kvp.Value.Commits > maxCommits)
                {
                    maxCommits = kvp.Value.Commits;
                }
            }
            IBrush[] palette = GetPalette();
            string tooltipFormat = ServiceLocator.Localization.Translate("{0} contributions on {1}", ForkPlusSettings.Default.UiLanguage);
            string authorsFormat = ServiceLocator.Localization.Translate("Authors: {0}", ForkPlusSettings.Default.UiLanguage);
            string moreFormat = ServiceLocator.Localization.Translate("+{0} more", ForkPlusSettings.Default.UiLanguage);
            for (int week = 0; week < WeeksCount; week++)
            {
                for (int dow = 0; dow < DayCount; dow++)
                {
                    DateTime date = startDate.AddDays(week * 7 + dow);
                    if (date > today)
                    {
                        continue;
                    }
                    DayContributionInfo info = data.TryGetValue(date, out var c) ? c : null;
                    int commits = info?.Commits ?? 0;
                    int level = GetLevel(commits, maxCommits);
                    // spike 用 Rectangle + Canvas.SetLeft/Top 定位（task spec：Canvas + Rectangle 网格）
                    // 对照 WPF: Border + Grid.SetColumn/SetRow
                    Rectangle rect = new Rectangle
                    {
                        Width = CellSize,
                        Height = CellSize,
                        RadiusX = 2,
                        RadiusY = 2,
                        Fill = palette[level],
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    ToolTip.SetTip(rect, BuildTooltip(tooltipFormat, authorsFormat, moreFormat, date, commits, info));
                    Canvas.SetLeft(rect, week * (CellSize + CellGap));
                    Canvas.SetTop(rect, dow * (CellSize + CellGap));
                    _heatmapCanvas.Children.Add(rect);
                }
            }

            // 刷新图例色块颜色
            // 对照 WPF: for k in 5: _legendCells[k].Background = palette[k]
            // spike: Rectangle.Fill = palette[k]（Rectangle 用 Fill 而非 Background）
            for (int k = 0; k < 5; k++)
            {
                _legendCells[k].Fill = palette[k];
            }

            // 计算并刷新统计摘要
            _summaryText.Text = BuildSummary(data);
        }

        // 对照 WPF: private static string BuildTooltip(string line1Format, string authorsFormat, string moreFormat,
        //   DateTime date, int commits, DayContributionInfo info)
        //   WPF: 第 1 行 "{0} contributions on {1}" + 第 2 行 "Authors: {0}"（Top N + "+M more"）
        private static string BuildTooltip(string line1Format, string authorsFormat, string moreFormat,
                                            DateTime date, int commits, DayContributionInfo info)
        {
            string dateStr = date.ToString("yyyy-MM-dd ddd", CultureInfo.CurrentCulture);
            string line1 = string.Format(line1Format, commits, dateStr);
            if (commits <= 0 || info == null || info.AuthorCount == 0)
            {
                return line1;
            }
            List<string> top = info.GetTopAuthors(MaxAuthorsShown);
            if (top.Count == 0)
            {
                return line1;
            }
            int remaining = info.AuthorCount - top.Count;
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Join(", ", top));
            if (remaining > 0)
            {
                sb.Append(", ").Append(string.Format(moreFormat, remaining));
            }
            string line2 = string.Format(authorsFormat, sb.ToString());
            return line1 + Environment.NewLine + line2;
        }

        // 对照 WPF: private static string BuildSummary(Dictionary<DateTime, DayContributionInfo> data)
        //   摘要：总贡献数 | 最长连续提交天数 | 最活跃日
        //   连续天数基于 data 中有 commits 的日期排序后逐日判定（gap == 1 天）
        private static string BuildSummary(Dictionary<DateTime, DayContributionInfo> data)
        {
            if (data == null || data.Count == 0)
            {
                return "";
            }
            int total = 0;
            int longestStreak = 0;
            int currentStreak = 0;
            DateTime? prevDate = null;
            DateTime mostActiveDate = DateTime.MinValue;
            int mostActiveCount = 0;
            // 按日期升序遍历，同时累计 total / streak / most active
            foreach (KeyValuePair<DateTime, DayContributionInfo> kvp in data.OrderBy(kv => kv.Key))
            {
                int commits = kvp.Value?.Commits ?? 0;
                total += commits;
                if (commits > 0)
                {
                    if (prevDate.HasValue && kvp.Key == prevDate.Value.AddDays(1))
                    {
                        currentStreak++;
                    }
                    else
                    {
                        currentStreak = 1;
                    }
                    if (currentStreak > longestStreak)
                    {
                        longestStreak = currentStreak;
                    }
                    if (commits > mostActiveCount)
                    {
                        mostActiveCount = commits;
                        mostActiveDate = kvp.Key;
                    }
                    prevDate = kvp.Key;
                }
                else
                {
                    currentStreak = 0;
                    prevDate = null;
                }
            }

            string totalFormat = ServiceLocator.Localization.Translate("Total: {0}", ForkPlusSettings.Default.UiLanguage);
            string streakFormat = ServiceLocator.Localization.Translate("Longest streak: {0} days", ForkPlusSettings.Default.UiLanguage);
            string mostActiveFormat = ServiceLocator.Localization.Translate("Most active: {0} ({1})", ForkPlusSettings.Default.UiLanguage);
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format(totalFormat, total));
            if (longestStreak > 0)
            {
                sb.Append("  ·  ").Append(string.Format(streakFormat, longestStreak));
            }
            if (mostActiveCount > 0)
            {
                string dateStr = mostActiveDate.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
                sb.Append("  ·  ").Append(string.Format(mostActiveFormat, dateStr, mostActiveCount));
            }
            return sb.ToString();
        }

        // 对照 WPF: private static int GetLevel(int commits, int maxCommits)
        //   ratio <= 0.25 → 1; <= 0.5 → 2; <= 0.75 → 3; > 0.75 → 4; 0 commits → 0
        private static int GetLevel(int commits, int maxCommits)
        {
            if (commits <= 0 || maxCommits <= 0)
            {
                return 0;
            }
            double ratio = (double)commits / (double)maxCommits;
            if (ratio <= 0.25) return 1;
            if (ratio <= 0.5) return 2;
            if (ratio <= 0.75) return 3;
            return 4;
        }

        // 对照 WPF: private static Brush[] GetPalette()
        //   WPF: 优先读 Heatmap.LevelNColor 资源（TryFindResource），回退到 IsDarkBase 默认值
        //   spike: 直接用 IsDarkBase 默认调色板（跳过资源查找，简化）
        private static IBrush[] GetPalette()
        {
            // spike 版硬编码调色板（对照 WPF defaults 数组）
            // 深色主题：GitHub Dark 风格（深绿渐变）
            // 浅色主题：GitHub Light 风格（浅绿渐变）
            bool isDark = ForkPlusSettings.Default.Theme.IsDarkBase();
            Color[] defaults = isDark
                ? new Color[5]
                {
                    Color.FromRgb(22, 27, 34),
                    Color.FromRgb(3, 58, 22),
                    Color.FromRgb(25, 111, 26),
                    Color.FromRgb(46, 160, 67),
                    Color.FromRgb(63, 217, 94)
                }
                : new Color[5]
                {
                    Color.FromRgb(235, 237, 240),
                    Color.FromRgb(155, 233, 168),
                    Color.FromRgb(64, 196, 99),
                    Color.FromRgb(48, 161, 78),
                    Color.FromRgb(33, 110, 57)
                };
            IBrush[] palette = new IBrush[5];
            for (int i = 0; i < 5; i++)
            {
                palette[i] = new SolidColorBrush(defaults[i]);
            }
            return palette;
        }
    }
}
