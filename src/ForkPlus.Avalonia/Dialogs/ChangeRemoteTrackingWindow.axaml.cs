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

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 ChangeRemoteTrackingWindow（真实迁移版，对照 WPF ChangeRemoteTrackingWindow.xaml.cs 162 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ChangeRemoteTrackingWindow.xaml.cs：
    //   - public partial class ChangeRemoteTrackingWindow : ForkPlusDialogWindow
    //   - 嵌套类 RemoteBranchItem（INotifyPropertyChanged）+ 枚举 RemoteBranchItemType { RemoteBranch, Separator, NoTracking }
    //   - 字段: GitModule _gitModule / LocalBranch _localBranch / RepositoryReferences _references
    //   - 构造函数 (GitModule, LocalBranch, RepositoryReferences):
    //     * DialogTitle="Change tracking reference" / DialogDescription="Change branch remote tracking reference"
    //     * SubmitButtonTitle="Change"
    //     * GitPointView.Value = _localBranch
    //     * Refresh() 构造 [NoTracking, Separator, ...RemoteBranches] 列表，按 UpstreamFullReference 选中
    //   - IsSubmitAllowed override: 选中项与当前 UpstreamFullReference 相同 false
    //   - GetCommandPreview override:
    //     * NoTracking: "git branch --unset-upstream <localName>"
    //     * RemoteBranch: "git branch --set-upstream-to=<remote>/<shortName> <localName>"
    //   - OnSubmit: JobQueue.Add → UpdateTrackingReferenceGitCommand.Execute(_gitModule, _localBranch, trackingReference, monitor)
    //     → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RemoteBranchItem 嵌套类 → spike 版简化（去 INotifyPropertyChanged / IconVisibility / Image）
    //   3. GitPointView → TextBlock 显示 localBranch.Name（spike 简化）
    //   4. ComboBox ItemTemplate → spike 版用 Separator + TextBlock + IsVisible 绑定（替代 WPF DataTrigger 样式）
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   6. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. IReadOnlyListExtensions.FirstItem → LINQ FirstOrDefault
    //   9. MainWindow.ActiveRepositoryUserControl.JobQueue → Task.Run + Dispatcher.UIThread.Post
    public partial class ChangeRemoteTrackingWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版 RemoteBranchItemType 枚举（对照 WPF ChangeRemoteTrackingWindow.RemoteBranchItemType）
        public enum RemoteBranchItemType
        {
            RemoteBranch,
            Separator,
            NoTracking
        }

        // spike 版 RemoteBranchItem（对照 WPF ChangeRemoteTrackingWindow.RemoteBranchItem）。
        // 简化：去掉 INotifyPropertyChanged / IconVisibility / Image，加 IsSeparator / IsTextItem 用于 DataTemplate 绑定。
        public class RemoteBranchItem
        {
            public RemoteBranchItemType ItemType { get; }
            public RemoteBranch? RemoteBranch { get; }
            public string Title { get; }
            public string ShortName { get; }

            // spike 版 DataTemplate 绑定辅助属性
            public bool IsSeparator => ItemType == RemoteBranchItemType.Separator;
            public bool IsTextItem => ItemType != RemoteBranchItemType.Separator;

            public static RemoteBranchItem CreateRemoteBranchItem(RemoteBranch remoteBranch)
            {
                return new RemoteBranchItem(remoteBranch.Name, RemoteBranchItemType.RemoteBranch, remoteBranch);
            }

            public static RemoteBranchItem CreateNoTrackingItem()
            {
                return new RemoteBranchItem(Translate("No tracking"), RemoteBranchItemType.NoTracking);
            }

            public static RemoteBranchItem CreateSeparator()
            {
                return new RemoteBranchItem("", RemoteBranchItemType.Separator);
            }

            public RemoteBranchItem(string title, RemoteBranchItemType type, RemoteBranch? remoteBranch = null)
            {
                Title = title;
                ShortName = remoteBranch?.ShortName ?? title;
                ItemType = type;
                RemoteBranch = remoteBranch;
            }
        }

        private readonly GitModule _gitModule;
        private readonly LocalBranch _localBranch;
        private readonly RepositoryReferences _references;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 相同（GitModule + LocalBranch + RepositoryReferences），加可选 Action 回调
        public ChangeRemoteTrackingWindow(
            GitModule gitModule,
            LocalBranch localBranch,
            RepositoryReferences references,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _localBranch = localBranch ?? throw new ArgumentNullException(nameof(localBranch));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Change tracking reference");
            DialogDescription = Translate("Change branch remote tracking reference");
            SubmitButtonTitle = Translate("Change");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Change tracking reference");

            // 对照 WPF: GitPointView.Value = _localBranch;
            LocalBranchTextBlock.Text = _localBranch.Name ?? "(local branch)";

            Refresh();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                RemoteBranch? obj = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
                if (_localBranch.UpstreamFullReference == obj?.FullReference)
                {
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            RemoteBranchItem? selectedItem = RemoteBranchesComboBox.SelectedItem as RemoteBranchItem;
            if (selectedItem == null)
            {
                return null;
            }
            string localName = _localBranch.Name;
            string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
            if (selectedItem.ItemType == RemoteBranchItemType.NoTracking)
            {
                return "git branch --unset-upstream " + Quote(localName);
            }
            RemoteBranch? remoteBranch = selectedItem.RemoteBranch;
            if (remoteBranch == null)
            {
                return null;
            }
            return "git branch --set-upstream-to=" + remoteBranch.Remote + "/" + remoteBranch.ShortName + " " + Quote(localName);
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
            RemoteBranch? trackingReference = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
            string name = Translate((trackingReference == null) ? "Remove tracking reference" : "Update tracking reference");
            string message = Translate((trackingReference == null) ? "Removing tracking reference..." : "Updating tracking reference...");
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, message);

            GitModule gitModule = _gitModule;
            LocalBranch localBranch = _localBranch;
            RemoteBranch? trackRef = trackingReference;

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult updateTrackingResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, localBranch, trackRef, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(updateTrackingResult); } catch (Exception ex) { Log.Error("ChangeRemoteTrackingWindow onCompleted failed", ex); }
                    Close(updateTrackingResult);
                });
            });
        }

        // 对照 WPF: RemoteBranchesComboBox_SelectionChanged
        public void RemoteBranchesComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: private void Refresh()
        private void Refresh()
        {
            RemoteBranchItem noTrackingItem = RemoteBranchItem.CreateNoTrackingItem();
            // 对照 WPF: RemoteBranchItem[] array = _references.RemoteBranches.Map(x => RemoteBranchItem.CreateRemoteBranchItem(x));
            RemoteBranchItem[] array = _references.RemoteBranches
                .Select((RemoteBranch x) => RemoteBranchItem.CreateRemoteBranchItem(x))
                .ToArray();
            var list = new List<RemoteBranchItem>(array.Length + 2);
            list.Add(noTrackingItem);
            list.Add(RemoteBranchItem.CreateSeparator());
            list.AddRange(array);
            RemoteBranchesComboBox.ItemsSource = list;

            string? upstreamFullReference = _localBranch.UpstreamFullReference;
            if (upstreamFullReference != null)
            {
                // 对照 WPF: IReadOnlyListExtensions.FirstItem(array, x => x.RemoteBranch.FullReference == upstreamFullReference)
                RemoteBranchesComboBox.SelectedItem = array.FirstOrDefault((RemoteBranchItem x) => x.RemoteBranch?.FullReference == upstreamFullReference);
            }
            else
            {
                RemoteBranchesComboBox.SelectedItem = noTrackingItem;
            }
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RemoteBranchesComboBox.IsEnabled = false;
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
