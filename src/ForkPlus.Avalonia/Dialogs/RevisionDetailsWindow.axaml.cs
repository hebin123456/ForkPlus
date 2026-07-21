using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.41b：Avalonia 版 RevisionDetailsWindow（真实迁移版，对照 WPF RevisionDetailsWindow.xaml.cs 107 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RevisionDetailsWindow.xaml.cs：
    //   - public partial class RevisionDetailsWindow : CustomWindow
    //   - 字段: GitModule _gitModule, bool _startUpFinished
    //   - 构造函数 (RepositoryUserControl repositoryUserControl, GitModule gitModule, RevisionDiffTarget target, string fileToSelect)
    //   - RevisionDetails.Initialize + ShowRevisionDetails(target, fileToSelect)
    //   - RevisionDetailsUpdated → RefreshTitle（SHA + subject）
    //   - OnSourceInitialized / OnLocationChanged / Window_SizeChanged / Window_Activated
    //     → ForkPlusSettings.Default.RevisionWindowLocationState 持久化
    //   - OnKeyDown: ESC → Close
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl + RevisionDiffTarget 参数 → 注入 GitModule + Sha（spike 简化）
    //   3. RevisionDetailsUserControl 嵌入式 UserControl → spike 版用静态 Grid 显示 SHA/Author/Date/Message/Parents
    //   4. RevisionDetailsUpdated → RefreshTitle → spike 版直接在构造函数中调 GetRevisionDetailsGitCommand 拿 RevisionDetails
    //   5. ForkPlusSettings.RevisionWindowLocationState 持久化跳过（WPF-only WindowLocationState）
    //   6. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   7. ESC 关闭由基类 ForkPlusDialogWindow 处理
    //   8. 只读对话框：ShowSubmitButton = false（只保留 Close 按钮）
    public partial class RevisionDetailsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Sha _sha;

        public RevisionDetailsWindow(GitModule gitModule, Sha sha)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _sha = sha;

            // 对照 WPF: ShowInTaskbar = true; WindowStartupLocation = CenterScreen;
            // spike 版用 CenterOwner（spike 通常作为子窗口）

            // 对照 WPF: base.Title = PreferencesLocalization.Current("Revision Details")
            string title = Translate("Revision Details");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Show details of the selected commit.");
            ShowSubmitButton = false;
            CancelButtonTitle = Translate("Close");

            LoadRevisionDetails();
        }

        // 对照 WPF: RevisionDetails.ShowRevisionDetails(target, fileToSelect) + RevisionDetailsUpdated → RefreshTitle
        // spike 版：直接同步调 GetRevisionDetailsGitCommand().Execute(gitModule, sha, monitor)
        private void LoadRevisionDetails()
        {
            JobMonitor monitor = new JobMonitor();
            GitCommandResult<RevisionDetails> result = new GetRevisionDetailsGitCommand().Execute(_gitModule, _sha, monitor);
            if (!result.Succeeded || result.Result == null)
            {
                ShaTextBox.Text = _sha.ToAbbreviatedString();
                AuthorNameTextBlock.Text = "";
                AuthorEmailTextBlock.Text = "";
                AuthorDateTextBlock.Text = "";
                CommitterNameTextBlock.Text = "";
                CommitterDateTextBlock.Text = "";
                ParentsTextBox.Text = "";
                MessageTextBox.Text = "";
                return;
            }

            RevisionDetails details = result.Result;
            RefreshTitle(details);

            ShaTextBox.Text = details.Sha.ToString();
            AuthorNameTextBlock.Text = details.Author?.Name ?? "";
            AuthorEmailTextBlock.Text = details.Author?.Email ?? "";
            AuthorDateTextBlock.Text = details.AuthorDate.ToLocalTime().ToString(Consts.FullDateTimeFormat);
            CommitterNameTextBlock.Text = details.Committer?.Name ?? "";
            CommitterDateTextBlock.Text = details.CommitterDate.ToLocalTime().ToString(Consts.FullDateTimeFormat);
            ParentsTextBox.Text = details.Parents != null && details.Parents.Length > 0
                ? string.Join(", ", details.Parents.Select(p => p.ToAbbreviatedString()))
                : "";
            MessageTextBox.Text = details.Message ?? "";
        }

        // 对照 WPF: private void RefreshTitle(RevisionDetails revisionDetails)
        private void RefreshTitle(RevisionDetails revisionDetails)
        {
            revisionDetails.MessageParts(out var subject, out var _);
            string newTitle = revisionDetails.Sha.ToAbbreviatedString() + " " + subject;
            Title = newTitle;
            DialogTitle = newTitle;
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
