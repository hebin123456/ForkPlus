// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/GeneralUserControl.xaml.cs（357 行）：
//   - public partial class GeneralUserControl : UserControl
//   - 字段：ForkPlusDialogWindow _parentWindow / bool _initialized /
//     CodeEditorMinFontSize=10.0 / CodeEditorMaxFontSize=40.0
//   - Initialize(ForkPlusDialogWindow)：RefreshSourceDirs + 读 ForkPlusSettings 各项 +
//     InitializeDiffCodeEditor（构造 VisualPatch 塞 DiffCodeEditor）+
//     RefreshLanguageComboBoxItems + SelectUiLanguage
//   - AddSrcDirButton / RemoveSrcDirButton / EditSrcDirButton（context menu）
//   - TextBox TextChanged 写 ForkPlusSettings.Default + NotificationCenter.Raise*
//   - LanguageComboBox_SelectionChanged：写 UiLanguage + PreferencesLocalization.Apply
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ForkPlusDialogWindow _parentWindow == spike 用 object? 占位
//   2. WPF DiffCodeEditor（自定义控件）== spike 移除（spike 不渲染 diff 预览）
//   3. WPF InitializeDiffCodeEditor（PatchParser + VisualPatch）== spike 移除
//   4. WPF OpenDialog.SelectDirectory == spike 移除（spike 用 stub 注释）
//   5. WPF NotificationCenter.Current.Raise* == spike 移除
//   6. WPF PreferencesLocalization.Apply == spike 移除（spike 不做逻辑树递归翻译）
//   7. WPF RepositoryManager.Instance.SourceDirs == spike 保留（Core）
//   8. WPF SrcDirViewModel == spike 保留（已迁移）
//   9. WPF Consts.Git.References.SpaceCharacterReplacements == spike 保留（Core 常量）
//  10. WPF NumericIgnoreCaseStringComparer == spike 保留（Core）
//  11. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class GeneralUserControl : UserControl
    {
        // 对照 WPF: private static readonly double CodeEditorMinFontSize = 10.0;
        private static readonly double CodeEditorMinFontSize = 10.0;

        // 对照 WPF: private static readonly double CodeEditorMaxFontSize = 40.0;
        private static readonly double CodeEditorMaxFontSize = 40.0;

        // 对照 WPF: private ForkPlusDialogWindow _parentWindow;
        // spike 版：用 object? 占位
        private object? _parentWindow;

        // 对照 WPF: private bool _initialized;
        private bool _initialized;

        public GeneralUserControl()
        {
            InitializeComponent();
        }

        // 对照 WPF: public void Initialize(ForkPlusDialogWindow parentWindow)
        // spike 版：parentWindow 类型改为 object?（spike 占位）
        public void Initialize(object? parentWindow)
        {
            _parentWindow = parentWindow;
            RefreshSourceDirs();
            ShowDiffChangeMarksCheckBox.IsChecked = ForkPlusSettings.Default.DiffShowChangeMarks;
            CodeEditorFontSizeTextBox.Text = ForkPlusSettings.Default.CodeEditorFontSize.ToString();
            // spike: InitializeDiffCodeEditor(); // 移除（spike 不渲染 diff 预览）
            switch (ForkPlusSettings.Default.RevisionSortOrder)
            {
                case RevisionSortOrder.Date:
                    DateSortOrderRadioButton.IsChecked = true;
                    break;
                case RevisionSortOrder.Topo:
                    TopologicalSortOrderRadioButton.IsChecked = true;
                    break;
            }
            // spike: DiffCodeEditor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; // 移除
            FetchRemotesAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.FetchRemotesAutomatically;
            FetchAllTagsCheckBox.IsChecked = ForkPlusSettings.Default.FetchAllTags;
            UpdateRepoStatusAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.AutomaticStatusUpdateInterval > 0;
            UpdateSubmodulesAutomaticallyCheckBox.IsChecked = ForkPlusSettings.Default.UpdateSubmodulesOnCheckout;
            DisableSyntaxHighlightingCheckBox.IsChecked = ForkPlusSettings.Default.DisableSyntaxHighlighting;
            SpaceCharacterComboBox.ItemsSource = Consts.Git.References.SpaceCharacterReplacements;
            SpaceCharacterComboBox.SelectedValue = ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement;
            PushAutomaticallyOnCommitCheckBox.IsChecked = ForkPlusSettings.Default.PushAutomaticallyOnCommit;
            CompactBranchLabelsCheckBox.IsChecked = ForkPlusSettings.Default.CompactBranchLabels;
            // v3.0.4：Undo/Redo 开关，默认不使能
            // spike: UndoRedoEnabledCheckBox.IsChecked = ForkPlusSettings.Default.UndoRedoEnabled;
            RefreshLanguageComboBoxItems();
            SelectUiLanguage(ForkPlusSettings.Default.UiLanguage);
            _initialized = true;
        }

        // 对照 WPF: private void RefreshLanguageComboBoxItems()
        private void RefreshLanguageComboBoxItems()
        {
            LanguageComboBox.Items.Clear();
            foreach (PreferencesLocalization.LanguageOption language in PreferencesLocalization.GetLanguages())
            {
                LanguageComboBox.Items.Add(new ComboBoxItem
                {
                    Content = language.DisplayName,
                    Tag = language.Code
                });
            }
        }

        // 对照 WPF: private void SelectUiLanguage(string language)
        private void SelectUiLanguage(string language)
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items.OfType<ComboBoxItem>())
            {
                if ((item.Tag as string) == language)
                {
                    LanguageComboBox.SelectedItem = item;
                    return;
                }
            }
            LanguageComboBox.SelectedIndex = 0;
        }

        // 对照 WPF: private void RefreshSourceDirs()
        private void RefreshSourceDirs()
        {
            SrcDirsListBox.ItemsSource = RepositoryManager.Instance.SourceDirs
                .ToSortedArray((string x, string y) => NumericIgnoreCaseStringComparer.Comparer.Compare(x, y))
                .Map((string x) => new SrcDirViewModel(x));
            RemoveSrcDirButton.IsVisible = RepositoryManager.Instance.SourceDirs.Length > 1;
        }

        // 对照 WPF: private void InitializeDiffCodeEditor()
        // spike 版：移除（spike 不渲染 diff 预览）
        private void InitializeDiffCodeEditor()
        {
            // spike: WPF 用 PatchParser.Parse(input) + VisualPatch.CreateVisualPatch 塞 DiffCodeEditor.VisualPatch
            //        spike 版不渲染 diff 预览（DiffCodeEditor 控件未迁移）
        }

        // 对照 WPF: private void EditSrcDirButton_Click(object sender, RoutedEventArgs e)
        private void EditSrcDirButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: SrcDirViewModel dataContext })
            {
                EditSrcDir(dataContext.Path);
            }
        }

        // 对照 WPF: private void AddSrcDirButton_Click(object sender, RoutedEventArgs e)
        private void AddSrcDirButton_Click(object? sender, RoutedEventArgs e)
        {
            AddSrcDir();
        }

        // 对照 WPF: private void RemoveSrcDirButton_Click(object sender, RoutedEventArgs e)
        private void RemoveSrcDirButton_Click(object? sender, RoutedEventArgs e)
        {
            if (SrcDirsListBox.SelectedItem is SrcDirViewModel srcDirViewModel)
            {
                RemoveSrcDir(srcDirViewModel.Path);
            }
        }

        // 对照 WPF: private void SrcDirsListBox_SelectionChanged(...)
        private void SrcDirsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // spike: 仅占位（WPF 中处理 RemoveSrcDirButton.IsEnabled）
        }

        // 对照 WPF: private void AddSrcDir()
        // spike 版：移除 OpenDialog.SelectDirectory（spike 用 stub 注释）
        private void AddSrcDir()
        {
            // spike: string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
            //        if (OpenDialog.SelectDirectory(_parentWindow, PreferencesLocalization.Current("Select source directory"), initialDirectory, out var directoryPath))
            //        { RepositoryManager.Instance.AddSourceDir(directoryPath); RefreshSourceDirs(); }
            // spike 版：占位（spike 不弹目录选择对话框）
        }

        // 对照 WPF: private void EditSrcDir(string oldPath)
        // spike 版：移除 OpenDialog.SelectDirectory
        private void EditSrcDir(string oldPath)
        {
            // spike: if (OpenDialog.SelectDirectory(_parentWindow, ...))
            //        { RepositoryManager.Instance.ReplaceSourceDir(oldPath, newPath); RefreshSourceDirs(); }
            // spike 版：占位
        }

        // 对照 WPF: private void RemoveSrcDir(string path)
        private void RemoveSrcDir(string path)
        {
            RepositoryManager.Instance.RemoveSourceDir(path);
            RefreshSourceDirs();
        }

        // 对照 WPF: private void ShowDiffChangeMarksCheckBox_CheckedChanged(...)
        private void ShowDiffChangeMarksCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.DiffShowChangeMarks = ShowDiffChangeMarksCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: private void CodeEditorFontSizeTextBox_TextChanged(...)
        private void CodeEditorFontSizeTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_initialized) return;
            if (double.TryParse(CodeEditorFontSizeTextBox.Text, out double size))
            {
                size = Math.Max(CodeEditorMinFontSize, Math.Min(CodeEditorMaxFontSize, size));
                ForkPlusSettings.Default.CodeEditorFontSize = size;
            }
        }

        // 对照 WPF: sort order radio handlers
        private void SortOrderRadioButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            if (DateSortOrderRadioButton.IsChecked.GetValueOrDefault())
            {
                ForkPlusSettings.Default.RevisionSortOrder = RevisionSortOrder.Date;
            }
            else if (TopologicalSortOrderRadioButton.IsChecked.GetValueOrDefault())
            {
                ForkPlusSettings.Default.RevisionSortOrder = RevisionSortOrder.Topo;
            }
        }

        // 对照 WPF: FetchRemotesAutomaticallyCheckBox
        private void FetchRemotesAutomaticallyCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.FetchRemotesAutomatically = FetchRemotesAutomaticallyCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: FetchAllTagsCheckBox
        private void FetchAllTagsCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.FetchAllTags = FetchAllTagsCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: UpdateRepoStatusAutomaticallyCheckBox
        private void UpdateRepoStatusAutomaticallyCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            bool enabled = UpdateRepoStatusAutomaticallyCheckBox.IsChecked.GetValueOrDefault();
            ForkPlusSettings.Default.AutomaticStatusUpdateInterval = enabled ? 1 : 0;
        }

        // 对照 WPF: UpdateSubmodulesAutomaticallyCheckBox
        private void UpdateSubmodulesAutomaticallyCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.UpdateSubmodulesOnCheckout = UpdateSubmodulesAutomaticallyCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: DisableSyntaxHighlightingCheckBox
        private void DisableSyntaxHighlightingCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.DisableSyntaxHighlighting = DisableSyntaxHighlightingCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: SpaceCharacterComboBox_SelectionChanged
        private void SpaceCharacterComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement = SpaceCharacterComboBox.SelectedValue as string;
        }

        // 对照 WPF: PushAutomaticallyOnCommitCheckBox
        private void PushAutomaticallyOnCommitCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.PushAutomaticallyOnCommit = PushAutomaticallyOnCommitCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: CompactBranchLabelsCheckBox
        private void CompactBranchLabelsCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.CompactBranchLabels = CompactBranchLabelsCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: UndoRedoEnabledCheckBox（v3.0.4）
        // spike: 移除（axaml 中未放此 CheckBox，spike 不接入）

        // 对照 WPF: LanguageComboBox_SelectionChanged
        private void LanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                ForkPlusSettings.Default.UiLanguage = code;
                // spike: PreferencesLocalization.Apply(this, code); // 移除（spike 不做逻辑树递归翻译）
            }
        }
    }
}
