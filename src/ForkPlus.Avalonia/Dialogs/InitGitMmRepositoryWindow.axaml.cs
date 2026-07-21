using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.38b：Avalonia 版 InitGitMmRepositoryWindow（对照 WPF InitGitMmRepositoryWindow.xaml.cs 210 行）。
    //
    // 对照 WPF：
    //   - RepositoryManager.Instance.DefaultSourceDir() → 注入 Func<string>? defaultSourceDirProvider
    //   - Application.Current.TabManager().OpenRepository(path) → 注入 Action<string>? onRepositoryInitialized
    //   - OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync
    //   - MessageBoxWindow.ShowDialog() → await ShowDialog<bool?>(owner)
    //   - GitMmCommandPreviewHelper.Format(args) → 复用已迁移到 Avalonia 的同名 helper
    //
    // spike 模式：
    //   - 4 行 Grid = Header / Description / Content / Footer
    //   - CommandPreviewTextBox 手动维护
    //   - DisableEditableControls 手动禁用 6 个 TextBox
    public partial class InitGitMmRepositoryWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly Func<string> _defaultSourceDirProvider;
        private readonly Action<string> _onRepositoryInitialized;

        public InitGitMmRepositoryWindow(
            Func<string> defaultSourceDirProvider = null,
            Action<string> onRepositoryInitialized = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _defaultSourceDirProvider = defaultSourceDirProvider;
            _onRepositoryInitialized = onRepositoryInitialized;

            DialogTitle = Translate("Initialize git mm Repository");
            DialogDescription = Translate("Initialize a git mm workspace from a manifest repository");
            SubmitButtonTitle = Translate("Initialize");

            // 对照 WPF: ParentDirectoryTextBox.Text = RepositoryManager.Instance.DefaultSourceDir()
            ParentDirectoryTextBox.Text = _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");

            RestoreDefaults();
            UpdateSubmitButton();
            RefreshCommandPreview();

            // 对照 WPF: Loaded += delegate { Dispatcher.BeginInvoke(RefreshCommandPreview, Loaded); ManifestUrlTextBox.Focus(); }
            Loaded += (_, _) =>
            {
                Dispatcher.UIThread.Post(RefreshCommandPreview);
                ManifestUrlTextBox.Focus();
            };
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed =>
            !string.IsNullOrWhiteSpace(ManifestUrlTextBox.Text)
            && !string.IsNullOrWhiteSpace(ParentDirectoryTextBox.Text)
            && !string.IsNullOrWhiteSpace(RepositoryNameTextBox.Text);

        // 对照 WPF: protected override async void OnSubmit()
        protected override async void OnSubmit()
        {
            try
            {
                string url = ManifestUrlTextBox.Text.Trim();
                string parentDirectory = ParentDirectoryTextBox.Text.Trim();
                string repositoryName = RepositoryNameTextBox.Text.Trim();
                string destinationDirectory = Path.Combine(parentDirectory, repositoryName);
                string manifest = string.IsNullOrWhiteSpace(ManifestFileTextBox.Text) ? "dependency.xml" : ManifestFileTextBox.Text.Trim();
                string branch = string.IsNullOrWhiteSpace(ManifestBranchTextBox.Text) ? "master" : ManifestBranchTextBox.Text.Trim();
                string group = string.IsNullOrWhiteSpace(ManifestGroupTextBox.Text) ? "default" : ManifestGroupTextBox.Text.Trim();

                if (!await ValidateDestination(destinationDirectory))
                {
                    return;
                }
                SaveDefaults(url, manifest, branch, group);
                DisableEditableControls();
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Initializing git mm repository..."));

                GitRequestResult result = await Task.Run(() => RunInit(destinationDirectory, url, manifest, branch, group));

                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                EnableEditableControls();

                if (!result.Success)
                {
                    var msgBox = new MessageBoxWindow(
                        "git mm init failed",
                        result.FullReadableOutput(),
                        "Close",
                        "Cancel",
                        showCancelButton: false);
                    await msgBox.ShowDialog<bool?>(this);
                    return;
                }

                // 对照 WPF: Application.Current.TabManager().OpenRepository(destinationDirectory)
                _onRepositoryInitialized?.Invoke(destinationDirectory);
                Close();
            }
            catch (Exception ex)
            {
                Log.Error("OnSubmit failed", ex);
            }
        }

        // 对照 WPF: ValidateDestination
        private async System.Threading.Tasks.Task<bool> ValidateDestination(string destinationDirectory)
        {
            try
            {
                if (Directory.Exists(destinationDirectory))
                {
                    if (IsGitMmWorkspace(destinationDirectory))
                    {
                        var msgBox = new MessageBoxWindow(
                            "Already a git mm repository",
                            string.Format(Translate("'{0}' is already a git mm repository."), destinationDirectory),
                            "Close", "Cancel", showCancelButton: false);
                        await msgBox.ShowDialog<bool?>(this);
                        return false;
                    }
                    if (Directory.GetFileSystemEntries(destinationDirectory).Length > 0)
                    {
                        var msgBox = new MessageBoxWindow(
                            "Destination folder is not empty",
                            string.Format(Translate("'{0}' already exists. Please choose another name."), destinationDirectory),
                            "Close", "Cancel", showCancelButton: false);
                        await msgBox.ShowDialog<bool?>(this);
                        return false;
                    }
                }
                else
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                return true;
            }
            catch (Exception ex)
            {
                var msgBox = new MessageBoxWindow(
                    "Failed to create destination folder",
                    ex.Message,
                    "Close", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return false;
            }
        }

        // 对照 WPF: GitMmUserControl.IsGitMmWorkspace
        private static bool IsGitMmWorkspace(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return Directory.Exists(Path.Combine(path, ".repo"))
                || Directory.Exists(Path.Combine(path, ".mm"));
        }

        // 对照 WPF: RunInit
        private GitRequestResult RunInit(string destinationDirectory, string url, string manifest, string branch, string group)
        {
            GitCommand command = new GitCommand("mm");
            command.AddRange("init", "-u", url, "-m", manifest, "-b", branch, "-g", group);
            return default(GitRequest)
                .CurrentDir(destinationDirectory)
                .Command(command)
                .Env(new[] { ("GIT_TERMINAL_PROMPT", "0") })
                .Execute(new JobMonitor());
        }

        // 对照 WPF: BrowseButton_Click
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string initialDirectory = _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = Translate("Select location"),
            };
            if (Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result != null && result.Count > 0)
            {
                ParentDirectoryTextBox.Text = result[0].Path.LocalPath;
                ParentDirectoryTextBox.Focus();
            }
        }

        // 对照 WPF: TextBox_TextChanged
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RestoreDefaults
        private void RestoreDefaults()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            ManifestUrlTextBox.Text = settings.InitUrl ?? "";
            ManifestFileTextBox.Text = settings.InitManifest;
            ManifestBranchTextBox.Text = settings.InitBranch;
            ManifestGroupTextBox.Text = settings.InitGroup;
            RefreshCommandPreview();
        }

        // 对照 WPF: RefreshCommandPreview
        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string cmd = GitMmCommandPreviewHelper.Format(CreateInitArgs());
            CommandPreviewTextBox.Text = cmd;
            ToolTip.SetTip(CommandPreviewTextBox, cmd);
        }

        // 对照 WPF: CreateInitArgs
        private string[] CreateInitArgs()
        {
            string url = ManifestUrlTextBox.Text.Trim();
            string manifest = string.IsNullOrWhiteSpace(ManifestFileTextBox.Text) ? "dependency.xml" : ManifestFileTextBox.Text.Trim();
            string branch = string.IsNullOrWhiteSpace(ManifestBranchTextBox.Text) ? "master" : ManifestBranchTextBox.Text.Trim();
            string group = string.IsNullOrWhiteSpace(ManifestGroupTextBox.Text) ? "default" : ManifestGroupTextBox.Text.Trim();
            return new[] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group };
        }

        // 对照 WPF: SaveDefaults
        private void SaveDefaults(string url, string manifest, string branch, string group)
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
                settings.Workspaces ?? Array.Empty<string>(),
                settings.ActiveWorkspace,
                settings.ActiveSubrepo,
                settings.ActiveSubrepos,
                settings.SubrepoOrders,
                settings.VisibleSubrepos,
                settings.CommandOutputCollapsed,
                settings.CommandOutputHeight,
                settings.CommandHistory,
                settings.UploadLinks,
                settings.UploadLinksByWorkspace,
                settings.SyncJobs,
                settings.StartBranch,
                url,
                manifest,
                branch,
                group,
                settings.DialogOptions);
            ForkPlusSettings.Default.Save();
        }

        // spike 版：手动禁用所有可编辑控件
        private void DisableEditableControls()
        {
            ManifestUrlTextBox.IsEnabled = false;
            ParentDirectoryTextBox.IsEnabled = false;
            RepositoryNameTextBox.IsEnabled = false;
            ManifestFileTextBox.IsEnabled = false;
            ManifestBranchTextBox.IsEnabled = false;
            ManifestGroupTextBox.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            ManifestUrlTextBox.IsEnabled = true;
            ParentDirectoryTextBox.IsEnabled = true;
            RepositoryNameTextBox.IsEnabled = true;
            ManifestFileTextBox.IsEnabled = true;
            ManifestBranchTextBox.IsEnabled = true;
            ManifestGroupTextBox.IsEnabled = true;
        }

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
