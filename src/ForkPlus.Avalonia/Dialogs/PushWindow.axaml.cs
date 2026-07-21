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
    // Phase 4.x：Avalonia 版 PushWindow（spike 真实迁移版，对照 WPF PushWindow.xaml.cs 616 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PushWindow.xaml.cs：
    //   - public partial class PushWindow : ForkPlusDialogWindow
    //   - 嵌套类 RemoteItem / RemoteBranchItem / RemoteItemType / RemoteBranchItemType
    //   - 字段: RepositoryUserControl _repositoryUserControl / Remote _remoteToSelect / LocalBranch _localBranchToSelect
    //           LocalBranch[] _localBranches / Remote[] _remotes / RemoteBranch[] _allRemoteBranches
    //           string _customRefspec / bool _stopRefresh / RemoteItem[] RemoteItems
    //   - 构造函数 (RepositoryUserControl, Remote, LocalBranch) → Refresh() + CheckSubmodules() + UpdateSubmitButton()
    //   - IsSubmitAllowed: SelectedLocalBranch + SelectedRemote 不为 null
    //   - GetCommandPreview: git push [--force-with-lease] [--tags] [--set-upstream] remote [localBranch:dst]
    //   - OnSubmit: PushGitCommand().Execute(...) → InvalidateAndRefresh(Revisions | References)
    //
    // Avalonia 版差异（spike）：
    //   1. 构造函数注入 GitModule + RepositoryReferences + RepositoryRemotes + Action 回调替代 RepositoryUserControl
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + CheckBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. Image.Show()/Hide() → IsVisible = true/false
    //   8. Collapse()/Show() 扩展方法 → IsVisible = false/true
    //   9. AccountManager account auto-login 暂不接入，仅触发 onCompleted 回调
    //  10. EditRemoteWindow / AddCustomRefspecWindow 依赖暂不接入（remote / remoteBranch 仅显示已存在项）
    //  11. ForcePushWarningImage (PNG) → spike 版用 TextBlock "⚠" 替代
    //  12. IReadOnlyListExtensions.FirstItem → list.FirstOrDefault(predicate) (LINQ)
    //  13. _localBranches.AnyItem(...) → _localBranches.Any(...) (LINQ)
    //  14. CheckSubmodules() 依赖 RepositoryData.Submodules，spike 简化为不调用（避免引入 Submodule 依赖）
    public partial class PushWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: RemoteItem 嵌套类（spike 简化：去掉 ImageSource/Visibility 依赖）
        public class RemoteItem
        {
            public RemoteItemType ItemType { get; private set; }

            public string Title { get; private set; }

            public Remote Remote { get; private set; }

            public string Name => Remote?.Name;

            public static RemoteItem CreateRemoteItem(Remote remote)
            {
                return new RemoteItem(remote.Name, RemoteItemType.Remote, remote);
            }

            public static RemoteItem CreateAddExistingRemoteItem()
            {
                return new RemoteItem(PushWindow.Translate("Add Remote..."), RemoteItemType.AddExistingRemote);
            }

            private RemoteItem(string title, RemoteItemType type, Remote remote = null)
            {
                Title = title;
                ItemType = type;
                Remote = remote;
            }
        }

        public enum RemoteItemType
        {
            Remote,
            AddExistingRemote
        }

        // 对照 WPF: RemoteBranchItem 嵌套类（spike 简化：去掉 Visibility 依赖）
        public class RemoteBranchItem
        {
            public RemoteBranchItemType ItemType { get; private set; }

            public RemoteBranch RemoteBranch { get; private set; }

            public string Title { get; private set; }

            public string ShortName { get; private set; }

            public static RemoteBranchItem CreateRemoteBranchItem(RemoteBranch remoteBranch)
            {
                return new RemoteBranchItem(remoteBranch.Name, RemoteBranchItemType.Branch, remoteBranch);
            }

            public static RemoteBranchItem CreateCustomItem(string title, RemoteBranch remoteBranch = null)
            {
                return new RemoteBranchItem(title, RemoteBranchItemType.Custom, remoteBranch);
            }

            public static RemoteBranchItem CreateAddCustomItem()
            {
                return new RemoteBranchItem(PushWindow.Translate("Custom..."), RemoteBranchItemType.AddCustom);
            }

            private RemoteBranchItem(string title, RemoteBranchItemType type, RemoteBranch remoteBranch = null)
            {
                Title = title;
                ShortName = remoteBranch?.ShortName ?? Title;
                ItemType = type;
                RemoteBranch = remoteBranch;
            }
        }

        public enum RemoteBranchItemType
        {
            Branch,
            Custom,
            AddCustom
        }

        private readonly GitModule _gitModule;
        private readonly RepositoryReferences _references;
        private readonly RepositoryRemotes _remotesSource;
        private readonly Action<GitCommandResult> _onCompleted;

        private readonly Remote _remoteToSelect;
        private readonly LocalBranch _localBranchToSelect;

        private LocalBranch[] _localBranches;
        private Remote[] _remotes;
        private RemoteBranch[] _allRemoteBranches;

        private string _customRefspec;
        private bool _stopRefresh;

        private RemoteItem[] RemoteItems { get; set; }

        private Remote SelectedRemote => (RemotesComboBox.SelectedItem as RemoteItem)?.Remote;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences + RepositoryRemotes + Action 回调替代 RepositoryUserControl
        public PushWindow(
            GitModule gitModule,
            RepositoryReferences references,
            RepositoryRemotes remotes,
            Remote remote = null,
            LocalBranch localBranch = null,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references ?? RepositoryReferences.Empty;
            _remotesSource = remotes ?? RepositoryRemotes.Empty;
            _remoteToSelect = remote;
            _localBranchToSelect = localBranch;
            _onCompleted = onCompleted;
            _customRefspec = null;

            DialogTitle = Translate("Push");
            DialogDescription = Translate("Push your local changes to remote repository");
            SubmitButtonTitle = Translate("Push");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Push");

            // 对照 WPF: AllTagsCheckBox.IsChecked = ForkPlusSettings.Default.Push_PushAllTags;
            AllTagsCheckBox.IsChecked = ForkPlusSettings.Default.Push_PushAllTags;
            // 对照 WPF: ForcePushWarningImage.ToolTip = Translate("Overwrite the remote branch...");
            ToolTip.SetTip(ForcePushWarningImage, Translate("Overwrite the remote branch even if it's not an ancestor of the local branch.\n- Force push is required for rebase of already published branch.\n- Blindly using force push can be dangerous as you can overwrite other users' commits.\n- Fork always uses --force-with-lease which protects from race conditions."));

            Refresh();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (!(LocalBranchesComboBox.SelectedItem is LocalBranch) || SelectedRemote == null)
                {
                    return false;
                }
                return base.IsSubmitAllowed;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            Remote remote = SelectedRemote;
            if (remote == null || !(LocalBranchesComboBox.SelectedItem is LocalBranch localBranch))
            {
                return null;
            }
            RemoteBranch remoteBranch = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
            bool pushAllTags = AllTagsCheckBox.IsChecked.GetValueOrDefault();
            bool force = ForcePushCheckBox.IsChecked.GetValueOrDefault();
            bool track = false;
            if (localBranch.UpstreamFullReference == null)
            {
                track = CreateTrackingReferenceCheckBox.IsChecked.GetValueOrDefault(true);
            }
            var parts = new List<string> { "git", "push" };
            if (force) parts.Add("--force-with-lease");
            if (pushAllTags) parts.Add("--tags");
            if (track) parts.Add("--set-upstream");
            parts.Add(remote.Name);
            if (remoteBranch != null)
            {
                string dst = (remoteBranch.Remote == remote.Name)
                    ? ("refs/heads/" + remoteBranch.ShortName)
                    : ("refs/heads/" + localBranch.Name);
                parts.Add(localBranch.FullReference + ":" + dst);
            }
            else if (_customRefspec != null)
            {
                parts.Add(localBranch.FullReference + ":" + _customRefspec);
            }
            else
            {
                parts.Add(localBranch.FullReference);
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            Remote remote = SelectedRemote;
            if (remote == null)
            {
                return;
            }
            LocalBranch localBranch = LocalBranchesComboBox.SelectedItem as LocalBranch;
            if (localBranch == null)
            {
                return;
            }
            GitModule gitModule = _gitModule;
            RemoteBranch remoteBranch = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
            bool pushAllTags = AllTagsCheckBox.IsChecked.GetValueOrDefault();
            bool force = ForcePushCheckBox.IsChecked.GetValueOrDefault();
            bool track = false;
            string customRefspec = _customRefspec;

            ForkPlusSettings.Default.Push_PushAllTags = pushAllTags;
            ForkPlusSettings.Default.Save();
            if (_remotes.Length > 1)
            {
                gitModule.Settings.RecentRemote = remote.Name;
            }
            if (localBranch.UpstreamFullReference == null)
            {
                track = CreateTrackingReferenceCheckBox.IsChecked.GetValueOrDefault(true);
            }

            string jobName = string.Format(Translate("Push '{0}' to '{1}'"), localBranch.Name, remote.Name);
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Pushing..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(jobName, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult pushResult = new PushGitCommand().Execute(
                    gitModule, remote.Name, localBranch, remoteBranch, customRefspec,
                    pushAllTags, force, track, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(pushResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("PushWindow onCompleted callback failed", ex);
                    }
                    Close(pushResult);
                });
            });
        }

        // 对照 WPF: ForcePushCheckBox_Changed
        public void ForcePushCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ForcePushWarningImage.IsVisible = ForcePushCheckBox.IsChecked.GetValueOrDefault();
            RefreshCommandPreview();
        }

        // 对照 WPF: CheckBox_Changed
        public void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: LocalBranchesComboBox_SelectionChanged
        public void LocalBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(LocalBranchesComboBox.SelectedItem is LocalBranch localBranch))
            {
                return;
            }
            _customRefspec = null;
            if (localBranch.UpstreamFullReference != null)
            {
                CreateTrackingReferenceCheckBox.IsChecked = false;
                CreateTrackingReferenceCheckBox.IsVisible = false;
            }
            else
            {
                CreateTrackingReferenceCheckBox.IsChecked = true;
                CreateTrackingReferenceCheckBox.IsVisible = true;
            }
            if (!_stopRefresh)
            {
                RemoteBranch upstream = FindUpstream(_allRemoteBranches, localBranch);
                string recentRemote = _gitModule.Settings.RecentRemote;
                Remote remote = _remotes.FirstOrDefault(x => x.Name == upstream?.Remote)
                    ?? _remotes.FirstOrDefault(x => x.Name == recentRemote)
                    ?? _remotes.FirstOrDefault(x => x.Name == Consts.Git.DefaultRemoteName)
                    ?? _remotes.FirstOrDefault();
                if (remote != null)
                {
                    _stopRefresh = true;
                    SelectRemote(remote);
                    _stopRefresh = false;
                }
                RefreshRemoteBranches();
                UpdateSubmitButton();
            }
            RefreshCommandPreview();
        }

        // 对照 WPF: RemotesComboBox_SelectionChanged
        public void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_stopRefresh)
            {
                return;
            }
            _customRefspec = null;
            if (!(RemotesComboBox.SelectedItem is RemoteItem remoteItem))
            {
                return;
            }
            // spike: 不接入 AddExistingRemote 分支（依赖 EditRemoteWindow）
            RefreshRemoteBranches();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RemoteBranchesComboBox_SelectionChanged
        public void RemoteBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // spike: 不接入 AddCustom 分支（依赖 AddCustomRefspecWindow）
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        private void RefreshRemoteBranches()
        {
            if (SelectedRemote == null)
            {
                return;
            }
            string selectedRemoteName = SelectedRemote.Name;
            List<RemoteBranch> list = _allRemoteBranches
                .Where(x => x.Remote == selectedRemoteName)
                .ToList();
            var array = list.Select(x => RemoteBranchItem.CreateRemoteBranchItem(x)).ToArray();
            object selectedItem = LocalBranchesComboBox.SelectedItem;
            LocalBranch selectedLocalBranch = selectedItem as LocalBranch;
            if (selectedLocalBranch == null)
            {
                RemoteBranchesComboBox.ItemsSource = array;
                return;
            }
            string text = null;
            RemoteBranch remoteBranch = null;
            RemoteBranch upstream = FindUpstream(list, selectedLocalBranch);
            if (upstream != null)
            {
                text = string.Format(Translate("default ({0})"), upstream.Name);
            }
            else
            {
                string trackingReferenceName = GetLocalBranchTrackingReferenceName(selectedLocalBranch);
                if (trackingReferenceName != null)
                {
                    remoteBranch = list.FirstOrDefault(x => x.ShortName == trackingReferenceName);
                    if (remoteBranch == null)
                    {
                        text = string.Format(Translate("new ({0})"), selectedRemoteName + "/" + trackingReferenceName);
                    }
                }
                else if (remoteBranch == null)
                {
                    remoteBranch = list.FirstOrDefault(x => x.ShortName == selectedLocalBranch.Name);
                    if (remoteBranch == null)
                    {
                        text = string.Format(Translate("new ({0})"), selectedRemoteName + "/" + selectedLocalBranch.Name);
                    }
                }
            }
            var list2 = new List<RemoteBranchItem>(list.Count + 5);
            RemoteBranchItem defaultItem = null;
            if (text != null)
            {
                defaultItem = RemoteBranchItem.CreateCustomItem(text);
                list2.Add(defaultItem);
            }
            RemoteBranchItem matchedItem = null;
            foreach (var item in array)
            {
                list2.Add(item);
                if (remoteBranch == item.RemoteBranch)
                {
                    matchedItem = item;
                }
            }
            RemoteBranchesComboBox.ItemsSource = list2.ToArray();
            RemoteBranchesComboBox.SelectedItem = matchedItem ?? defaultItem;
        }

        private string GetLocalBranchTrackingReferenceName(LocalBranch branch)
        {
            string upstreamFullReference = branch.UpstreamFullReference;
            if (upstreamFullReference == null)
            {
                return null;
            }
            if (!upstreamFullReference.StartsWith("refs/remotes/"))
            {
                return null;
            }
            string text = upstreamFullReference.Substring("refs/remotes/".Length);
            int num = text.IndexOf('/');
            if (num != -1 && num + 1 < text.Length)
            {
                return text.Substring(num + 1);
            }
            return null;
        }

        private void Refresh()
        {
            if (_references == null)
            {
                return;
            }
            GitModule gitModule = _gitModule;
            if (gitModule == null)
            {
                return;
            }
            _stopRefresh = true;
            _localBranches = _references.LocalBranches ?? Array.Empty<LocalBranch>();
            _remotes = _remotesSource.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
            _allRemoteBranches = _references.RemoteBranches ?? Array.Empty<RemoteBranch>();
            LocalBranchesComboBox.ItemsSource = _localBranches;
            LocalBranch activeBranch = _references.ActiveBranch;
            LocalBranchesComboBox.SelectedItem =
                _localBranches.FirstOrDefault(x => x.FullReference == _localBranchToSelect?.FullReference)
                ?? activeBranch
                ?? _localBranches.FirstOrDefault();
            RefreshRemotes();
            Remote remote = null;
            string upstream = activeBranch?.UpstreamFullReference;
            if (upstream != null)
            {
                RemoteBranch activeUpstream = _allRemoteBranches.FirstOrDefault(x => x.FullReference == upstream);
                if (activeUpstream != null)
                {
                    remote = _remotes.FirstOrDefault(x => x.Name == activeUpstream.Remote);
                }
            }
            string recentRemote = gitModule.Settings.RecentRemote;
            Remote remote2 = _remotes.FirstOrDefault(x => x.Name == _remoteToSelect?.Name)
                ?? remote
                ?? _remotes.FirstOrDefault(x => x.Name == recentRemote)
                ?? _remotes.FirstOrDefault(x => x.Name == Consts.Git.DefaultRemoteName)
                ?? _remotes.FirstOrDefault();
            _stopRefresh = false;
            SelectRemote(remote2);
        }

        private void RefreshRemotes()
        {
            if (_remotes.Length == 1)
            {
                RemotesLabel.IsVisible = false;
                RemotesComboBox.IsVisible = false;
            }
            else
            {
                RemotesLabel.IsVisible = true;
                RemotesComboBox.IsVisible = true;
            }
            var list = new List<RemoteItem>(_remotes.Length + 1);
            list.AddRange(_remotes.Select(RemoteItem.CreateRemoteItem));
            if (_remotes.Length == 0)
            {
                list.Add(RemoteItem.CreateAddExistingRemoteItem());
            }
            RemoteItems = list.ToArray();
            RemotesComboBox.ItemsSource = RemoteItems;
        }

        private void SelectRemote(Remote remote)
        {
            RemotesComboBox.SelectedItem = (RemoteItems ?? Array.Empty<RemoteItem>())
                .FirstOrDefault(x => x.ItemType == RemoteItemType.Remote && x.Remote == remote);
        }

        private static RemoteBranch FindUpstream(IReadOnlyList<RemoteBranch> remoteBranches, LocalBranch localBranch)
        {
            string upstreamFullReference = localBranch?.UpstreamFullReference;
            if (upstreamFullReference == null)
            {
                return null;
            }
            return remoteBranches.FirstOrDefault(x => x.FullReference == upstreamFullReference);
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            LocalBranchesComboBox.IsEnabled = false;
            RemotesComboBox.IsEnabled = false;
            RemoteBranchesComboBox.IsEnabled = false;
            CreateTrackingReferenceCheckBox.IsEnabled = false;
            AllTagsCheckBox.IsEnabled = false;
            ForcePushCheckBox.IsEnabled = false;
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
