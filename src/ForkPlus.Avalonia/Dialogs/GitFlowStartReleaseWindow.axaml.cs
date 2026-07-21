using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 GitFlowStartReleaseWindow（对照 WPF GitFlowStartReleaseWindow.xaml.cs 121 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitFlowStartReleaseWindow.xaml.cs：
    //   - public partial class GitFlowStartReleaseWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / LocalBranch[] _localBranches / GitFlowSettings _gitFlowSettings（无 UnfinishedBranchName）
    //   - 构造函数 (GitModule gitModule) → 从 MainWindow.ActiveRepositoryUserControl.RepositoryData 取 GitFlowSettings + LocalBranches
    //   - IsSubmitAllowed: 未选分支 false / 名字空 false / ValidateGitFlow 失败 Warning / 重复分支 Warning
    //   - GetCommandPreview: "git flow release start {name} {baseBranch.Name}"
    //   - OnSubmit: JobQueue.Add(StartGitFlowReleaseGitCommand().Execute) → Close(result)
    //   - Refresh: 选 DevelopBranch ?? active，回填 ReleasePrefix
    //
    // Avalonia 版差异（spike）：同 GitFlowStartFeatureWindow（构造函数注入、Task.Run、手动禁用控件）。
    public partial class GitFlowStartReleaseWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private LocalBranch[] _localBranches;
        private GitFlowSettings _gitFlowSettings;

        // 构造函数签名与 WPF 不同：注入 GitFlowSettings + LocalBranch[] 替代 MainWindow 依赖
        public GitFlowStartReleaseWindow(
            GitModule gitModule,
            GitFlowSettings gitFlowSettings,
            LocalBranch[] localBranches)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _gitFlowSettings = gitFlowSettings ?? throw new ArgumentNullException(nameof(gitFlowSettings));
            _localBranches = localBranches ?? Array.Empty<LocalBranch>();

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Start Git Flow release");
            DialogDescription = Translate("Create a new release branch based on 'develop' and switch to it");
            SubmitButtonTitle = Translate("Start Release");
            CancelButtonTitle = Translate("Cancel");
            Title = DialogTitle;

            Refresh();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                if (!(BranchesComboBox.SelectedItem is LocalBranch))
                {
                    return false;
                }
                string? text = ReleaseNameTextBox.Text;
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }
                string? text2 = ReferenceNameValidator.ValidateGitFlow(text);
                if (text2 != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, text2);
                    return false;
                }
                string branchName = (_gitFlowSettings.ReleasePrefix + text).ToLower();
                if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string? releaseName = ReleaseNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                return null;
            }
            LocalBranch? baseBranch = BranchesComboBox.SelectedItem as LocalBranch;
            if (baseBranch == null)
            {
                return null;
            }
            return "git flow release start " + releaseName + " " + baseBranch.Name;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string? preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            LocalBranch? startPoint = BranchesComboBox.SelectedItem as LocalBranch;
            if (startPoint == null)
            {
                return;
            }
            string releaseName = ReleaseNameTextBox.Text ?? string.Empty;
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Starting '" + _gitFlowSettings.ReleasePrefix + releaseName + "'...");

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new StartGitFlowReleaseGitCommand().Execute(_gitModule, releaseName, startPoint, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    Close(result);
                });
            });
        }

        // 对照 WPF: ReleaseName_TextChanged
        public void ReleaseName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: BranchesComboBox_SelectionChanged
        public void BranchesComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        private void Refresh()
        {
            BranchesComboBox.ItemsSource = _localBranches;
            // 对照 WPF: SelectedItem = IReadOnlyListExtensions.FirstItem(DevelopBranch) ?? FirstItem(IsActive)
            // 静态调用消歧：LocalBranch[] 同时实现 IList<T> 与 IReadOnlyList<T>，扩展方法二义
            BranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.Name == _gitFlowSettings.DevelopBranch)
                ?? IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.IsActive);
            ReleasePrefixTextBlock.Text = _gitFlowSettings.ReleasePrefix;
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            BranchesComboBox.IsEnabled = false;
            ReleaseNameTextBox.IsEnabled = false;
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
