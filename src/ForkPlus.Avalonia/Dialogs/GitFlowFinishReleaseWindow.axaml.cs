using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.33b：Avalonia 版 GitFlowFinishReleaseWindow（真实迁移版，对照 WPF GitFlowFinishReleaseWindow.xaml.cs 95 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitFlowFinishReleaseWindow.xaml.cs：
    //   - public partial class GitFlowFinishReleaseWindow : ForkPlusDialogWindow
    //   - 字段: GitModule / LocalBranch _releaseBranch / RepositoryData / GitFlowSettings / IReadOnlyList<LocalBranch>
    //   - 构造函数 (GitModule gitModule, RepositoryData repositoryData, LocalBranch releaseBranch)
    //   - GetCommandPreview override: "git flow release finish <release>"
    //   - OnSubmit: FinishGitFlowReleaseGitCommand().Execute(_gitModule, release, deleteBranches, noBackmerge, tagMessage, monitor)
    //     → Close(result)
    //   - BackMerge 逻辑：bool noBackmerge = !BackMergeMasterCheckBox.IsChecked.GetValueOrDefault();
    //     ForkPlusSettings.Default.GitFlowFinishRelease_BackMergeMaster = !noBackmerge;
    //   - Refresh: 过滤 LocalBranches 以 ReleasePrefix 开头，选中 _releaseBranch，
    //     恢复 DeleteBranchesCheckBox + BackMergeMasterCheckBox
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 BranchesComboBox + 2 个 CheckBox + TagMessageTextBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. RepositoryData.References.LocalBranches.Filter / .FirstItem → LINQ Where().ToList() / FirstOrDefault()
    //   8. TextChangedEventArgs → Avalonia 同名类型
    public partial class GitFlowFinishReleaseWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch _releaseBranch;
        private readonly RepositoryData _repositoryData;
        private GitFlowSettings _gitFlowSettings;
        private IReadOnlyList<LocalBranch> _allReleaseBranches;

        // 构造函数签名：保留 (GitModule, RepositoryData, LocalBranch) 与 WPF 一致
        public GitFlowFinishReleaseWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch releaseBranch)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _repositoryData = repositoryData ?? throw new ArgumentNullException(nameof(repositoryData));
            _releaseBranch = releaseBranch;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Finish Git Flow release");
            DialogDescription = Translate("Finish the release and merge it into the develop and master branches");
            SubmitButtonTitle = Translate("Finish");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Finish Git Flow release");

            Refresh();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch) || _gitFlowSettings == null)
            {
                return null;
            }
            string release = localBranch.Name.Remove(0, _gitFlowSettings.ReleasePrefix.Length);
            return "git flow release finish " + release;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch)) return;
            string release = localBranch.Name.Remove(0, _gitFlowSettings.ReleasePrefix.Length);
            string tagMessage = TagMessageTextBox.Text;
            bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();
            bool noBackmerge = !BackMergeMasterCheckBox.IsChecked.GetValueOrDefault();

            // 对照 WPF: ForkPlusSettings.Default.GitFlowFinishRelease_* = ...; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.GitFlowFinishRelease_DeleteBranches = deleteBranches;
            ForkPlusSettings.Default.GitFlowFinishRelease_BackMergeMaster = !noBackmerge;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...) + base.Dispatcher.Async(Close)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new FinishGitFlowReleaseGitCommand().Execute(
                    _gitModule, release, deleteBranches, noBackmerge, tagMessage, monitor);
                Dispatcher.UIThread.Post(delegate { Close(result); });
            });
        }

        private void Refresh()
        {
            _gitFlowSettings = _repositoryData.GitFlowSettings;
            _allReleaseBranches = _repositoryData.References.LocalBranches
                .Where((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.ReleasePrefix))
                .ToList();
            BranchesComboBox.ItemsSource = _allReleaseBranches;
            BranchesComboBox.SelectedItem = _allReleaseBranches.FirstOrDefault((LocalBranch x) => x.Name == _releaseBranch.Name);
            DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishRelease_DeleteBranches;
            BackMergeMasterCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishRelease_BackMergeMaster;
            RefreshCommandPreview();
        }

        // 对照 WPF: BranchesComboBox_SelectionChanged
        private void BranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: CheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：TagMessage 改变不需要刷新命令预览（WPF 命令预览不包含 tag message），
        // 但仍提供事件处理器以匹配 axaml 中的 TextChanged 订阅。
        private void TagMessage_TextChanged(object sender, TextChangedEventArgs e)
        {
            // WPF 版命令预览不包含 tag message，无需刷新；保留 handler 仅用于 axaml 绑定兼容
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            BranchesComboBox.IsEnabled = false;
            DeleteBranchesCheckBox.IsEnabled = false;
            BackMergeMasterCheckBox.IsEnabled = false;
            TagMessageTextBox.IsEnabled = false;
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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
