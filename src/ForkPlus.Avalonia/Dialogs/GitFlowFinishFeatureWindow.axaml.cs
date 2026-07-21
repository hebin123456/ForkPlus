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
    // Phase 4.33b：Avalonia 版 GitFlowFinishFeatureWindow（真实迁移版，对照 WPF GitFlowFinishFeatureWindow.xaml.cs 106 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitFlowFinishFeatureWindow.xaml.cs：
    //   - public partial class GitFlowFinishFeatureWindow : ForkPlusDialogWindow
    //   - 字段: GitModule / LocalBranch _featureBranch / RepositoryData / GitFlowSettings / IReadOnlyList<LocalBranch>
    //   - 构造函数 (GitModule gitModule, RepositoryData repositoryData, LocalBranch featureBranch)
    //   - GetCommandPreview override: "git flow feature finish [-r] [--no-ff] <feature>"
    //   - OnSubmit: FinishGitFlowFeatureGitCommand().Execute(_gitModule, feature, rebase, deleteBranches, noFastForward, monitor)
    //     → Close(result)
    //   - Refresh: 过滤 LocalBranches 以 FeaturePrefix 开头，选中 _featureBranch，恢复 3 个 CheckBox 状态
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 BranchesComboBox + 3 个 CheckBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. RepositoryData.References.LocalBranches.Filter / .FirstItem → LINQ Where().ToList() / FirstOrDefault()
    //   8. TextChangedEventArgs → Avalonia 同名类型
    public partial class GitFlowFinishFeatureWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch _featureBranch;
        private readonly RepositoryData _repositoryData;
        private GitFlowSettings _gitFlowSettings;
        private IReadOnlyList<LocalBranch> _allFeatureBranches;

        // 构造函数签名：保留 (GitModule, RepositoryData, LocalBranch) 与 WPF 一致
        public GitFlowFinishFeatureWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch featureBranch)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _repositoryData = repositoryData ?? throw new ArgumentNullException(nameof(repositoryData));
            _featureBranch = featureBranch;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Finish Git Flow feature");
            DialogDescription = Translate("Finish the feature and merge it into the develop branch");
            SubmitButtonTitle = Translate("Finish");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Finish Git Flow feature");

            Refresh();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch) || _gitFlowSettings == null)
            {
                return null;
            }
            string feature = localBranch.Name.Remove(0, _gitFlowSettings.FeaturePrefix.Length);
            var parts = new List<string> { "git", "flow", "feature", "finish" };
            if (RebaseInsteadOfMergeCheckBox.IsChecked.GetValueOrDefault()) parts.Add("-r");
            if (NoFastForwardCheckBox.IsChecked.GetValueOrDefault()) parts.Add("--no-ff");
            parts.Add(feature);
            return string.Join(" ", parts);
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
            string feature = localBranch.Name.Remove(0, _gitFlowSettings.FeaturePrefix.Length);
            bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();
            bool rebase = RebaseInsteadOfMergeCheckBox.IsChecked.GetValueOrDefault();
            bool noFastForward = NoFastForwardCheckBox.IsChecked.GetValueOrDefault();

            // 对照 WPF: ForkPlusSettings.Default.GitFlowFinishFeature_* = ...; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.GitFlowFinishFeature_DeleteBranches = deleteBranches;
            ForkPlusSettings.Default.GitFlowFinishFeature_Rebase = rebase;
            ForkPlusSettings.Default.GitFlowFinishFeature_NoFastForward = noFastForward;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...) + base.Dispatcher.Async(Close)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new FinishGitFlowFeatureGitCommand().Execute(
                    _gitModule, feature, rebase, deleteBranches, noFastForward, monitor);
                Dispatcher.UIThread.Post(delegate { Close(result); });
            });
        }

        private void Refresh()
        {
            _gitFlowSettings = _repositoryData.GitFlowSettings;
            _allFeatureBranches = _repositoryData.References.LocalBranches
                .Where((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.FeaturePrefix))
                .ToList();
            BranchesComboBox.ItemsSource = _allFeatureBranches;
            BranchesComboBox.SelectedItem = _allFeatureBranches.FirstOrDefault((LocalBranch x) => x.Name == _featureBranch.Name);
            DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_DeleteBranches;
            RebaseInsteadOfMergeCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_Rebase;
            NoFastForwardCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_NoFastForward;
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

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            BranchesComboBox.IsEnabled = false;
            DeleteBranchesCheckBox.IsEnabled = false;
            RebaseInsteadOfMergeCheckBox.IsEnabled = false;
            NoFastForwardCheckBox.IsEnabled = false;
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
