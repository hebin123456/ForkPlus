using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.16b：Avalonia 版 GitMmStartWindow（真实迁移版，对照 WPF GitMmStartWindow.xaml.cs）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitMmStartWindow.xaml.cs（289 行）：
    //   - public partial class GitMmStartWindow : ForkPlusDialogWindow
    //   - private readonly GitMmSubrepoItem[] _subrepos
    //   - private readonly HashSet<string> _selectedSubrepoPaths
    //   - public string[] StartArgs { get; private set; }
    //   - protected override bool IsSubmitAllowed
    //     * BranchName 不空 && (AllSubrepos || _selectedSubrepoPaths.Count > 0) && base.IsSubmitAllowed
    //   - 构造函数 (subrepos, selectedSubrepo):
    //     * _subrepos = subrepos?.ToArray() ?? []
    //     * if (selectedSubrepo != null) _selectedSubrepoPaths.Add(selectedSubrepo.Path)
    //     * DialogTitle/Description/SubmitButtonTitle
    //     * PreferencesLocalization.Apply  // spike 省略
    //     * RestoreDialogOptions + InitializeCommandPreviewHandlers + RefreshSubreposButton + UpdateSubmitButton + RefreshCommandPreview
    //   - OnSubmit: StartArgs = CreateArgs(); SaveDialogOptions(); base.OnSubmit()
    //   - CreateArgs: "start" + BranchName + "-j" + jobs + "-g" + grepMode(可选) + "--all" 或 subrepo names + 各 flag
    //   - SubreposDropDownButton_Click: 动态构造 ContextMenu 含 MenuItem(IsCheckable=true, StaysOpenOnClick=true)
    //   - RestoreDialogOptions/SaveDialogOptions: 读写 GitMm.DialogOptions 的 start.* 选项 + StartBranch
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Apply 不存在 → cs 中显式 Translate
    //   3. Avalonia 11 MenuItem 用 ToggleType=CheckBox + IsChecked 替代 WPF IsCheckable
    //   4. Avalonia 11 MenuItem 无 StaysOpenOnClick 等价属性；spike 版用 Click 事件 + e.Handled
    //      防止菜单关闭（实测在 Avalonia 11.3+ 中 ToggleType=CheckBox 默认不关闭）
    //   5. Dispatcher.BeginInvoke → Dispatcher.UIThread.Post
    public partial class GitMmStartWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitMmSubrepoItem[] _subrepos;
        private readonly HashSet<string> _selectedSubrepoPaths = new HashSet<string>();

        // 用户提交的 git mm start 命令参数（OnSubmit 时填充）
        public string[] StartArgs { get; private set; }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BranchNameTextBox.Text))
                {
                    return false;
                }
                if (!AllSubreposCheckBox.IsChecked.GetValueOrDefault() && _selectedSubrepoPaths.Count == 0)
                {
                    return false;
                }
                return base.IsSubmitAllowed;
            }
        }

        public GitMmStartWindow(IEnumerable<GitMmSubrepoItem>? subrepos, GitMmSubrepoItem? selectedSubrepo)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _subrepos = subrepos?.ToArray() ?? Array.Empty<GitMmSubrepoItem>();
            if (selectedSubrepo != null)
            {
                _selectedSubrepoPaths.Add(selectedSubrepo.Path);
            }

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Translate("Start git mm");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Start development branch for git mm sub repositories");
            SubmitButtonTitle = Translate("Start");

            // 对照 WPF: RestoreDialogOptions + InitializeCommandPreviewHandlers + RefreshSubreposButton + UpdateSubmitButton + RefreshCommandPreview
            RestoreDialogOptions();
            InitializeCommandPreviewHandlers();
            RefreshSubreposButton();
            UpdateSubmitButton();
            StartArgs = CreateArgs();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            StartArgs = CreateArgs();
            SaveDialogOptions();
            CloseWithOk();
        }

        // 对照 WPF: private string[] CreateArgs()
        private string[] CreateArgs()
        {
            List<string> args = new List<string> { "start", BranchNameTextBox.Text?.Trim() ?? "" };
            args.Add("-j");
            args.Add(SelectedJobs().ToString());
            string grepMode = SelectedComboBoxText(GrepModeComboBox);
            if (!string.IsNullOrWhiteSpace(grepMode) && grepMode != "mixed")
            {
                args.Add("-g");
                args.Add(grepMode);
            }
            if (AllSubreposCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--all");
            }
            else
            {
                args.AddRange(_subrepos
                    .Where(subrepo => _selectedSubrepoPaths.Contains(subrepo.Path))
                    .Select(subrepo => subrepo.Name));
            }
            if (AllowTagCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--allow-tag");
            }
            if (AllowCommitCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--allow-commit");
            }
            if (AllowNoTrackCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--allow-no-track");
            }
            if (HeadCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--head");
            }
            return args.ToArray();
        }

        private int SelectedJobs()
        {
            if (JobsComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int jobs))
            {
                return Math.Max(1, Math.Min(10, jobs));
            }
            return 8;
        }

        private static string SelectedComboBoxText(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        }

        // 对照 WPF: private void RestoreDialogOptions()
        private void RestoreDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            BranchNameTextBox.Text = !string.IsNullOrWhiteSpace(settings.StartBranch) ? settings.StartBranch : "develop";
            SelectComboBoxItem(JobsComboBox, settings.GetDialogOption("start.jobs", "8"));
            SelectComboBoxItem(GrepModeComboBox, settings.GetDialogOption("start.grepMode", "mixed"));
            AllSubreposCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allSubrepos", defaultValue: true);
            AllowTagCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowTag");
            AllowCommitCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowCommit");
            AllowNoTrackCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.allowNoTrack");
            HeadCheckBox.IsChecked = IsDialogOptionChecked(settings, "start.head");
        }

        private static void SelectComboBoxItem(ComboBox comboBox, string? value)
        {
            foreach (ComboBoxItem item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key, bool defaultValue = false)
        {
            return string.Equals(settings.GetDialogOption(key, defaultValue ? "true" : "false"), "true", StringComparison.OrdinalIgnoreCase);
        }

        // 对照 WPF: SubreposDropDownButton_Click
        // Avalonia 11: MenuItem ToggleType=CheckBox + IsChecked 替代 WPF IsCheckable
        private void SubreposDropDownButton_Click(object? sender, RoutedEventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();
            foreach (GitMmSubrepoItem subrepo in _subrepos)
            {
                MenuItem menuItem = new MenuItem
                {
                    Header = subrepo.Name,
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = _selectedSubrepoPaths.Contains(subrepo.Path)
                };
                string subrepoPath = subrepo.Path;
                menuItem.Click += delegate
                {
                    if (menuItem.IsChecked)
                    {
                        _selectedSubrepoPaths.Add(subrepoPath);
                    }
                    else
                    {
                        _selectedSubrepoPaths.Remove(subrepoPath);
                    }
                    RefreshSubreposButton();
                    UpdateSubmitButton();
                    RefreshCommandPreview();
                };
                contextMenu.Items.Add(menuItem);
            }
            SubreposDropDownButton.ContextMenu = contextMenu;
            contextMenu.PlacementTarget = SubreposDropDownButton;
            contextMenu.Open();
        }

        // 对照 WPF: AllSubreposCheckBox_Changed
        public void AllSubreposCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshSubreposButton();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: BranchNameTextBox_TextChanged
        public void BranchNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: private void RefreshSubreposButton()
        private void RefreshSubreposButton()
        {
            bool allSubrepos = AllSubreposCheckBox.IsChecked.GetValueOrDefault();
            SubreposDropDownButton.IsEnabled = !allSubrepos;
            if (allSubrepos)
            {
                SubreposDropDownButton.Content = Translate("All sub repositories");
                return;
            }
            if (_selectedSubrepoPaths.Count == 0)
            {
                SubreposDropDownButton.Content = Translate("Select sub repositories");
                return;
            }
            SubreposDropDownButton.Content = string.Join(", ", _subrepos
                .Where(subrepo => _selectedSubrepoPaths.Contains(subrepo.Path))
                .Select(subrepo => subrepo.Name));
        }

        // 对照 WPF: private void InitializeCommandPreviewHandlers()
        private void InitializeCommandPreviewHandlers()
        {
            JobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
            GrepModeComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
            CheckBox[] checkBoxes = new CheckBox[]
            {
                AllowTagCheckBox,
                AllowCommitCheckBox,
                AllowNoTrackCheckBox,
                HeadCheckBox
            };
            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.IsCheckedChanged += delegate { RefreshCommandPreview(); };
            }
        }

        // 对照 WPF: private void RefreshCommandPreview()
        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBlock != null)
            {
                string cmd = GitMmCommandPreviewHelper.Format(CreateArgs());
                CommandPreviewTextBlock.Text = cmd;
                ToolTip.SetTip(CommandPreviewTextBlock, cmd);
            }
        }

        // 对照 WPF: private void SaveDialogOptions()
        private void SaveDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
            dialogOptions["start.jobs"] = SelectedJobs().ToString();
            dialogOptions["start.grepMode"] = SelectedComboBoxText(GrepModeComboBox) ?? "mixed";
            SaveCheckBox(dialogOptions, "start.allSubrepos", AllSubreposCheckBox);
            SaveCheckBox(dialogOptions, "start.allowTag", AllowTagCheckBox);
            SaveCheckBox(dialogOptions, "start.allowCommit", AllowCommitCheckBox);
            SaveCheckBox(dialogOptions, "start.allowNoTrack", AllowNoTrackCheckBox);
            SaveCheckBox(dialogOptions, "start.head", HeadCheckBox);
            ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
                settings.Workspaces,
                settings.ActiveWorkspace,
                settings.ActiveSubrepo,
                settings.ActiveSubrepos,
                settings.SubrepoOrders,
                settings.VisibleSubrepos,
                settings.CommandOutputCollapsed,
                settings.CommandOutputHeight,
                settings.CommandHistory,
                settings.UploadLinks,
                settings.UploadLinksByWorkspace,
                settings.SyncJobs,
                BranchNameTextBox.Text?.Trim() ?? "",
                settings.InitUrl,
                settings.InitManifest,
                settings.InitBranch,
                settings.InitGroup,
                dialogOptions);
            ForkPlusSettings.Default.Save();
        }

        private static void SaveCheckBox(Dictionary<string, string> dialogOptions, string key, CheckBox checkBox)
        {
            dialogOptions[key] = checkBox.IsChecked.GetValueOrDefault() ? "true" : "false";
        }

        // 对照 WPF: private static string Translate(string text)
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
