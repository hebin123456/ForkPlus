using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionsHeaderUserControl（完全对照 WPF 6b4cbff~1）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionsHeaderUserControl.xaml.cs（123 行）：
    //   - SetSubmoduleRevisions(SubmoduleDiffContent)：显示 src/dst revision 详情
    //   - SetRevisions(Revision, BugtrackerLinkDefinition[], RevisionDetails, bool)：显示 revision 对比
    //   - UpdateControls：更新 AvatarImage/AuthorTextBlock/ShaTextBlock/SubjectTextBlock 等
    //   - GetCustomLabelString：Sha → 缩写字符串
    //   - SwapRevisionsButton：交换 src/dst 顺序
    //
    // Avalonia 适配：
    //   - WPF Show()/Hide()/Collapse() → IsVisible = true/false
    //   - WPF Enable()/Disable() → IsEnabled = true/false
    //   - WPF Theme.Diff.AddedBrush/RemovedBrush → DynamicResource Diff.Added/Diff.Removed
    //   - WPF SelectableTextBlock → TextBlock（Avalonia TextBlock 默认可选）
    //   - WPF ApplySearchAndButrackerHighlighting → spike 跳过（仅设 Text）
    //   - spike 保留 IServiceProvider 构造函数（DI 兼容）
    public partial class RevisionsHeaderUserControl : UserControl
    {
        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;

        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // ===== 构造函数（spike 用 IServiceProvider，DI 兼容）=====
        public RevisionsHeaderUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== Initialize(object)（spike DI 注入）=====
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== SetSubmoduleRevisions（对照 WPF）=====
        // 显示 src/dst submodule revision 详情
        public void SetSubmoduleRevisions(SubmoduleDiffContent submoduleDiffContent)
        {
            if (submoduleDiffContent == null) return;

            OtherRevisionDetailsContainer.IsVisible = true;
            SwapRevisionsButton.IsVisible = false;

            Revision srcRevision = submoduleDiffContent.SrcRevision;
            if (srcRevision != null)
            {
                Revision dstRevision = submoduleDiffContent.DstRevision;
                if (dstRevision != null)
                {
                    UpdateControls(dstRevision, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                        OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                        OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                        OtherCustomTextBlockBorder, OtherCustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Added"));
                    UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock,
                        AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                        SubjectTextBlock, DescriptionSymbolTextBlock,
                        CustomTextBlockBorder, CustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Removed"));
                }
                else
                {
                    UpdateControls(null, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                        OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                        OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                        OtherCustomTextBlockBorder, OtherCustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Added"),
                        GetCustomLabelString(submoduleDiffContent.DstSha));
                    UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock,
                        AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                        SubjectTextBlock, DescriptionSymbolTextBlock,
                        CustomTextBlockBorder, CustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Removed"));
                }
            }
            else
            {
                Revision dstRevision2 = submoduleDiffContent.DstRevision;
                if (dstRevision2 != null)
                {
                    UpdateControls(dstRevision2, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                        OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                        OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                        OtherCustomTextBlockBorder, OtherCustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Added"));
                    UpdateControls(null, AuthorAvatarImage, AuthorTextBlock,
                        AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                        SubjectTextBlock, DescriptionSymbolTextBlock,
                        CustomTextBlockBorder, CustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Removed"),
                        GetCustomLabelString(submoduleDiffContent.SrcSha));
                }
                else
                {
                    UpdateControls(null, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                        OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                        OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                        OtherCustomTextBlockBorder, OtherCustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Added"),
                        GetCustomLabelString(submoduleDiffContent.DstSha));
                    UpdateControls(null, AuthorAvatarImage, AuthorTextBlock,
                        AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                        SubjectTextBlock, DescriptionSymbolTextBlock,
                        CustomTextBlockBorder, CustomTextBlock,
                        submoduleDiffContent.Bugtrackers, GetBrush("Diff.Removed"),
                        GetCustomLabelString(submoduleDiffContent.SrcSha));
                }
            }
        }

        // ===== SetRevisions（对照 WPF）=====
        // 显示 revision 对比（compareToWorkingDirectory 或 srcRevision vs revision）
        public void SetRevisions(Revision revision, BugtrackerLinkDefinition[] bugtrackers,
            RevisionDetails srcRevision = null, bool compareToWorkingDirectory = false)
        {
            if (revision == null) return;

            if (compareToWorkingDirectory)
            {
                OtherRevisionDetailsContainer.IsVisible = true;
                UpdateControls(revision, AuthorAvatarImage, AuthorTextBlock,
                    AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                    SubjectTextBlock, DescriptionSymbolTextBlock,
                    CustomTextBlockBorder, CustomTextBlock,
                    bugtrackers, GetBrush("Diff.Removed"));
                UpdateControls(revision, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                    OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                    OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                    OtherCustomTextBlockBorder, OtherCustomTextBlock,
                    bugtrackers, GetBrush("Diff.Added"), "Local Changes");
                SwapRevisionsButton.IsEnabled = false;
            }
            else if (srcRevision != null)
            {
                OtherRevisionDetailsContainer.IsVisible = true;
                SwapRevisionsButton.IsEnabled = true;
                UpdateControls(revision, OtherAuthorAvatarImage, OtherAuthorTextBlock,
                    OtherAuthorDateTextBlock, OtherShaTextBlock, OtherShaBackgroundBorder,
                    OtherSubjectTextBlock, OtherDescriptionSymbolTextBlock,
                    OtherCustomTextBlockBorder, OtherCustomTextBlock,
                    bugtrackers, GetBrush("Diff.Added"));
                UpdateControls(srcRevision, AuthorAvatarImage, AuthorTextBlock,
                    AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                    SubjectTextBlock, DescriptionSymbolTextBlock,
                    CustomTextBlockBorder, CustomTextBlock,
                    bugtrackers, GetBrush("Diff.Removed"));
            }
            else
            {
                OtherRevisionDetailsContainer.IsVisible = false;
                UpdateControls(revision, AuthorAvatarImage, AuthorTextBlock,
                    AuthorDateTextBlock, ShaTextBlock, ShaBackgroundBorder,
                    SubjectTextBlock, DescriptionSymbolTextBlock,
                    CustomTextBlockBorder, CustomTextBlock,
                    bugtrackers);
            }
        }

        // ===== UpdateControls（对照 WPF private static void UpdateControls）=====
        // 更新一组控件显示单个 revision 的详情
        private static void UpdateControls(
            Revision revision,
            Controls.AvatarImage authorAvatarImage,
            TextBlock authorTextBlock,
            TextBlock authorDateTextBlock,
            TextBlock shaTextBlock,
            Border shaBackgroundBorder,
            TextBlock subjectTextBlock,
            TextBlock descriptionSymbolTextBlock,
            Border customTextBlockBorder,
            TextBlock customTextBlock,
            BugtrackerLinkDefinition[] bugtrackers,
            IBrush brush = null,
            string customTextBlockText = null)
        {
            if (customTextBlockText != null)
            {
                // 自定义标签模式（如 "Local Changes"、缩写 SHA）
                customTextBlockBorder.IsVisible = true;
                authorTextBlock.IsVisible = false;
                authorDateTextBlock.IsVisible = false;
                shaTextBlock.IsVisible = false;
                shaBackgroundBorder.IsVisible = false;
                subjectTextBlock.IsVisible = false;
                authorAvatarImage.UserIdentity = new UserIdentity("", "");
                customTextBlock.Text = customTextBlockText;
                if (brush != null) customTextBlockBorder.Background = brush;
            }
            else if (revision != null)
            {
                // 正常 revision 详情模式
                customTextBlockBorder.IsVisible = false;
                authorTextBlock.IsVisible = true;
                authorDateTextBlock.IsVisible = true;
                shaTextBlock.IsVisible = true;
                shaBackgroundBorder.IsVisible = true;
                subjectTextBlock.IsVisible = true;
                authorAvatarImage.UserIdentity = revision.Author;
                authorTextBlock.Text = revision.Author.Name;
                authorDateTextBlock.Text = revision.AuthorDate.ToString(Consts.NormalDateTimeFormat);
                shaTextBlock.Text = revision.Sha.ToAbbreviatedString();
                if (brush != null) shaBackgroundBorder.Background = brush;
                revision.MessageParts(out string subject, out string description);
                subjectTextBlock.Text = subject;
                // spike 跳过 ApplySearchAndButrackerHighlighting
                ToolTip.SetTip(subjectTextBlock, revision.Message?.TrimEnd());
                descriptionSymbolTextBlock.IsVisible = !string.IsNullOrEmpty(description);
            }
            else
            {
                // null revision：隐藏所有详情
                customTextBlockBorder.IsVisible = false;
                authorTextBlock.IsVisible = false;
                authorDateTextBlock.IsVisible = false;
                shaTextBlock.IsVisible = false;
                shaBackgroundBorder.IsVisible = false;
                subjectTextBlock.IsVisible = false;
                descriptionSymbolTextBlock.IsVisible = false;
                authorAvatarImage.UserIdentity = new UserIdentity("", "");
            }
        }

        // ===== GetCustomLabelString（对照 WPF）=====
        // Sha → 缩写字符串（Sha.Zero 返回 "null"）
        private static string GetCustomLabelString(Sha sha)
        {
            if (sha != null && !(sha == Sha.Zero))
            {
                return sha.ToAbbreviatedString();
            }
            return "null";
        }

        // ===== SwapRevisionsButton_Click（spike 新增，对照 WPF SwapRevisionsButton 交换 src/dst）=====
        private void SwapRevisionsButton_Click(object sender, RoutedEventArgs e)
        {
            // spike 版：交换事件由父控件处理（对照 WPF 通过 Style 绑定 command）
            Console.WriteLine("[RevisionsHeaderUserControl] SwapRevisionsButton clicked (spike)");
        }

        // ===== GetBrush（spike 辅助：从 DynamicResource 获取画刷）=====
        // 对照 WPF Theme.Diff.AddedBrush / Theme.Diff.RemovedBrush
        private static IBrush GetBrush(string resourceKey)
        {
            if (Application.Current != null && Application.Current.TryGetResource(resourceKey, null, out object value)
                && value is IBrush brush)
            {
                return brush;
            }
            return null;
        }
    }
}
