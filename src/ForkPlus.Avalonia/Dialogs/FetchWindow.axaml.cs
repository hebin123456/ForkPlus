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
    // Phase 4.x：Avalonia 版 FetchWindow（spike 真实迁移版，对照 WPF FetchWindow.xaml.cs 119 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/FetchWindow.xaml.cs：
    //   - public partial class FetchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule / Remote _predefinedRemote
    //   - 构造函数 (RepositoryUserControl, GitModule, Remote) → 从 MainWindow.ActiveRepositoryUserControl.RepositoryData.Remotes 取 remotes
    //   - IsSubmitAllowed: RemoteComboBox.SelectedItem != null
    //   - GetCommandPreview: git fetch [--all | remote] [--tags]
    //   - OnSubmit: FetchGitCommand().Execute(...) → InvalidateAndRefresh(Revisions | References)
    //
    // Avalonia 版差异（spike）：
    //   1. 构造函数注入 GitModule + RepositoryRemotes + Action 回调替代 RepositoryUserControl
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + CheckBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   6. IReadOnlyListExtensions.FirstItem → list.FirstOrDefault(predicate) (LINQ)
    public partial class FetchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryRemotes _remotesSource;
        private readonly Action<GitCommandResult> _onCompleted;

        private readonly Remote _predefinedRemote;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryRemotes + Action 回调替代 RepositoryUserControl
        public FetchWindow(
            GitModule gitModule,
            RepositoryRemotes remotes,
            Remote remote = null,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _remotesSource = remotes ?? RepositoryRemotes.Empty;
            _predefinedRemote = remote;
            _onCompleted = onCompleted;

            DialogTitle = Translate("Fetch");
            DialogDescription = Translate("Fetch latest changes from remote repository");
            SubmitButtonTitle = Translate("Fetch");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Fetch");

            // 对照 WPF: Remote[] array = MainWindow.ActiveRepositoryUserControl.RepositoryData.Remotes.Items.ToSortedArray(...)
            Remote[] array = _remotesSource.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
            RemoteComboBox.ItemsSource = array;
            RemoteComboBox.SelectedItem =
                array.FirstOrDefault(x => x.Name == _predefinedRemote?.Name)
                ?? array.FirstOrDefault(x => x.Name == Consts.Git.DefaultRemoteName)
                ?? array.FirstOrDefault();

            FetchAllRemotesCheckBox.IsChecked = ForkPlusSettings.Default.Fetch_FetchAllRemotes;

            RefreshCommandPreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (RemoteComboBox.SelectedItem == null && !FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault())
                {
                    return false;
                }
                return base.IsSubmitAllowed;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            bool fetchAllRemotes = FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
            bool allTags = ForkPlusSettings.Default.FetchAllTags;
            var parts = new List<string> { "git", "fetch" };
            if (fetchAllRemotes)
            {
                parts.Add("--all");
            }
            else
            {
                Remote remote = RemoteComboBox.SelectedItem as Remote;
                if (remote == null)
                {
                    return null;
                }
                parts.Add(remote.Name);
            }
            if (allTags)
            {
                parts.Add("--tags");
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
            Remote remote = (Remote)RemoteComboBox.SelectedItem;
            bool fetchAllRemotes = FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
            bool fetchAllTags = ForkPlusSettings.Default.FetchAllTags;
            GitModule gitModule = _gitModule;

            string name = fetchAllRemotes ? Translate("Fetch all") : string.Format(Translate("Fetch '{0}'"), remote.Name);
            ForkPlusSettings.Default.Fetch_FetchAllRemotes = fetchAllRemotes;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Fetching..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult fetchResult = new FetchGitCommand().Execute(
                    gitModule, remote, fetchAllRemotes, monitor, noPrompt: false, fetchAllTags);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(fetchResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("FetchWindow onCompleted callback failed", ex);
                    }
                    Close(fetchResult);
                });
            });
        }

        // 对照 WPF: RemotesComboBox_SelectionChanged
        public void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: FetchAllRemotesCheckBox_Checked
        public void FetchAllRemotesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RemoteComboBox.IsEnabled = !FetchAllRemotesCheckBox.IsChecked.GetValueOrDefault();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RemoteComboBox.IsEnabled = false;
            FetchAllRemotesCheckBox.IsEnabled = false;
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
