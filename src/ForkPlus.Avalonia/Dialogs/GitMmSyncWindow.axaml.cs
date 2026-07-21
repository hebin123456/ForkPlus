using System;
using System.Collections.Generic;
using Avalonia.Controls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.15b：Avalonia 版 GitMmSyncWindow（真实迁移版，对照 WPF GitMmSyncWindow.xaml.cs）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitMmSyncWindow.xaml.cs（228 行）：
    //   - public partial class GitMmSyncWindow : ForkPlusDialogWindow
    //   - public string[] SyncArgs { get; private set; }
    //   - public int CheckoutJobs { get; private set; } = 4
    //   - 构造函数 (workspacePath):
    //     * DialogTitle = Translate("Sync git mm")
    //     * DialogDescription = Translate("Sync git mm workspace")
    //     * SubmitButtonTitle = Translate("Sync")
    //     * WorkspacePathTextBlock.Text = workspacePath
    //     * ForceSyncWarningImage.ToolTip = Translate("Discard local sync state and force git mm to resync projects.")
    //     * PreferencesLocalization.Apply(this, UiLanguage)  // spike 版省略
    //     * SelectCheckoutJobs(ForkPlusSettings.Default.GitMm.SyncJobs)
    //     * SelectJobs(FetchJobsComboBox, GetDialogOption("sync.fetchJobs"), defaultValue: 8)
    //     * RestoreDialogOptions() — 从 GitMm.DialogOptions 读 sync.* 选项
    //     * InitializeCommandPreviewHandlers() — ComboBox.SelectionChanged + CheckBox.Checked/Unchecked
    //     * SyncArgs = CreateArgs()
    //     * RefreshCommandPreview()
    //   - OnSubmit: CheckoutJobs = SelectedCheckoutJobs(); SyncArgs = CreateArgs();
    //               SaveDialogOptions(); base.OnSubmit()
    //   - CreateArgs: 拼 "sync" + "-J" + checkoutJobs + "-j" + fetchJobs + 各 flag
    //   - RestoreDialogOptions / SaveDialogOptions: 读写 GitMm.DialogOptions
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter(Footer) 注入 Footer 引用
    //   2. PreferencesLocalization.Apply 不存在 → cs 中显式 Translate 关键文本
    //   3. ToolTip 用 ToolTip.SetTip(control, text)
    //   4. CheckBox.IsChecked 是 bool?，用 GetValueOrDefault()
    //   5. CheckBox 事件用 IsCheckedChanged（不是 Checked/Unchecked）
    //   6. ComboBoxItem.Content 在 Avalonia 是 object?，ToString() 兜底
    public partial class GitMmSyncWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 用户提交的 git mm sync 命令参数（OnSubmit 时填充）
        public string[] SyncArgs { get; private set; }

        // 用户选定的 checkout 并发数（OnSubmit 时填充，默认 4）
        public int CheckoutJobs { get; private set; } = 4;

        public GitMmSyncWindow(string workspacePath)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Translate("Sync git mm");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Sync git mm workspace");
            SubmitButtonTitle = Translate("Sync");

            // 对照 WPF: WorkspacePathTextBlock.Text = workspacePath ?? ""
            WorkspacePathTextBlock.Text = workspacePath ?? "";
            ToolTip.SetTip(WorkspacePathTextBlock, WorkspacePathTextBlock.Text);

            // 对照 WPF: ForceSyncWarningImage.ToolTip
            string forceWarn = Translate("Discard local sync state and force git mm to resync projects.");
            ToolTip.SetTip(ForceSyncWarningImage, forceWarn);

            // 对照 WPF: SelectCheckoutJobs(ForkPlusSettings.Default.GitMm.SyncJobs)
            SelectCheckoutJobs(ForkPlusSettings.Default.GitMm.SyncJobs);
            // 对照 WPF: SelectJobs(FetchJobsComboBox, GetDialogOption("sync.fetchJobs"), defaultValue: 8)
            SelectJobs(FetchJobsComboBox, ForkPlusSettings.Default.GitMm.GetDialogOption("sync.fetchJobs"), defaultValue: 8);

            // 对照 WPF: RestoreDialogOptions + InitializeCommandPreviewHandlers
            RestoreDialogOptions();
            InitializeCommandPreviewHandlers();
            SyncArgs = CreateArgs();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            CheckoutJobs = SelectedCheckoutJobs();
            SyncArgs = CreateArgs();
            SaveDialogOptions();
            CloseWithOk();
        }

        // 对照 WPF: private string[] CreateArgs()
        private string[] CreateArgs()
        {
            List<string> args = new List<string> { "sync" };
            args.Add("-J");
            args.Add(SelectedCheckoutJobs().ToString());
            args.Add("-j");
            args.Add(SelectedFetchJobs().ToString());
            if (ForceSyncCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--force-sync");
            }
            if (DetachCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("-d");
            }
            if (UpdateManifestCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("--update-manifest");
            }
            AddFlag(args, LocalOnlyCheckBox, "-l");
            AddFlag(args, NetworkOnlyCheckBox, "-n");
            AddFlag(args, FailFastCheckBox, "--fail-fast");
            AddFlag(args, AllBranchesCheckBox, "-a");
            AddFlag(args, TagsCheckBox, "--tags");
            AddFlag(args, FetchSubmodulesCheckBox, "--fetch-submodules");
            AddFlag(args, ForceCheckoutCheckBox, "--force-checkout");
            AddFlag(args, ForceFetchCheckBox, "--force-fetch");
            AddFlag(args, ForceRemoveDirtyCheckBox, "--force-remove-dirty");
            return args.ToArray();
        }

        private static void AddFlag(List<string> args, CheckBox checkBox, string flag)
        {
            if (checkBox.IsChecked.GetValueOrDefault())
            {
                args.Add(flag);
            }
        }

        // 对照 WPF: SelectCheckoutJobs / SelectJobs / SelectedCheckoutJobs / SelectedFetchJobs / SelectedJobs / ClampCheckoutJobs
        private void SelectCheckoutJobs(string value)
        {
            SelectJobs(CheckoutJobsComboBox, value, defaultValue: 4);
        }

        private static void SelectJobs(ComboBox comboBox, string? value, int defaultValue)
        {
            int jobs = defaultValue;
            if (int.TryParse(value, out int parsedJobs))
            {
                jobs = ClampCheckoutJobs(parsedJobs);
            }
            comboBox.SelectedIndex = jobs - 1;
        }

        private int SelectedCheckoutJobs()
        {
            return SelectedJobs(CheckoutJobsComboBox, defaultValue: 4);
        }

        private int SelectedFetchJobs()
        {
            return SelectedJobs(FetchJobsComboBox, defaultValue: 8);
        }

        private static int SelectedJobs(ComboBox comboBox, int defaultValue)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int jobs))
            {
                return ClampCheckoutJobs(jobs);
            }
            return defaultValue;
        }

        private static int ClampCheckoutJobs(int jobs)
        {
            return Math.Max(1, Math.Min(10, jobs));
        }

        // 对照 WPF: private void RestoreDialogOptions()
        private void RestoreDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            ForceSyncCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceSync");
            DetachCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.detach");
            UpdateManifestCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.updateManifest");
            LocalOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.localOnly");
            NetworkOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.networkOnly");
            FailFastCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.failFast");
            AllBranchesCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.allBranches");
            TagsCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.tags");
            FetchSubmodulesCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.fetchSubmodules");
            ForceCheckoutCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceCheckout");
            ForceFetchCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceFetch");
            ForceRemoveDirtyCheckBox.IsChecked = IsDialogOptionChecked(settings, "sync.forceRemoveDirty");
        }

        private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key)
        {
            return string.Equals(settings.GetDialogOption(key), "true", StringComparison.OrdinalIgnoreCase);
        }

        // 对照 WPF: private void SaveDialogOptions()
        private void SaveDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
            dialogOptions["sync.fetchJobs"] = SelectedFetchJobs().ToString();
            SaveCheckBox(dialogOptions, "sync.forceSync", ForceSyncCheckBox);
            SaveCheckBox(dialogOptions, "sync.detach", DetachCheckBox);
            SaveCheckBox(dialogOptions, "sync.updateManifest", UpdateManifestCheckBox);
            SaveCheckBox(dialogOptions, "sync.localOnly", LocalOnlyCheckBox);
            SaveCheckBox(dialogOptions, "sync.networkOnly", NetworkOnlyCheckBox);
            SaveCheckBox(dialogOptions, "sync.failFast", FailFastCheckBox);
            SaveCheckBox(dialogOptions, "sync.allBranches", AllBranchesCheckBox);
            SaveCheckBox(dialogOptions, "sync.tags", TagsCheckBox);
            SaveCheckBox(dialogOptions, "sync.fetchSubmodules", FetchSubmodulesCheckBox);
            SaveCheckBox(dialogOptions, "sync.forceCheckout", ForceCheckoutCheckBox);
            SaveCheckBox(dialogOptions, "sync.forceFetch", ForceFetchCheckBox);
            SaveCheckBox(dialogOptions, "sync.forceRemoveDirty", ForceRemoveDirtyCheckBox);
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
                CheckoutJobs.ToString(),
                settings.StartBranch,
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

        // 对照 WPF: private void InitializeCommandPreviewHandlers()
        private void InitializeCommandPreviewHandlers()
        {
            CheckoutJobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
            FetchJobsComboBox.SelectionChanged += delegate { RefreshCommandPreview(); };
            CheckBox[] checkBoxes = new CheckBox[]
            {
                ForceSyncCheckBox,
                DetachCheckBox,
                UpdateManifestCheckBox,
                LocalOnlyCheckBox,
                NetworkOnlyCheckBox,
                FailFastCheckBox,
                AllBranchesCheckBox,
                TagsCheckBox,
                FetchSubmodulesCheckBox,
                ForceCheckoutCheckBox,
                ForceFetchCheckBox,
                ForceRemoveDirtyCheckBox
            };
            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.IsCheckedChanged += delegate { RefreshCommandPreview(); };
            }
        }

        // 对照 WPF: private void RefreshCommandPreview()
        // spike 版基类不提供 RefreshCommandPreview，子类自己维护 CommandPreviewTextBlock.Text
        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBlock != null)
            {
                string cmd = GitMmCommandPreviewHelper.Format(CreateArgs());
                CommandPreviewTextBlock.Text = cmd;
                // 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
                ToolTip.SetTip(CommandPreviewTextBlock, cmd);
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
        // Phase 0.4b 完成后，ILocalizationService 已注册到 ServiceLocator
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
