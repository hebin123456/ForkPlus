using System;
using System.Collections.Generic;
using Avalonia.Controls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.14b：Avalonia 版 GitMmUploadWindow（真实迁移版，对照 WPF GitMmUploadWindow.xaml.cs）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitMmUploadWindow.xaml.cs（185 行）：
    //   - public partial class GitMmUploadWindow : ForkPlusDialogWindow
    //   - public string[] UploadArgs { get; private set; }
    //   - 构造函数 (workspacePath):
    //     * DialogTitle = Translate("Upload git mm")
    //     * DialogDescription = Translate("Upload git mm changes for review")
    //     * SubmitButtonTitle = Translate("Upload")
    //     * WorkspacePathTextBlock.Text = workspacePath
    //     * ForceUploadWarningImage.ToolTip = Translate("Force upload even if git mm reports safety checks.")
    //     * PreferencesLocalization.Apply(this, UiLanguage)  // spike 版省略
    //     * RestoreDialogOptions() — 从 ForkPlusSettings.GitMm.DialogOptions 读 upload.* 选项
    //     * InitializeCommandPreviewHandlers() — TextBox.TextChanged + CheckBox.Checked/Unchecked
    //     * UploadArgs = CreateArgs()
    //     * RefreshCommandPreview()
    //   - OnSubmit: UploadArgs = CreateArgs(); SaveDialogOptions(); base.OnSubmit()
    //   - CreateArgs: 拼 "upload" + 各 flag 参数
    //   - RestoreDialogOptions / SaveDialogOptions: 读写 ForkPlusSettings.GitMm.DialogOptions
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter(Footer) 注入 Footer 引用
    //   2. PreferencesLocalization.Apply 不存在 → cs 中显式 Translate 关键文本
    //   3. ToolTip 用 ToolTip.SetTip(control, text)
    //   4. CheckBox.IsChecked 是 bool?，用 GetValueOrDefault()
    public partial class GitMmUploadWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 用户提交的 git mm upload 命令参数（OnSubmit 时填充）
        public string[] UploadArgs { get; private set; }

        public GitMmUploadWindow(string workspacePath)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Translate("Upload git mm");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Upload git mm changes for review");
            SubmitButtonTitle = Translate("Upload");

            // 对照 WPF: WorkspacePathTextBlock.Text = workspacePath ?? ""
            WorkspacePathTextBlock.Text = workspacePath ?? "";
            ToolTip.SetTip(WorkspacePathTextBlock, WorkspacePathTextBlock.Text);

            // 对照 WPF: ForceUploadWarningImage.ToolTip
            string forceWarn = Translate("Force upload even if git mm reports safety checks.");
            ToolTip.SetTip(ForceUploadWarningImage, forceWarn);

            // 对照 WPF: RestoreDialogOptions + InitializeCommandPreviewHandlers
            RestoreDialogOptions();
            InitializeCommandPreviewHandlers();
            UploadArgs = CreateArgs();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            UploadArgs = CreateArgs();
            SaveDialogOptions();
            CloseWithOk();
        }

        // 对照 WPF: private string[] CreateArgs()
        private string[] CreateArgs()
        {
            List<string> args = new List<string> { "upload" };
            if (ForceUploadCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("-f");
            }
            if (AssumeYesCheckBox.IsChecked.GetValueOrDefault())
            {
                args.Add("-y");
            }
            string title = CommitTitleTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                args.Add("-T");
                args.Add(title);
            }
            AddTextArg(args, "--topic", TopicTextBox.Text);
            AddTextArg(args, "-R", ReviewersTextBox.Text);
            AddTextArg(args, "--cc", CcTextBox.Text);
            AddFlag(args, CurrentBranchOnlyCheckBox, "--cbr");
            AddFlag(args, HeadCheckBox, "--head");
            AddFlag(args, ReadyCheckBox, "--ready");
            AddFlag(args, WipCheckBox, "--wip");
            AddFlag(args, NoUpdateManifestCheckBox, "-N");
            return args.ToArray();
        }

        private static void AddTextArg(List<string> args, string flag, string value)
        {
            string trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                args.Add(flag);
                args.Add(trimmedValue);
            }
        }

        private static void AddFlag(List<string> args, CheckBox checkBox, string flag)
        {
            if (checkBox.IsChecked.GetValueOrDefault())
            {
                args.Add(flag);
            }
        }

        // 对照 WPF: private void RestoreDialogOptions()
        private void RestoreDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            CommitTitleTextBox.Text = settings.GetDialogOption("upload.title");
            TopicTextBox.Text = settings.GetDialogOption("upload.topic");
            ReviewersTextBox.Text = settings.GetDialogOption("upload.reviewers");
            CcTextBox.Text = settings.GetDialogOption("upload.cc");
            ForceUploadCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.force");
            AssumeYesCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.assumeYes", defaultValue: true);
            CurrentBranchOnlyCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.currentBranchOnly");
            HeadCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.head");
            ReadyCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.ready");
            WipCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.wip");
            NoUpdateManifestCheckBox.IsChecked = IsDialogOptionChecked(settings, "upload.noUpdateManifest");
        }

        private static bool IsDialogOptionChecked(ForkPlusSettings.GitMmSettings settings, string key, bool defaultValue = false)
        {
            return string.Equals(settings.GetDialogOption(key, defaultValue ? "true" : "false"), "true", StringComparison.OrdinalIgnoreCase);
        }

        // 对照 WPF: private void InitializeCommandPreviewHandlers()
        private void InitializeCommandPreviewHandlers()
        {
            CommitTitleTextBox.TextChanged += delegate { RefreshCommandPreview(); };
            TopicTextBox.TextChanged += delegate { RefreshCommandPreview(); };
            ReviewersTextBox.TextChanged += delegate { RefreshCommandPreview(); };
            CcTextBox.TextChanged += delegate { RefreshCommandPreview(); };
            CheckBox[] checkBoxes = new CheckBox[]
            {
                ForceUploadCheckBox,
                AssumeYesCheckBox,
                CurrentBranchOnlyCheckBox,
                HeadCheckBox,
                ReadyCheckBox,
                WipCheckBox,
                NoUpdateManifestCheckBox
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

        // 对照 WPF: private void SaveDialogOptions()
        private void SaveDialogOptions()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
            dialogOptions["upload.title"] = CommitTitleTextBox.Text?.Trim() ?? "";
            dialogOptions["upload.topic"] = TopicTextBox.Text?.Trim() ?? "";
            dialogOptions["upload.reviewers"] = ReviewersTextBox.Text?.Trim() ?? "";
            dialogOptions["upload.cc"] = CcTextBox.Text?.Trim() ?? "";
            SaveCheckBox(dialogOptions, "upload.force", ForceUploadCheckBox);
            SaveCheckBox(dialogOptions, "upload.assumeYes", AssumeYesCheckBox);
            SaveCheckBox(dialogOptions, "upload.currentBranchOnly", CurrentBranchOnlyCheckBox);
            SaveCheckBox(dialogOptions, "upload.head", HeadCheckBox);
            SaveCheckBox(dialogOptions, "upload.ready", ReadyCheckBox);
            SaveCheckBox(dialogOptions, "upload.wip", WipCheckBox);
            SaveCheckBox(dialogOptions, "upload.noUpdateManifest", NoUpdateManifestCheckBox);
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
