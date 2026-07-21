using System;
using System.Collections.Generic;
using System.Linq;
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
    // Avalonia spike 版 MergeBranchWindow（对照 WPF MergeBranchWindow.xaml.cs 187 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/MergeBranchWindow.xaml.cs：
    //   - public partial class MergeBranchWindow : ForkPlusDialogWindow
    //   - 嵌套类 MergeOptionComboBoxItem（INotifyPropertyChanged）：Title/Description/Command/MergeType/IsSeparator
    //   - 字段: RepositoryUserControl _repositoryUserControl / Reference _source / LocalBranch _destination
    //           / MergeOptionComboBoxItem[] _mergeOptionsComboBoxItems
    //   - 构造函数 (RepositoryUserControl, Reference source, LocalBranch destination)
    //     - DialogTitle/Description/SubmitButtonTitle
    //     - SourceGitPointView.Value = source / DestinationGitPointView.Value = destination
    //     - MergeTypeComboBox.ItemsSource = _mergeOptionsComboBoxItems
    //     - MergeTypeComboBox.SelectedItem = FirstItem(MergeType == ForkPlusSettings.Default.MergeType)
    //     - MergeBranchTestGitCommand.Execute → SetStatus(Success/Warning)
    //   - SelectedMergeType: ((MergeOptionComboBoxItem)MergeTypeComboBox.SelectedItem).MergeType
    //   - GetCommandPreview: "git merge [--no-ff|--squash|--no-commit] <source.Name>"
    //   - OnSubmit: (可选 Checkout) + MergeGitCommand + UpdateSubmodulesGitCommand
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 source.Name / destination.Name 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. ComboBox SelectionChanged 参数：Avalonia.Controls.SelectionChangedEventArgs
    //   8. spike 简化：跳过 separator 项（4 个 ComboBoxItem 而非 5 个）、跳过 checkout（假设 destination 已 active）、
    //      跳过 submodule 更新，仅执行核心 MergeGitCommand
    public partial class MergeBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: public class MergeOptionComboBoxItem : INotifyPropertyChanged
        // spike 版：简化为 POCO（spike 不需要 INotifyPropertyChanged，ComboBox 静态列表）
        public class MergeOptionComboBoxItem
        {
            public MergeType MergeType { get; }
            public string Title { get; }
            public string Description { get; }
            public string Command { get; }

            public MergeOptionComboBoxItem(string title, string description, string command, MergeType mergeType)
            {
                MergeType = mergeType;
                Title = title;
                Description = description;
                Command = command;
            }
        }

        private readonly GitModule _gitModule;
        private readonly Reference _source;
        private readonly LocalBranch _destination;
        private readonly RepositoryReferences? _references;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 对照 WPF: _mergeOptionsComboBoxItems（5 项含 Separator，spike 简化为 4 项无 Separator）
        private readonly MergeOptionComboBoxItem[] _mergeOptionsComboBoxItems = new MergeOptionComboBoxItem[4]
        {
            new MergeOptionComboBoxItem("Default", "Fast-forward if possible", "", MergeType.FastForward),
            new MergeOptionComboBoxItem("No Fast-Forward", "Always create a merge commit", "--no-ff", MergeType.NoFastForward),
            new MergeOptionComboBoxItem("Squash", "Squash merge", "--squash", MergeType.Squash),
            new MergeOptionComboBoxItem("Don't Commit", "Merge without commit", "--no-commit", MergeType.NoCommit)
        };

        // 对照 WPF: public MergeType SelectedMergeType
        public MergeType SelectedMergeType => (MergeTypeComboBox.SelectedItem as MergeOptionComboBoxItem)?.MergeType ?? MergeType.FastForward;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences + Action 回调替代 RepositoryUserControl
        public MergeBranchWindow(
            GitModule gitModule,
            Reference source,
            LocalBranch destination,
            RepositoryReferences? references = null,
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
            _references = references;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Merge Branch");
            DialogDescription = Translate("Merge branch into another one");
            SubmitButtonTitle = Translate("Merge");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Merge Branch");

            // 对照 WPF: SourceGitPointView.Value = source; DestinationGitPointView.Value = destination;
            // Avalonia spike: 用 TextBlock 显示名称简化
            SourceTextBlock.Text = _source.Name;
            DestinationTextBlock.Text = _destination.Name;

            // 对照 WPF: MergeTypeComboBox.ItemsSource = _mergeOptionsComboBoxItems;
            MergeTypeComboBox.ItemsSource = _mergeOptionsComboBoxItems;
            // 对照 WPF: MergeTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(..., x => x.MergeType == mergeTypeToSelect);
            MergeType mergeTypeToSelect = ForkPlusSettings.Default.MergeType;
            MergeTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(
                _mergeOptionsComboBoxItems,
                (MergeOptionComboBoxItem x) => x.MergeType == mergeTypeToSelect);

            // 对照 WPF: MergeBranchTestGitCommand.Execute(...) 三态预检
            var testResult = new MergeBranchTestGitCommand().Execute(_gitModule, _source, _destination);
            if (testResult.Succeeded)
            {
                if (testResult.Result == MergeBranchTestGitCommand.TestResult.Success)
                {
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Merge can be done without conflicts"));
                }
                else if (testResult.Result == MergeBranchTestGitCommand.TestResult.Conflict)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Merge will cause conflicts"));
                }
            }

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_source == null || !(MergeTypeComboBox.SelectedItem is MergeOptionComboBoxItem selected))
            {
                return null;
            }
            var parts = new List<string> { "git", "merge" };
            if (!string.IsNullOrEmpty(selected.Command))
            {
                parts.Add(selected.Command);
            }
            parts.Add(_source.Name);
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
            // MergeGitCommand.Execute 需要 RepositoryReferences 参数
            if (_references == null)
            {
                return;
            }
            Reference source = _source;
            LocalBranch destination = _destination;
            MergeType selectedMergeType = SelectedMergeType;
            RepositoryReferences references = _references;

            // 对照 WPF: ForkPlusSettings.Default.MergeType = selectedMergeType; Save();
            ForkPlusSettings.Default.MergeType = selectedMergeType;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Merging..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(...) + base.Dispatcher.Async(Close)
            // Avalonia spike: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // 简化：仅执行核心 MergeGitCommand，跳过 checkout（假设 destination 已 active）、跳过 submodule 更新
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new MergeGitCommand().Execute(
                    _gitModule, source, selectedMergeType, references, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("MergeBranchWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: MergeTypeComboBox_SelectionChanged
        public void MergeTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            MergeTypeComboBox.IsEnabled = false;
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
