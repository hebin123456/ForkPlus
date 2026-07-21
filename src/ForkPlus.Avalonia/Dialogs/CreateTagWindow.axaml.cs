using System;
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
    // Phase 4.40b：Avalonia 版 CreateTagWindow（真实迁移版，对照 WPF CreateTagWindow.xaml.cs 187 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CreateTagWindow.xaml.cs：
    //   - public partial class CreateTagWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / Tag[] _tags / Remote[] _remotes / IGitPoint _gitPoint
    //   - 构造函数 (GitModule gitModule, RepositoryReferences refs, Remote[] remotes, IGitPoint startPoint)
    //   - IsSubmitAllowed: tag name 非空 + ReferenceNameValidator 通过 + 不重复
    //   - GetCommandPreview: "git tag -a [-m \"msg\"] tagName commit" + 可选 push
    //   - OnSubmit: CreateTagGitCommand().Execute(...) → 可选 PushTagGitCommand().Execute(...)
    //     → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 TagNameTextBox + TagMessageTextBox + PushCheckBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   6. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   7. GitPointView 自定义控件 → spike 版用 TextBlock 显示 FriendlyName 简化
    //   8. ReferenceTextBox + ReferenceNameAutocompleteProvider → spike 版用普通 TextBox（autocomplete 暂不接入）
    //   9. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class CreateTagWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Tag[] _tags;
        private readonly Remote[] _remotes;
        private readonly IGitPoint _gitPoint;

        // 构造函数签名与 WPF 一致（除 RepositoryUserControl 已在 WPF 端无）
        public CreateTagWindow(
            GitModule gitModule,
            RepositoryReferences refs,
            Remote[] remotes,
            IGitPoint startPoint)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _tags = refs?.Tags ?? Array.Empty<Tag>();
            _remotes = remotes ?? Array.Empty<Remote>();
            _gitPoint = startPoint;

            // 对照 WPF: GitPointView.Value = startPoint;
            // Avalonia spike: 用 TextBlock 显示 FriendlyName 简化
            GitPointTextBlock.Text = startPoint?.FriendlyName ?? "";

            // 对照 WPF: DialogTitle = Translate("Create Tag");
            string title = Translate("Create Tag");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Create annotated tag at the selected point");

            // 对照 WPF: PushCheckBox.Content = Translate((remotes.Length > 1) ? "Push to all remotes" : "Push");
            PushCheckBox.Content = Translate(remotes.Length > 1 ? "Push to all remotes" : "Push");
            // 对照 WPF: PushCheckBox.IsChecked = ForkPlusSettings.Default.CreateTag_Push;
            PushCheckBox.IsChecked = ForkPlusSettings.Default.CreateTag_Push;

            RefreshButtonTitle();
            // 对照 WPF: RefreshCommandPreview()（InitializeComponent 期间 AddCommandPreview 已执行，
            // 但此时 TagNameTextBox 等控件尚未赋值，导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。）
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string? tagName = TagNameTextBox.Text?.ToLower();
                if (string.IsNullOrEmpty(tagName))
                {
                    return false;
                }
                string? error = ReferenceNameValidator.Validate(tagName);
                if (error != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, error);
                    return false;
                }
                if (_tags.AnyItem((Tag x) => x.Name.ToLower() == tagName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, "Tag '" + TagNameTextBox.Text + "' already exists");
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string? tagName = TagNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "tag", "-a" };
            string? message = TagMessageTextBox.Text;
            if (!string.IsNullOrEmpty(message))
            {
                parts.Add("-m");
                parts.Add(message.Contains(" ") ? "\"" + message + "\"" : message);
            }
            parts.Add(tagName);
            string? commit = _gitPoint?.FriendlyName;
            if (!string.IsNullOrEmpty(commit))
            {
                parts.Add(commit);
            }
            string command = string.Join(" ", parts);
            if (PushCheckBox.IsChecked.GetValueOrDefault() && _remotes != null)
            {
                foreach (Remote remote in _remotes)
                {
                    command += "\ngit push " + remote.Name + " refs/tags/" + tagName;
                }
            }
            return command;
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
            string tagName = TagNameTextBox.Text ?? string.Empty;
            string tagMessage = TagMessageTextBox.Text ?? string.Empty;
            bool push = PushCheckBox.IsChecked.GetValueOrDefault();
            // 对照 WPF: ForkPlusSettings.Default.CreateTag_Push = push; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.CreateTag_Push = push;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, push
                ? FormatTranslate("Creating '{0}' and pushing...", tagName)
                : FormatTranslate("Creating '{0}'...", tagName));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = PerformCreateTag(tagName, tagMessage, push, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    Close(result);
                });
            });
        }

        // 对照 WPF: private GitCommandResult PerformCreateTag(string tagName, string tagMessage, bool push, JobMonitor monitor)
        private GitCommandResult PerformCreateTag(string tagName, string tagMessage, bool push, JobMonitor monitor)
        {
            GitCommandResult gitCommandResult = new CreateTagGitCommand().Execute(_gitModule, tagName, tagMessage, _gitPoint, monitor);
            if (!gitCommandResult.Succeeded)
            {
                return gitCommandResult;
            }
            if (!push)
            {
                return gitCommandResult;
            }
            string tagFullReference = "refs/tags/" + tagName;
            Remote[] remotes = _remotes;
            foreach (Remote remote in remotes)
            {
                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress, FormatTranslate("Pushing '{0}' to '{1}'...", tagName, remote.Name));
                });
                GitCommandResult pushResult = new PushTagGitCommand().Execute(_gitModule, remote.Name, tagFullReference, monitor);
                if (!pushResult.Succeeded)
                {
                    return pushResult;
                }
            }
            return gitCommandResult;
        }

        // 对照 WPF: TagNameTextBox_TextChanged
        public void TagNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: TagMessageTextBox_TextChanged
        public void TagMessageTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: PushCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void PushCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshButtonTitle();
            RefreshCommandPreview();
        }

        // 对照 WPF: private void RefreshButtonTitle()
        private void RefreshButtonTitle()
        {
            SubmitButtonTitle = Translate(PushCheckBox.IsChecked.GetValueOrDefault() ? "Create and Push" : "Create");
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            TagNameTextBox.IsEnabled = false;
            TagMessageTextBox.IsEnabled = false;
            PushCheckBox.IsEnabled = false;
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
