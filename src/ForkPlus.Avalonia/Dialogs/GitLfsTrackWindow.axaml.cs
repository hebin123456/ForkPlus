using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.37b：Avalonia 版 GitLfsTrackWindow（对照 WPF GitLfsTrackWindow.xaml.cs 105 行）。
    //
    // 对照 WPF：
    //   - DelayedAction<string> _updatePreviewAction：防抖输入 0.3s
    //   - Task<GitCommandResult<string[]>> + TaskScheduler.FromCurrentSynchronizationContext() → UI 线程
    //   - GetCommandPreview override: "git lfs track pattern1 pattern2 ..."
    //
    // Avalonia 版差异：
    //   1. spike 模式：SetFooter / SetTitleTextBlock / SetDescriptionTextBlock
    //   2. spike 基类不提供 GetCommandPreview → 自行维护 CommandPreviewTextBox + RefreshCommandPreview()
    //   3. TaskScheduler.FromCurrentSynchronizationContext() → Dispatcher.UIThread.Post
    //   4. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class GitLfsTrackWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private DelayedAction<string> _updatePreviewAction;
        private readonly GitModule _gitModule;
        private readonly string _initialPattern;

        public GitLfsTrackWindow(GitModule gitModule, string initialPattern)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _initialPattern = initialPattern ?? string.Empty;

            // DelayedAction 延迟 0.3s 触发预览刷新（避免每次键入都跑 git lfs ls-files）
            _updatePreviewAction = new DelayedAction<string>(UpdatePreview, 0.3);

            DialogTitle = Translate("Add tracking patterns to Git LFS");
            DialogDescription = Translate("Add file path patterns to .gitattributes");
            SubmitButtonTitle = Translate("Track");
            PatternLabelTextBlock.Text = Translate("(one pattern per line)");
            PreviewLabelTextBlock.Text = Translate("0 files match");
            PatternTextBox.Text = _initialPattern;

            // 立即触发一次预览（对应 WPF _updatePreviewAction.InvokeNow）
            _updatePreviewAction.InvokeNow(_initialPattern);

            // 刷新命令预览（spike 版需手动维护）
            RefreshCommandPreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(PatternTextBox.Text);

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            string text = PatternTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            string[] patterns = text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            return "git lfs track " + string.Join(" ", patterns);
        }

        // spike 版：手动刷新命令预览文本（基类无 RefreshCommandPreview）
        private void RefreshCommandPreview()
        {
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
            CommandPreviewTextBox.IsVisible = !string.IsNullOrEmpty(preview);
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            string[] patterns = PatternTextBox.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            GitCommandResult gitResult = new AddGitLfsTrackPatternGitCommand().Execute(_gitModule, patterns);
            Close(gitResult);
        }

        // 对照 WPF: PatternTextBox_TextChanged
        private void PatternTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _updatePreviewAction.InvokeWithDelay(PatternTextBox.Text);
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: UpdatePreview(string pattern)
        private void UpdatePreview(string pattern)
        {
            string[] patterns = pattern.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            SetStatus(ForkPlusDialogStatus.InProgress, string.Empty);

            // WPF: Task + TaskScheduler.FromCurrentSynchronizationContext()
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            Task.Run(delegate
            {
                GitCommandResult<string[]> result = new GitLfsGetPreviewFilesGitCommand().Execute(_gitModule, patterns);
                Dispatcher.UIThread.Post(delegate
                {
                    // 如果用户已修改输入，跳过本次结果（对照 WPF: if (!(PatternTextBox.Text != pattern))）
                    if (PatternTextBox.Text != pattern)
                    {
                        return;
                    }
                    SetStatus(ForkPlusDialogStatus.None, string.Empty);

                    string text;
                    string label;
                    if (result.Succeeded)
                    {
                        string[] files = result.Result;
                        text = string.Join(Environment.NewLine, files);
                        label = files.Length == 1
                            ? Translate("1 file matches")
                            : string.Format(Translate("{0} files match"), files.Length);
                    }
                    else
                    {
                        text = string.Empty;
                        label = Translate("0 files match");
                    }
                    PreviewTextBox.Text = text;
                    PreviewLabelTextBlock.Text = label;
                });
            });
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
