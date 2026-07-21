using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.33b：Avalonia 版 GitFlowInitWindow（对照 WPF GitFlowInitWindow.xaml.cs 185 行）。
    //
    // 对照 WPF：
    //   - public partial class GitFlowInitWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule
    //   - 构造函数 (GitModule gitModule)
    //   - IsSubmitAllowed: 5 个非空检查 + 2 个 Validate + 4 个 ValidateGitFlow
    //   - GetCommandPreview override: "git flow init"（Master/Develop 非空时）
    //   - OnSubmit: DisableEditableControls + SetStatus(InProgress) + JobQueue.Add
    //     → InitGitFlowGitCommand().Execute(_gitModule, gitFlowSettings, monitor) → Dispatcher.Async(Close(result))
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. MainWindow.ActiveRepositoryUserControl?.RepositoryData?.References?.LocalBranches 查找 "main"
    //      → 注入 Func<string>? mainBranchProvider（仅查 "main" 分支）
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 6 个 TextBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   7. WPF 6 个独立 TextChanged handler → Avalonia 单一共享 TextBox_TextChanged（与 InitGitMmRepositoryWindow 一致）
    public partial class GitFlowInitWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Func<string> _mainBranchProvider;

        public GitFlowInitWindow(GitModule gitModule, Func<string> mainBranchProvider = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _mainBranchProvider = mainBranchProvider;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Initialize Git Flow");
            DialogDescription = Translate("Start using Git Flow by initializing it inside an existing git repository");
            SubmitButtonTitle = Translate("Initialize Git Flow");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: MasterBranchTextBox.Text = MainBranch() ?? "master";
            // WPF MainBranch() 在 LocalBranches 中找 "main"，spike 版注入 Func<string>? provider
            MasterBranchTextBox.Text = _mainBranchProvider?.Invoke() ?? "master";
            DevelopBranchTextBox.Text = "develop";
            FeaturePrefixTextBox.Text = "feature/";
            ReleasePrefixTextBox.Text = "release/";
            HotfixPrefixTextBox.Text = "hotfix/";

            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                if (string.IsNullOrEmpty(MasterBranchTextBox.Text))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Production branch name can't be empty"));
                    return false;
                }
                if (string.IsNullOrEmpty(DevelopBranchTextBox.Text))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Development branch name can't be empty"));
                    return false;
                }
                if (string.IsNullOrEmpty(FeaturePrefixTextBox.Text))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Feature branch prefix can't be empty"));
                    return false;
                }
                if (string.IsNullOrEmpty(ReleasePrefixTextBox.Text))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Release branch prefix can't be empty"));
                    return false;
                }
                if (string.IsNullOrEmpty(HotfixPrefixTextBox.Text))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Hotfix branch prefix can't be empty"));
                    return false;
                }
                string masterError = ReferenceNameValidator.Validate(MasterBranchTextBox.Text);
                if (masterError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, masterError);
                    return false;
                }
                string developError = ReferenceNameValidator.Validate(DevelopBranchTextBox.Text);
                if (developError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, developError);
                    return false;
                }
                string featureError = ReferenceNameValidator.ValidateGitFlow(FeaturePrefixTextBox.Text);
                if (featureError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, featureError);
                    return false;
                }
                string releaseError = ReferenceNameValidator.ValidateGitFlow(ReleasePrefixTextBox.Text);
                if (releaseError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, releaseError);
                    return false;
                }
                string hotfixError = ReferenceNameValidator.ValidateGitFlow(HotfixPrefixTextBox.Text);
                if (hotfixError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, hotfixError);
                    return false;
                }
                string versionTagError = ReferenceNameValidator.ValidateGitFlow(VersionTagPrefixTextBox.Text);
                if (versionTagError != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, versionTagError);
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (string.IsNullOrEmpty(MasterBranchTextBox.Text) || string.IsNullOrEmpty(DevelopBranchTextBox.Text))
            {
                return null;
            }
            return "git flow init";
        }

        // spike 版：手动刷新命令预览文本（基类无 RefreshCommandPreview）
        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
            CommandPreviewTextBox.IsVisible = !string.IsNullOrEmpty(preview);
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (!IsSubmitAllowed)
            {
                return;
            }

            GitFlowSettings gitFlowSettings = new GitFlowSettings(
                MasterBranchTextBox.Text,
                DevelopBranchTextBox.Text,
                FeaturePrefixTextBox.Text,
                ReleasePrefixTextBox.Text,
                HotfixPrefixTextBox.Text,
                VersionTagPrefixTextBox.Text);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Initializing Git Flow..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new InitGitFlowGitCommand().Execute(_gitModule, gitFlowSettings, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    Close(result);
                });
            });
        }

        // 对照 WPF: 6 个 TextBox 的 TextChanged 事件
        // WPF 每个文本框单独有 handler（MasterBranchTextBox_TextChanged / DevelopBranchTextBox_TextChanged 等）
        // Avalonia spike 用单一共享 handler（与 InitGitMmRepositoryWindow 一致）
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用 6 个可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            MasterBranchTextBox.IsEnabled = false;
            DevelopBranchTextBox.IsEnabled = false;
            FeaturePrefixTextBox.IsEnabled = false;
            ReleasePrefixTextBox.IsEnabled = false;
            HotfixPrefixTextBox.IsEnabled = false;
            VersionTagPrefixTextBox.IsEnabled = false;
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
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
