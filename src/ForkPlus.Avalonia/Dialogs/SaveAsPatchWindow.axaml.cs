using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 SaveAsPatchWindow（对照 WPF SaveAsPatchWindow.xaml.cs 115 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/SaveAsPatchWindow.xaml.cs：
    //   - public partial class SaveAsPatchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule / Revision _revision / Sha _src / Sha? _dst
    //   - 构造函数 (RepositoryUserControl, GitModule, Revision, Sha?)
    //   - OnInitialized: async 加载 GetRevisionsInRangeGitCommand → RevisionsItemsControl.ItemsSource
    //   - GetCommandPreview: git format-patch <range> [-o <dir>]
    //   - OnSubmit: OpenDialog.SelectPatchSaveLocation → ExportPatchGitCommand.Execute → Close
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl → GitModule + Func<string>? defaultSourceDirProvider + Action<GitCommandResult>? onCompleted 回调
    //   3. RepositoryManager.Instance.DefaultSourceDir() → 注入 Func<string>? defaultSourceDirProvider
    //   4. OpenDialog.SelectPatchSaveLocation → TopLevel.StorageProvider.SaveFilePickerAsync
    //   5. ErrorWindow → MessageBoxWindow.ShowDialog
    //   6. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   7. spike 基类不提供 DisableEditableControls → 手动禁用
    //   8. BindableGitPointView (ItemsControl.ItemTemplate) → spike 用 ItemsControl + TextBlock 显示 SHA + message
    //   9. OnInitialized (WPF 事件) → Loaded 事件 (Avalonia 等价)
    public partial class SaveAsPatchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Func<string>? _defaultSourceDirProvider;
        private readonly Action<GitCommandResult>? _onCompleted;

        private Revision _revision;
        private Sha _src;
        private Sha? _dst;

        public SaveAsPatchWindow(
            GitModule gitModule,
            Revision revision,
            Sha? dst,
            Func<string>? defaultSourceDirProvider = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _revision = revision ?? throw new ArgumentNullException(nameof(revision));
            _src = revision.Sha;
            _dst = dst;
            _defaultSourceDirProvider = defaultSourceDirProvider;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Create Patch");
            DialogDescription = Translate("Save commit as patch");
            SubmitButtonTitle = Translate("Save");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Create Patch");

            // 对照 WPF: RevisionsTextBlock.Text = Translate(dst.HasValue ? "Revisions:" : "Revision:");
            RevisionsTextBlock.Text = Translate(dst.HasValue ? "Revisions:" : "Revision:");

            // 对照 WPF: protected override async void OnInitialized(EventArgs e)
            Loaded += SaveAsPatchWindow_Loaded;

            RefreshCommandPreview();
        }

        // 对照 WPF: OnInitialized - 异步加载 revisions
        private async void SaveAsPatchWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading..."));
                var gitCommandResult = await Task.Run(() =>
                    new GetRevisionsInRangeGitCommand().Execute(_gitModule, _src, _dst));
                if (!gitCommandResult.Succeeded)
                {
                    Close();
                    return;
                }
                SetStatus(ForkPlusDialogStatus.None, string.Empty);

                var result = gitCommandResult.Result;
                // 对照 WPF: RevisionsItemsControl.ItemsSource = result.Revisions;
                // spike 简化：把 Revision[] 转为 "{abbreviated_sha} {subject}" 字符串列表展示
                var displayItems = new List<string>();
                if (result.Revisions != null)
                {
                    foreach (var rev in result.Revisions)
                    {
                        if (rev == null) continue;
                        rev.MessageParts(out var subject, out _);
                        displayItems.Add(rev.Sha.ToAbbreviatedString() + " " + subject);
                    }
                }
                RevisionsItemsControl.ItemsSource = displayItems;

                _src = result.Src;
                _dst = result.Dst;
                RefreshCommandPreview();
            }
            catch (Exception ex)
            {
                Log.Error("OnInitialized failed", ex);
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_src == Sha.Zero)
            {
                return null;
            }
            string range = _dst.HasValue
                ? (_src.ToAbbreviatedString() + ".." + _dst.Value.ToAbbreviatedString())
                : _src.ToAbbreviatedString();
            string outputDir = ForkPlusSettings.Default.RecentPatchDirectory ?? "";
            if (!string.IsNullOrEmpty(outputDir))
            {
                string quotedDir = outputDir.IndexOf(' ') >= 0 ? ("\"" + outputDir + "\"") : outputDir;
                return "git format-patch " + range + " -o " + quotedDir;
            }
            return "git format-patch " + range;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override async void OnSubmit()
        {
            string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory
                ?? _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");
            string repositoryName = _gitModule.RepositoryName;
            string text = _dst.HasValue
                ? (repositoryName + "-" + _src.ToAbbreviatedString() + "-" + _dst.Value.ToAbbreviatedString() + Consts.Git.PatchFileExtension)
                : (repositoryName + "-" + _src.ToAbbreviatedString() + "-" + (_revision.Message ?? ""));
            text = CutInvalidCharacters(text);

            // 对照 WPF: OpenDialog.SelectPatchSaveLocation(...)
            // Avalonia: TopLevel.StorageProvider.SaveFilePickerAsync
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                Close();
                return;
            }

            var options = new FilePickerSaveOptions
            {
                Title = Translate("Save patch as..."),
                DefaultExtension = "patch",
                SuggestedFileName = text,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Git Patch")
                    {
                        Patterns = new List<string> { "*.patch" }
                    }
                }
            };

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (storageFile == null)
            {
                // 用户取消保存对话框，不关闭窗口
                return;
            }
            string filePath = storageFile.Path.LocalPath;

            // 对照 WPF: ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
            ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Saving patch..."));

            // 对照 WPF: ExportPatchGitCommand().Execute(_gitModule, _src, _dst, filePath) 同步执行
            // Avalonia: Task.Run + Dispatcher.UIThread.Post（避免阻塞 UI）
            var src = _src;
            var dst = _dst;
            await Task.Run(delegate
            {
                GitCommandResult gitCommandResult = new ExportPatchGitCommand().Execute(_gitModule, src, dst, filePath);
                Dispatcher.UIThread.Post(delegate
                {
                    if (!gitCommandResult.Succeeded)
                    {
                        // 对照 WPF: new ErrorWindow(_repositoryUserControl, gitCommandResult.Error).ShowDialog();
                        // Avalonia: MessageBoxWindow.ShowDialog
                        var errorBox = new MessageBoxWindow(
                            Translate("Create Patch"),
                            gitCommandResult.Error?.FriendlyDescription ?? "",
                            "OK",
                            showCancelButton: false);
                        _ = errorBox.ShowDialog<bool?>(this);
                        return;
                    }
                    try
                    {
                        _onCompleted?.Invoke(gitCommandResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("SaveAsPatchWindow onCompleted callback failed", ex);
                    }
                    Close(gitCommandResult);
                });
            });
        }

        // 对照 WPF: CutInvalidCharacters
        private static string CutInvalidCharacters(string text)
        {
            var sb = new StringBuilder(text);
            sb.Replace(":", "");
            return sb.ToString();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            // 本对话框无可编辑控件，仅通过 SetStatus InProgress 禁用 Submit/Cancel
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
