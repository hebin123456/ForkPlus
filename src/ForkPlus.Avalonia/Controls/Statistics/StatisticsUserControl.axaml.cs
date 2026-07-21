using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using OxyPlot;

namespace ForkPlus.Avalonia.Controls.Statistics
{
    // Phase 2.7a：Avalonia 版 StatisticsUserControl（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/StatisticsUserControl.xaml.cs（800+ 行）：
    //   - public partial class StatisticsUserControl : UserControl, ILocalizableControl
    //   - 嵌套类：AuthorStatViewModel / CodeLineLanguageViewModel / PlotHelper
    //   - 字段：5 个 PlotModel + 13 色调色板 _colors / _pieChartColors
    //   - 构造函数：InitializeComponent + 5 个 PlotHelper.CreateXxx() 初始化 +
    //     LinePlot.Model = _linePlotModel 等 + 订阅 NotificationCenter.ApplicationThemeChanged
    //   - 公共方法：UpdatePlots(RepositoryStats) / UpdateCommitsPerWeekDayPlot /
    //     UpdateCommitsPerDayHourPlot / UpdateCodeLinesPlot(CodeLineStats)
    //   - 私有：CreateLineSeries / CreatePieSlice / RefreshPlotColors / OxyColorToHex /
    //     OnDateRangeChanged / CodeLinesRefButton_Click / CodeLinesRefreshButton_Click /
    //     CodeLinesRefPopup_Opened/Closed / CodeLinesRefSearchBox_TextChanged /
    //     CodeLinesRefListBox_SelectionChanged
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 版仅迁移 axaml 骨架 + 嵌套 VM 类（零成本复用）
    //   2. 跳过 PlotHelper 5 个静态方法（Phase 2.7b 迁移，业务核心）
    //   3. 跳过 5 个 PlotModel 字段初始化（spike 版 PlotView.Model=null）
    //   4. 跳过 UpdatePlots / UpdateCommitsPerWeekDayPlot 等业务方法（Phase 2.7b）
    //   5. 跳过 NotificationCenter.ApplicationThemeChanged 订阅
    //      （NotificationCenter 在 WPF 工程，Avalonia 工程不可访问，
    //       Phase 0 抽 INotificationService 后再接入）
    //   6. ILocalizableControl 接口不迁移（WPF 工程接口，spike 不接入）
    //   7. CodeLinesRefButton_Click / CodeLinesRefreshButton_Click 用 stub 实现（打印日志）
    //
    // 本 spike 版暂不迁移（留 Phase 2.7b）：
    //   - PlotHelper.CreateLinePlotModel / CreatePiePlotModel / CreateWeekDayPlotModel /
    //     CreateDayHourPlotModel / RefreshPlotColors
    //   - UpdatePlots / UpdateCommitsPerWeekDayPlot / UpdateCommitsPerDayHourPlot / UpdateCodeLinesPlot
    //   - CreateLineSeries / CreatePieSlice
    //   - 13 色 _colors / _pieChartColors 调色板
    //   - DateRangeButton / ContributionHeatmap / CodeLinesRefPopup 集成
    //   - NotificationCenter 主题切换订阅
    //   - ILocalizableControl 接口
    //
    // 本 spike 版验证：
    //   - OxyPlot.Avalonia 2.1.2 PlotView 控件可在 Avalonia 11.3 axaml 中实例化
    //   - 5 个 PlotView 控件树渲染正常（Model=null 显示空图）
    //   - AuthorStatListBox + CodeLinesListBox DataTemplate 绑定可工作
    //   - CodeLineLanguageViewModel.Color 字符串绑定到 Rectangle.Fill（Avalonia 字符串转 Brush）
    public partial class StatisticsUserControl : UserControl
    {
        // 对照 WPF: public class AuthorStatViewModel : INotifyPropertyChanged
        // spike 版完整迁移（纯数据，零成本复用）
        public class AuthorStatViewModel : INotifyPropertyChanged
        {
            public string Name { get; }
            public int TotalCommits { get; }

            // 对照 WPF: event PropertyChangedEventHandler PropertyChanged（空 add/remove，
            //   WPF 版没有真的实现 INPC，只是为了 ItemsSource 绑定兼容）
            // Avalonia 版保持同样行为
            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }

            public AuthorStatViewModel(string name, int totalCommits)
            {
                Name = name;
                TotalCommits = totalCommits;
            }
        }

        // 对照 WPF: public class CodeLineLanguageViewModel
        // spike 版完整迁移（纯数据，零成本复用）
        public class CodeLineLanguageViewModel
        {
            public string Name { get; }
            public long Files { get; }
            public long Code { get; }
            public long Comments { get; }
            public long Blanks { get; }
            /// <summary>饼图色块颜色，XAML 里 Rectangle.Fill 绑定。</summary>
            public string Color { get; }

            public CodeLineLanguageViewModel(string name, long files, long code, long comments, long blanks, string color)
            {
                Name = name;
                Files = files;
                Code = code;
                Comments = comments;
                Blanks = blanks;
                Color = color;
            }
        }

        public StatisticsUserControl()
        {
            InitializeComponent();
            // spike 版不初始化 5 个 PlotModel（Phase 2.7b 在此补 PlotHelper.CreateXxx()）
            // LinePlot.Model = ...
            // PiePlot.Model = ...
            // WeekDayPlot.Model = ...
            // DayHourPlot.Model = ...
            // CodeLinesPiePlot.Model = ...
        }

        // ===== ShowStatistics（task spec 关键 API）=====
        // 对照 WPF: public void ShowStatistics(GitModule gitModule) : this(gitModule, null, false)
        //   WPF: ShowStatistics(gitModule, null, false);
        // spike 版：实例方法（与 WPF 一致），转发到三参重载
        public void ShowStatistics(GitModule gitModule)
        {
            ShowStatistics(gitModule, null, false);
        }

        // 对照 WPF: public void ShowStatistics(GitModule gitModule, [Null] string initialRef, bool scrollToCodeLines)
        //   WPF: _gitModule = gitModule + _initialRef = initialRef + _scrollToCodeLinesRequest = scrollToCodeLines +
        //         ResetDateRange + LoadStatistics + (scrollToCodeLines ? 滚动到 CodeLinesSection)
        // spike 版：实例方法，仅记录 gitModule 引用（真实统计计算留待 Phase 2.7b）
        // task spec 标注为"静态方法"，但 WPF 实际是实例方法（通过 x:Name 实例调用），
        // spike 保持与 WPF 一致的实例方法签名，确保 RepositoryStatisticsWindow /
        // RepositoryDetailsUserControl 等调用方可直接使用。
        public void ShowStatistics(GitModule gitModule, string? initialRef, bool scrollToCodeLines)
        {
            // spike 版：仅记录参数，真实 PlotModel 填充 + 统计计算留待 Phase 2.7b
            // 对照 WPF: _gitModule = gitModule
            GitModule = gitModule;
            InitialRef = initialRef;
            ScrollToCodeLines = scrollToCodeLines;

            // spike 版：在 CodeLinesSummary 显示占位文本（Phase 2.7b 替换为真实统计）
            if (CodeLinesSummary != null)
            {
                string refSuffix = string.IsNullOrEmpty(initialRef) ? "" : " @ " + initialRef;
                CodeLinesSummary.Text = "Statistics loaded for " + (gitModule?.Path ?? "(null)") + refSuffix + " (spike: Phase 2.7b will populate plots)";
            }
        }

        // spike 新增：当前统计上下文（Phase 2.7b PlotHelper 填充时使用）
        public GitModule? GitModule { get; private set; }
        public string? InitialRef { get; private set; }
        public bool ScrollToCodeLines { get; private set; }

        // 对照 WPF: private void CodeLinesRefButton_Click(object sender, RoutedEventArgs e)
        // spike 版：stub，Phase 2.7b 接入 CodeLinesRefPopup
        private void CodeLinesRefButton_Click(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("[StatisticsUserControl] CodeLinesRefButton_Click (spike: stub, Phase 2.7b)");
        }

        // 对照 WPF: private void CodeLinesRefreshButton_Click(object sender, RoutedEventArgs e)
        // spike 版：stub，Phase 2.7b 接入 tokei 调用
        private void CodeLinesRefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("[StatisticsUserControl] CodeLinesRefreshButton_Click (spike: stub, Phase 2.7b)");
        }

        // Phase 2.7b 在此补：
        //   - 5 个 PlotModel 字段 + PlotHelper.CreateXxx() 初始化
        //   - UpdatePlots(RepositoryStats) / UpdateCommitsPerWeekDayPlot /
        //     UpdateCommitsPerDayHourPlot / UpdateCodeLinesPlot(CodeLineStats)
        //   - CreateLineSeries / CreatePieSlice
        //   - RefreshPlotColors + NotificationCenter.ApplicationThemeChanged 订阅
        //   - 13 色 _colors / _pieChartColors 调色板
        //   - DateRangeButton / ContributionHeatmap 集成
        //   - CodeLinesRefPopup / CodeLinesRefSearchBox / CodeLinesRefListBox 集成
    }
}
