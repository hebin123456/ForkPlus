using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia spike 版 RebaseBranchWindow（对照 WPF RebaseBranchWindow.xaml.cs 309 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RebaseBranchWindow.xaml.cs：
    //   - public partial class RebaseBranchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch _source / IGitPoint _destination
    //           / bool _rebaseContainsLocalBranches
    //   - 构造函数 (RepositoryUserControl, LocalBranch source, IGitPoint destination)
    //     - 设置 DialogTitle/Description/SubmitButtonTitle
    //     - SourceGitPointView.Value = source / DestinationGitPointView.Value = destination
    //     - AutostashCheckBox / UpdateRefsCheckBox 初始 IsChecked 来自 ForkPlusSettings
    //     - RebaseTestGitCommand.Execute → SetStatus(Success/Warning)
    //     - GetMergeBase + GetLocalBranchesInRange → 决定 UpdateRefsCheckBox.Visibility + LocalBranchesListBox
    //   - GetCommandPreview override: "git rebase [--update-refs] [--autostash] <destination>"
    //   - OnSubmit: Stash→Checkout→Rebase→ApplyStash→UpdateSubmodules 多阶段 AddUndoable
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 source.Name / destination.FriendlyName 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 CheckBox
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   8. spike 简化：跳过 GetMergeBase + GetLocalBranchesInRange 检测（UpdateRefsCheckBox 始终可见，无 LocalBranchesListBox）、
    //      跳过 autostash 手动 stash/unstash、跳过 source checkout、跳过 submodule 更新
    public partial class RebaseBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch _source;
        private readonly IGitPoint _destination;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public RebaseBranchWindow(
            GitModule gitModule,
            LocalBranch source,
            IGitPoint destination,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Rebase");
            DialogDescription = Translate("Copy commits from one branch to another");
            SubmitButtonTitle = Translate("Rebase");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Rebase");

            // 对照 WPF: SourceGitPointView.Value = source; DestinationGitPointView.Value = destination;
            // Avalonia spike: 用 TextBlock 显示名称简化
            SourceTextBlock.Text = _source.Name;
            DestinationTextBlock.Text = _destination.FriendlyName;

            // 对照 WPF: AutostashCheckBox.IsChecked = ForkPlusSettings.Default.RebaseAutostash;
            AutostashCheckBox.IsChecked = ForkPlusSettings.Default.RebaseAutostash;
            // 对照 WPF: UpdateRefsCheckBox.IsChecked = ForkPlusSettings.Default.RebaseUpdateRefs;
            UpdateRefsCheckBox.IsChecked = ForkPlusSettings.Default.RebaseUpdateRefs;

            // 对照 WPF: RebaseTestGitCommand.Execute(...) 三态预检
            var testResult = new RebaseTestGitCommand().Execute(_gitModule, _source, _destination.ObjectName);
            if (testResult.Succeeded)
            {
                if (testResult.Result == RebaseTestGitCommand.TestResult.Success)
                {
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Rebase can be done without conflicts"));
                }
                else if (testResult.Result == RebaseTestGitCommand.TestResult.Conflict)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Rebase will cause conflicts"));
                }
            }
            else
            {
                Log.Warn(testResult.Error.FriendlyDescription);
            }

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_destination == null)
            {
                return null;
            }
            var parts = new List<string> { "git", "rebase" };
            bool updateRefs = UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
            bool autostash = AutostashCheckBox.IsChecked.GetValueOrDefault();
            if (updateRefs)
            {
                parts.Add("--update-refs");
            }
            if (autostash)
            {
                parts.Add("--autostash");
            }
            parts.Add(_destination.ObjectName);
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
            bool updateRefs = UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
            bool autostash = AutostashCheckBox.IsChecked.GetValueOrDefault();

            // 对照 WPF: ForkPlusSettings.Default.RebaseAutostash = ...; RebaseUpdateRefs = ...; Save();
            ForkPlusSettings.Default.RebaseAutostash = autostash;
            ForkPlusSettings.Default.RebaseUpdateRefs = updateRefs;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Rebasing..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(...) + base.Dispatcher.Async(Close)
            // Avalonia spike: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // 简化：仅执行核心 RebaseBranchGitCommand，跳过 stash/checkout/submodule 阶段
            string destination = _destination.ObjectName;
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new RebaseBranchGitCommand().Execute(
                    _gitModule, destination, rebaseMerges: false, updateRefs, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("RebaseBranchWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: UpdateRefsCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void UpdateRefsCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: AutostashCheckBox_Changed
        public void AutostashCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            UpdateRefsCheckBox.IsEnabled = false;
            AutostashCheckBox.IsEnabled = false;
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
