using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 TooltipRevisionDetailsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/TooltipRevisionDetailsUserControl.xaml.cs（99 行）：
    //   - UserControl 基类
    //   - 字段：_repositoryUserControl / _sha
    //   - 事件：ShowRevisionInSeparateWindowButtonClicked
    //   - 构造函数 TooltipRevisionDetailsUserControl(RepositoryUserControl, Sha)：
    //     InitializeComponent + RefreshControls
    //   - RefreshControls()：异步加载 Revision（GetRevisionHeader git 命令）→
    //     填充 AuthorAvatarImage / AuthorTextBlock / AuthorDateTextBlock / ShaTextBlock /
    //     SubjectTextBlock / ReferencePanel + 错误 fallback
    //   - GetRevisionHeader(GitModule, Sha)：调 GetRevisionsGitCommand 获取 Revision
    //   - ShowRevisionInSeparateWindowButton_Click：触发事件 + Commands.ShowRevisionInSeparateWindow
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF UserControl → Avalonia UserControl
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Collapsed/Visible → IsVisible = false/true
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //   - WPF Image.Show/Hide → IsVisible = true/false
    //   - WPF AuthorAvatarImage（自定义控件）→ spike TextBlock emoji 👤
    //   - WPF ReferencePanel（自定义控件）→ spike 不迁移（task spec 简化：Border + TextBlock）
    //   - WPF SubjectTextBlock.ApplySearchAndButrackerHighlighting → spike 纯文本
    //
    // spike 简化（task spec 关键 API）：
    //   - task spec 关键 API：SetRevision(RevisionViewModel revision)
    //   - task spec 简化：用 Border + TextBlock 显示 commit 信息
    //   - WPF 构造函数(RepositoryUserControl, Sha) → spike 无参构造 + SetRevision
    //   - WPF GetRevisionHeader git 命令 → spike 不调（数据由 SetRevision 注入）
    //   - WPF ShowRevisionInSeparateWindowButton → spike 保留按钮（回调注入）
    //   - 复用 RevisionViewModelSpike.cs 的 RevisionViewModel POCO
    public partial class TooltipRevisionDetailsUserControl : UserControl
    {
        // ===== 事件（对照 WPF: public EventHandler ShowRevisionInSeparateWindowButtonClicked）=====
        public event EventHandler ShowRevisionInSeparateWindowButtonClicked;

        // ===== 私有字段（对照 WPF）=====
        // 对照 WPF: private readonly RepositoryUserControl _repositoryUserControl
        // spike 版：父控件引用（spike 用 object 占位，对照 spike 策略 #1）
        public object RepositoryUserControl { get; private set; }

        // 对照 WPF: private readonly Sha _sha
        // spike 版：当前 revision（由 SetRevision 注入）
        public RevisionViewModel Revision { get; private set; }

        // ===== 注入回调（替代 RepositoryUserControl.Commands 依赖）=====
        // 对照 WPF: ShowRevisionInSeparateWindowButton_Click →
        //   RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(gitModule, RevisionDiffTarget.Revision(_sha))
        public Action<RevisionViewModel> ShowRevisionInSeparateWindowCallback { get; set; }

        // ===== 构造函数 =====
        public TooltipRevisionDetailsUserControl()
        {
            InitializeComponent();
            // 对照 WPF: RefreshControls()（spike 版：显示 fallback，等待 SetRevision）
            ShowFallback(Translate("Not available"));
        }

        // ===== Initialize(object)（spike 新增，注入父控件）=====
        // 对照 WPF: 构造函数注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetRevision(RevisionViewModel)（task spec 关键 API）=====
        // 对照 WPF: RefreshControls() 异步加载 GetRevisionHeader git 命令
        //   WPF: GetRevisionHeader(gitModule, _sha) → 填充各 TextBlock
        // spike 版：直接显示传入的 RevisionViewModel（不异步加载 git）
        public void SetRevision(RevisionViewModel revision)
        {
            Revision = revision;
            if (revision == null)
            {
                ShowFallback(Translate("Not available"));
                return;
            }

            // 隐藏 loading + fallback，显示 commit 详情
            // 对照 WPF: DetailsContainer.Show() + FallbackMessageTextBlock.Collapse()
            HideBusy();
            HideFallback();
            if (DetailsContainer != null) DetailsContainer.IsVisible = true;

            // 对照 WPF: AuthorTextBlock.Text = result.Author.Name
            if (AuthorTextBlock != null) AuthorTextBlock.Text = revision.Author ?? string.Empty;
            // 对照 WPF: AuthorDateTextBlock.Text = result.AuthorDate.ToString(Consts.NormalDateTimeFormat)
            if (AuthorDateTextBlock != null) AuthorDateTextBlock.Text = revision.AuthorDate.ToString("yyyy-MM-dd HH:mm:ss");
            // 对照 WPF: ShaTextBlock.Text = result.Sha.ToAbbreviatedString()
            if (ShaTextBlock != null) ShaTextBlock.Text = revision.AbbreviatedSha ?? revision.Sha ?? string.Empty;
            // 对照 WPF: SubjectTextBlock.Text = result.Message
            if (SubjectTextBlock != null) SubjectTextBlock.Text = revision.Subject ?? string.Empty;

            // spike 新增：显示 commit body（WPF 无，spike 用于显示完整 message）
            if (BodyTextBlock != null)
            {
                if (!string.IsNullOrEmpty(revision.Body))
                {
                    BodyTextBlock.Text = revision.Body;
                    BodyTextBlock.IsVisible = true;
                }
                else
                {
                    BodyTextBlock.IsVisible = false;
                }
            }

            // 对照 WPF: ReferencePanel.Refresh(list, items) → spike 不迁移（task spec 简化）
        }

        // ===== ShowLoading（对照 WPF: 无，spike 新增）=====
        public void ShowLoading()
        {
            HideFallback();
            if (DetailsContainer != null) DetailsContainer.IsVisible = false;
            if (BusyIndicator != null) BusyIndicator.IsVisible = true;
        }

        // ===== ShowFallback(string)（对照 WPF: FallbackMessageTextBlock.Show()）=====
        // 对照 WPF: DetailsContainer.Collapse() + FallbackMessageTextBlock.Show()
        public void ShowFallback(string message)
        {
            HideBusy();
            if (DetailsContainer != null) DetailsContainer.IsVisible = false;
            if (FallbackMessageTextBlock != null)
            {
                FallbackMessageTextBlock.Text = message ?? string.Empty;
                FallbackMessageTextBlock.IsVisible = true;
            }
        }

        // ===== Refresh()（对照 WPF: RefreshControls）=====
        // spike 版：显示 loading（不实际调 git，由 SetRevision 注入数据）
        public void Refresh()
        {
            if (Revision == null)
            {
                ShowLoading();
                Dispatcher.UIThread.Post(() =>
                {
                    if (Revision == null)
                    {
                        ShowFallback(Translate("Waiting for revision data..."));
                    }
                });
            }
            else
            {
                SetRevision(Revision);
            }
        }

        // ===== ShowRevisionInSeparateWindowButton_Click（对照 WPF）=====
        // 对照 WPF: private void ShowRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
        //   WPF: ShowRevisionInSeparateWindowButtonClicked?.Invoke(this, EventArgs.Empty);
        //         RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(_repositoryUserControl.GitModule, new RevisionDiffTarget.Revision(_sha))
        // spike 版：触发事件 + 调用注入的回调
        private void ShowRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
        {
            ShowRevisionInSeparateWindowButtonClicked?.Invoke(this, EventArgs.Empty);
            ShowRevisionInSeparateWindowCallback?.Invoke(Revision);
        }

        // ===== 私有辅助方法 =====
        private void HideBusy()
        {
            if (BusyIndicator != null) BusyIndicator.IsVisible = false;
        }

        private void HideFallback()
        {
            if (FallbackMessageTextBlock != null) FallbackMessageTextBlock.IsVisible = false;
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
