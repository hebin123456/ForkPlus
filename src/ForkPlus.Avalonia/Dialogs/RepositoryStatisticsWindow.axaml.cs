using System;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 RepositoryStatisticsWindow（真实迁移版，对照 WPF RepositoryStatisticsWindow.xaml.cs 47 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RepositoryStatisticsWindow.xaml.cs：
    //   - public partial class RepositoryStatisticsWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / string _initialRef / bool _scrollToCodeLines
    //   - 构造函数 (GitModule gitModule) 转调 (GitModule, string? initialRef, bool scrollToCodeLines)
    //     * ShowLogo = false / ShowHeader = false
    //     * ShowCancelButton = false
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Close")
    //     * ResizeMode = CanResizeWithGrip
    //     * RepositoryNameTextBlock.Text = gitModule.Path
    //     * Loaded += RepositoryStatisticsWindow_Loaded
    //   - RepositoryStatisticsWindow_Loaded: StatisticsUserControl.ShowStatistics(
    //       _gitModule, _initialRef, _scrollToCodeLines)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. StatisticsUserControl 是 WPF 自定义控件，spike 版用 StatisticsPlaceholderTextBlock
    //      显示 "Statistics - loading..." 占位（实际统计功能留待迁移）
    //   3. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   4. ResizeMode.CanResizeWithGrip → CanResize="True"（Avalonia 11 无 Grip 模式，axaml 中声明）
    //   5. spike 基类不提供 ShowLogo/ShowHeader，子类 axaml 中不放 Logo Image 即等价
    //   6. Loaded += （Avalonia 同名事件，参数 EventArgs）
    //   7. spike 保留 RepositoryNameTextBlock 显示 gitModule.Path
    public partial class RepositoryStatisticsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly string? _initialRef;
        private readonly bool _scrollToCodeLines;

        // 对照 WPF: public RepositoryStatisticsWindow(GitModule gitModule) : this(gitModule, null, false)
        public RepositoryStatisticsWindow(GitModule gitModule)
            : this(gitModule, null, false)
        {
        }

        // 对照 WPF: public RepositoryStatisticsWindow(GitModule gitModule, string initialRef, bool scrollToCodeLines)
        // spike 版 initialRef 改为 string?（WPF 用 [Null] 标注，可空语义一致）
        public RepositoryStatisticsWindow(GitModule gitModule, string? initialRef, bool scrollToCodeLines)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _initialRef = initialRef;
            _scrollToCodeLines = scrollToCodeLines;

            // 对照 WPF: ShowLogo = false / ShowHeader = false
            // spike 版：基类不提供 ShowLogo/ShowHeader，子类 axaml 中不放 Logo Image 即等价

            // 对照 WPF: ShowCancelButton = false
            ShowCancelButton = false;

            // 对照 WPF: SubmitButtonTitle = PreferencesLocalization.Current("Close")
            SubmitButtonTitle = Translate("Close");

            // 对照 WPF: DialogTitle / DialogDescription（WPF 通过 ShowHeader=false 隐藏，
            // spike 保留 TitleTextBlock 显示标题，DescriptionTextBlock 留空）
            string title = Translate("Repository Statistics");
            Title = title;
            DialogTitle = title;
            DialogDescription = "";

            // 对照 WPF: RepositoryNameTextBlock.Text = gitModule.Path
            RepositoryNameTextBlock.Text = gitModule.Path;

            // 对照 WPF: Loaded += RepositoryStatisticsWindow_Loaded
            Loaded += RepositoryStatisticsWindow_Loaded;
        }

        // 对照 WPF: private void RepositoryStatisticsWindow_Loaded(object sender, RoutedEventArgs e)
        // Avalonia 11: Loaded 事件参数为 EventArgs
        // spike 版：StatisticsUserControl 是 WPF 控件，spike 版用 StatisticsPlaceholderTextBlock
        // 显示仓库路径占位（实际统计功能留待迁移）
        private void RepositoryStatisticsWindow_Loaded(object? sender, EventArgs e)
        {
            // 对照 WPF: StatisticsUserControl.ShowStatistics(_gitModule, _initialRef, _scrollToCodeLines)
            // spike 版：仅显示仓库路径占位文本
            string refSuffix = string.IsNullOrEmpty(_initialRef) ? "" : " @ " + _initialRef;
            StatisticsPlaceholderTextBlock.Text = "Statistics - loading..." + refSuffix;
        }

        // 对照 WPF: PreferencesLocalization.Current(text) / PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
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
    }
}
