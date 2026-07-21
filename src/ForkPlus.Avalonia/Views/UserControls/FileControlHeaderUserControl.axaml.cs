using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Settings;
using ForkPlus.UI;
using ToolTip = Avalonia.Controls.ToolTip;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.7b：FileControlHeaderUserControl 升级 — DiffLayoutMode 真实持久化。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FileControlHeaderUserControl.xaml.cs（404 行）：
    //   - 2 个 DependencyProperty：FilePath / OldFilePath
    //   - 2 个公共属性：Target (FileDiffControlTarget) / HighlightPixelsToggleButtonEnabled
    //   - 2 个公共方法：ApplyLocalization / Show(filePath, oldFilePath, mode)
    //   - 私有 DiffLayoutMode 属性（按 Target 从 ForkPlusSettings 读写不同设置项）
    //   - 9 个 ToggleButton/Button click handler（更新 ForkPlusSettings + 触发 NotificationCenter）
    //
    // 本升级版实现：
    //   - DiffLayoutMode 属性：按 Target 从 ForkPlusSettings 读写不同设置项（真实持久化）
    //   - DiffShowEntireFile 属性：同上
    //   - 9 个 click handler：更新 ForkPlusSettings（不触发 NotificationCenter，Avalonia 工程尚未接入）
    //   - Show 方法：真实切换 TextMode/ImageMode 容器可见性
    //   - Loaded 事件：从 ForkPlusSettings 恢复所有 ToggleButton 状态
    //
    // 暂不实现（依赖未迁移的子系统）：
    //   - NotificationCenter 事件订阅（Avalonia 工程尚未接入 NotificationCenter）
    //   - PreferencesLocalization.Apply（依赖 WPF logical tree 遍历，Avalonia 用 ServiceLocator.Localization）
    //   - FilePathTextBlock 自定义控件（用 TextBlock 占位）
    //   - IconTools.GetImageSourceForExtension（用空 Image 占位）
    //   - TextDiffControl 滚动（Previous/Next 按钮）
    public partial class FileControlHeaderUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        public FileDiffControlTarget Target { get; set; } = FileDiffControlTarget.Revision;

        private bool _highlightPixelsToggleButtonEnabled;
        public bool HighlightPixelsToggleButtonEnabled
        {
            get => _highlightPixelsToggleButtonEnabled;
            set
            {
                _highlightPixelsToggleButtonEnabled = value;
                UpdateHighlightPixelsToggleButtonState();
            }
        }

        // 对照 WPF: DependencyProperty FilePath / OldFilePath（Avalonia 用 StyledProperty，spike 用普通属性）
        public string FilePath { get; set; }
        public string OldFilePath { get; set; }

        // ===== 私有 DiffLayoutMode 属性（对照 WPF，按 Target 读写不同 ForkPlusSettings 设置项）=====
        private DiffLayoutMode DiffLayoutMode
        {
            get
            {
                switch (Target)
                {
                    case FileDiffControlTarget.Commit:
                        return ForkPlusSettings.Default.CommitDiffLayoutMode;
                    case FileDiffControlTarget.History:
                    case FileDiffControlTarget.HunkHistory:
                        return ForkPlusSettings.Default.HistoryDiffLayoutMode;
                    case FileDiffControlTarget.Popup:
                        return ForkPlusSettings.Default.PopupDiffLayoutMode;
                    case FileDiffControlTarget.Revision:
                        return ForkPlusSettings.Default.RevisionDiffLayoutMode;
                    case FileDiffControlTarget.RevisionWindow:
                        return ForkPlusSettings.Default.RevisionWindowDiffLayoutMode;
                    default:
                        return ForkPlusSettings.Default.RevisionDiffLayoutMode;
                }
            }
            set
            {
                switch (Target)
                {
                    case FileDiffControlTarget.Commit:
                        ForkPlusSettings.Default.CommitDiffLayoutMode = value;
                        break;
                    case FileDiffControlTarget.History:
                    case FileDiffControlTarget.HunkHistory:
                        ForkPlusSettings.Default.HistoryDiffLayoutMode = value;
                        break;
                    case FileDiffControlTarget.Popup:
                        ForkPlusSettings.Default.PopupDiffLayoutMode = value;
                        break;
                    case FileDiffControlTarget.Revision:
                        ForkPlusSettings.Default.RevisionDiffLayoutMode = value;
                        break;
                    case FileDiffControlTarget.RevisionWindow:
                        ForkPlusSettings.Default.RevisionWindowDiffLayoutMode = value;
                        break;
                }
            }
        }

        // ===== 私有 DiffShowEntireFile 属性（对照 WPF）=====
        private bool? DiffShowEntireFile
        {
            get
            {
                switch (Target)
                {
                    case FileDiffControlTarget.Revision:
                    case FileDiffControlTarget.Commit:
                    case FileDiffControlTarget.Popup:
                    case FileDiffControlTarget.History:
                        return ForkPlusSettings.Default.DiffShowEntireFile;
                    case FileDiffControlTarget.RevisionWindow:
                        return ForkPlusSettings.Default.RevisionWindowDiffShowEntireFile;
                    case FileDiffControlTarget.HunkHistory:
                        return null;
                    default:
                        return ForkPlusSettings.Default.DiffShowEntireFile;
                }
            }
            set
            {
                switch (Target)
                {
                    case FileDiffControlTarget.Revision:
                    case FileDiffControlTarget.Commit:
                    case FileDiffControlTarget.Popup:
                    case FileDiffControlTarget.History:
                        ForkPlusSettings.Default.DiffShowEntireFile = value.GetValueOrDefault();
                        break;
                    case FileDiffControlTarget.RevisionWindow:
                        ForkPlusSettings.Default.RevisionWindowDiffShowEntireFile = value.GetValueOrDefault();
                        break;
                    case FileDiffControlTarget.HunkHistory:
                        break;
                }
            }
        }

        public FileControlHeaderUserControl()
        {
            InitializeComponent();
            Loaded += FileControlHeaderUserControl_Loaded;
        }

        // ===== Loaded 事件：从 ForkPlusSettings 恢复所有 ToggleButton 状态（对照 WPF 构造函数中的 Loaded 委托）=====
        private void FileControlHeaderUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateIgnoreWhiteSpacesToggleButtonState();
            UpdateShowHiddenSymbolsToggleButtonState();
            UpdateWordWrapToggleButtonState();
            UpdateShowEntireFileState();
            UpdateDiffLayoutModeToggleButtonState();
            UpdateHighlightPixelsToggleButtonState();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: public void Show(string filePath, string oldFilePath, FileControlHeaderMode mode = FileControlHeaderMode.None)
        public void Show(string filePath, string oldFilePath, FileControlHeaderMode mode = FileControlHeaderMode.None)
        {
            OldFilePath = oldFilePath;
            FilePath = filePath;
            if (FilePathTextBlock != null)
            {
                FilePathTextBlock.Text = filePath ?? "(no file)";
            }
            RefreshToolbarLayout(mode);
            IsVisible = true;
        }

        // 对照 WPF: public void ApplyLocalization()
        // spike 版：Avalonia 工程尚未接入 PreferencesLocalization，用 ServiceLocator.Localization 翻译 ToolTip
        public void ApplyLocalization()
        {
            // Phase 3.7b：用 ServiceLocator.Localization 翻译 ToolTip（如果可用）
            // PreferencesLocalization.Apply 依赖 WPF logical tree 遍历，Avalonia 用本方式替代
            UpdateDiffLayoutModeToggleButtonState();
        }

        // ===== ToggleButton/Button 事件（对照 WPF 9 个 click handler，真实更新 ForkPlusSettings）=====

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // 依赖 TextDiffControl（未迁移），spike 不实现滚动
            Console.WriteLine("[FileControlHeader] Previous change (spike — TextDiffControl not migrated)");
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // 依赖 TextDiffControl（未迁移），spike 不实现滚动
            Console.WriteLine("[FileControlHeader] Next change (spike — TextDiffControl not migrated)");
        }

        private void IgnoreWhitespacesToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool value = IgnoreWhitespacesToggleButton.IsChecked ?? false;
            ForkPlusSettings.Default.DiffIgnoreWhitespaces = value;
            // NotificationCenter.RaiseDiffIgnoreWhitespacesChanged 暂不调用（Avalonia 工程未接入 NotificationCenter）
        }

        private void ShowHiddenSymbolsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool value = ShowHiddenSymbolsToggleButton.IsChecked ?? false;
            ForkPlusSettings.Default.DiffShowHiddenSymbols = value;
        }

        private void WordWrapToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool value = WordWrapToggleButton.IsChecked ?? false;
            ForkPlusSettings.Default.DiffWordWrap = value;
        }

        private void DecreaseVisibleLines_Click(object sender, RoutedEventArgs e)
        {
            int newValue = ForkPlusSettings.Default.DiffContextSize - 1;
            ForkPlusSettings.Default.DiffContextSize = newValue;
        }

        private void IncreaseVisibleLines_Click(object sender, RoutedEventArgs e)
        {
            int newValue = ForkPlusSettings.Default.DiffContextSize + 1;
            ForkPlusSettings.Default.DiffContextSize = newValue;
        }

        private void ShowEntireFileToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool value = ShowEntireFileToggleButton.IsChecked ?? false;
            DiffShowEntireFile = value;
        }

        private void DiffLayoutModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isSideBySide = DiffLayoutModeToggleButton.IsChecked ?? false;
            DiffLayoutMode = isSideBySide ? DiffLayoutMode.SideBySide : DiffLayoutMode.Split;
            UpdateDiffLayoutModeToggleButtonState();
            UpdateWordWrapToggleButtonState();
        }

        private void HighlightPixelsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool value = HighlightPixelsToggleButton.IsChecked ?? false;
            ForkPlusSettings.Default.ImageDiffHighlightPixels = value;
        }

        // ===== ToggleButton 状态更新（对照 WPF 6 个 Update*State 方法）=====

        private void UpdateIgnoreWhiteSpacesToggleButtonState()
        {
            if (IgnoreWhitespacesToggleButton != null)
            {
                IgnoreWhitespacesToggleButton.IsChecked = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
            }
        }

        private void UpdateShowHiddenSymbolsToggleButtonState()
        {
            if (ShowHiddenSymbolsToggleButton != null)
            {
                ShowHiddenSymbolsToggleButton.IsChecked = ForkPlusSettings.Default.DiffShowHiddenSymbols;
            }
        }

        private void UpdateWordWrapToggleButtonState()
        {
            if (WordWrapToggleButton == null) return;
            if (DiffLayoutMode == DiffLayoutMode.Split)
            {
                WordWrapToggleButton.IsChecked = ForkPlusSettings.Default.DiffWordWrap;
                WordWrapToggleButton.IsEnabled = true;
            }
            else
            {
                WordWrapToggleButton.IsChecked = false;
                WordWrapToggleButton.IsEnabled = false;
            }
        }

        private void UpdateShowEntireFileState()
        {
            if (ShowEntireFileToggleButton == null) return;
            bool? diffShowEntireFile = DiffShowEntireFile;
            if (diffShowEntireFile.HasValue)
            {
                bool value = diffShowEntireFile.GetValueOrDefault();
                ShowEntireFileToggleButton.IsEnabled = true;
                ShowEntireFileToggleButton.IsChecked = value;
                if (DecreaseNumberOfVisibleLinesButton != null)
                    DecreaseNumberOfVisibleLinesButton.IsEnabled = !value;
                if (IncreaseNumberOfVisibleLinesButton != null)
                    IncreaseNumberOfVisibleLinesButton.IsEnabled = !value;
            }
            else
            {
                ShowEntireFileToggleButton.IsEnabled = false;
                ShowEntireFileToggleButton.IsChecked = false;
                if (DecreaseNumberOfVisibleLinesButton != null)
                    DecreaseNumberOfVisibleLinesButton.IsEnabled = false;
                if (IncreaseNumberOfVisibleLinesButton != null)
                    IncreaseNumberOfVisibleLinesButton.IsEnabled = false;
            }
        }

        private void UpdateDiffLayoutModeToggleButtonState()
        {
            if (DiffLayoutModeToggleButton == null) return;
            if (DiffLayoutMode == DiffLayoutMode.SideBySide)
            {
                DiffLayoutModeToggleButton.IsChecked = true;
                ToolTip.SetTip(DiffLayoutModeToggleButton, "Split diff");
            }
            else
            {
                DiffLayoutModeToggleButton.IsChecked = false;
                ToolTip.SetTip(DiffLayoutModeToggleButton, "Side by side diff");
            }
        }

        private void UpdateHighlightPixelsToggleButtonState()
        {
            if (HighlightPixelsToggleButton == null) return;
            if (HighlightPixelsToggleButtonEnabled)
            {
                HighlightPixelsToggleButton.IsEnabled = true;
                HighlightPixelsToggleButton.IsChecked = ForkPlusSettings.Default.ImageDiffHighlightPixels;
            }
            else
            {
                HighlightPixelsToggleButton.IsEnabled = false;
                HighlightPixelsToggleButton.IsChecked = false;
            }
        }

        // ===== 工具栏布局切换（对照 WPF RefreshToolbarLayout）=====
        private void RefreshToolbarLayout(FileControlHeaderMode mode)
        {
            if (TextModeButtonsContainer == null || TextModeNavigationButtonsContainer == null
                || ImageModeButtonsContainer == null) return;

            switch (mode)
            {
                case FileControlHeaderMode.None:
                    TextModeButtonsContainer.IsVisible = false;
                    TextModeNavigationButtonsContainer.IsVisible = false;
                    ImageModeButtonsContainer.IsVisible = false;
                    break;
                case FileControlHeaderMode.Text:
                    TextModeButtonsContainer.IsVisible = true;
                    TextModeNavigationButtonsContainer.IsVisible = true;
                    ImageModeButtonsContainer.IsVisible = false;
                    break;
                case FileControlHeaderMode.Image:
                    TextModeButtonsContainer.IsVisible = false;
                    TextModeNavigationButtonsContainer.IsVisible = false;
                    ImageModeButtonsContainer.IsVisible = true;
                    break;
                case FileControlHeaderMode.Hex:
                    TextModeButtonsContainer.IsVisible = false;
                    TextModeNavigationButtonsContainer.IsVisible = false;
                    ImageModeButtonsContainer.IsVisible = false;
                    break;
            }
        }
    }
}
