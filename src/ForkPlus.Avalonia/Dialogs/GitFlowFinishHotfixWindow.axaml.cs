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
    // Phase 4.33b：Avalonia 版 GitFlowFinishHotfixWindow（真实迁移版，对照 WPF GitFlowFinishHotfixWindow.xaml.cs 92 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitFlowFinishHotfixWindow.xaml.cs：
    //   - public partial class GitFlowFinishHotfixWindow : ForkPlusDialogWindow
    //   - 字段: GitModule / LocalBranch _hotfixBranch / RepositoryData / GitFlowSettings / IReadOnlyList<LocalBranch>
    //   - 构造函数 (GitModule gitModule, RepositoryData repositoryData, LocalBranch hotfixBranch)
    //   - GetCommandPreview override: "git flow hotfix finish <hotfix>"
    //   - OnSubmit: FinishGitFlowHotfixGitCommand().Execute(_gitModule, hotfix, deleteBranches, tagMessage, monitor)
    //     → Close(result)
    //   - Refresh: 过滤 LocalBranches 以 HotfixPrefix 开头，选中 _hotfixBranch，恢复 DeleteBranchesCheckBox
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 BranchesComboBox + DeleteBranchesCheckBox + TagMessageTextBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. RepositoryData.References.LocalBranches.Filter / .FirstItem → LINQ Where().ToList() / FirstOrDefault()
    //   8. TextChangedEventArgs → Avalonia 同名类型
    public partial class GitFlowFinishHotfixWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch _hotfixBranch;
        private readonly RepositoryData _repositoryData;
        private GitFlowSettings _gitFlowSettings;
        private IReadOnlyList<LocalBranch> _allHotfixBranches;

        // 构造函数签名：保留 (GitModule, RepositoryData, LocalBranch) 与 WPF 一致
        public GitFlowFinishHotfixWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch hotfixBranch)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _repositoryData = repositoryData ?? throw new ArgumentNullException(nameof(repositoryData));
            _hotfixBranch = hotfixBranch;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Finish Git Flow hotfix");
            DialogDescription = Translate("Finish the hotfix and merge it into the develop and master branches");
            SubmitButtonTitle = Translate("Finish");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Finish Git Flow hotfix");

            Refresh();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch) || _gitFlowSettings == null)
            {
                return null;
            }
            string hotfix = localBranch.Name.Remove(0, _gitFlowSettings.HotfixPrefix.Length);
            return "git flow hotfix finish " + hotfix;
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
            string hotfix = localBranch.Name.Remove(0, _gitFlowSettings.HotfixPrefix.Length);
            string tagMessage = TagMessageTextBox.Text;
            bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();

            // 对照 WPF: ForkPlusSettings.Default.GitFlowFinishHotfix_DeleteBranches = deleteBranches; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.GitFlowFinishHotfix_DeleteBranches = deleteBranches;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...) + base.Dispatcher.Async(Close)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new FinishGitFlowHotfixGitCommand().Execute(
                    _gitModule, hotfix, deleteBranches, tagMessage, monitor);
                Dispatcher.UIThread.Post(delegate { Close(result); });
            });
        }

        private void Refresh()
        {
            _gitFlowSettings = _repositoryData.GitFlowSettings;
            _allHotfixBranches = _repositoryData.References.LocalBranches
                .Where((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.HotfixPrefix))
                .ToList();
            BranchesComboBox.ItemsSource = _allHotfixBranches;
            BranchesComboBox.SelectedItem = _allHotfixBranches.FirstOrDefault((LocalBranch x) => x.Name == _hotfixBranch.Name);
            DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishHotfix_DeleteBranches;
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
