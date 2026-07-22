using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionSummaryUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionSummaryUserControl.xaml.cs（625 行）：
    //   - 公共方法：Refresh(Sha, BugtrackerLinkDefinition[], UserColors) / ApplyLocalization /
    //     HighlightSearchMatches(RevisionSearchQuery)
    //   - 公共属性：RevisionDetailsUserControl（反向引用父控件）/
    //     RepositoryUserControl（通过父控件取）
    //   - 无 Initialize 注入模式（通过公共属性 RevisionDetailsUserControl 注入）
    //
    // 装入路径（WPF）：
    //   RevisionDetailsUserControl.xaml Row 2 → RevisionSummaryUserControl（Commit tab）
    //
    // Avalonia 版差异：
    //   - WPF SelectableTextBlock → Avalonia SelectableTextBlock（API 对应）
    //   - WPF Image avatar → Avalonia Image（spike 不加载真实 avatar）
    //   - WPF ReferencePanel（自定义控件）→ ItemsControl + TextBlock
    //   - WPF PreferencesLocalization → ServiceLocator.Localization（spike 用硬编码字符串）
    //
    // spike 简化：
    //   - 用 ItemsControl 显示文件变更统计（Path / Status / Additions / Deletions）
    //   - AvatarImage 用 Avalonia Image 占位（默认不显示）
    //   - ReferencePanel 用 ItemsControl + TextBlock 占位
    //   - AiExplainCommitButton 保留（Button + emoji 文本）
    //   - DiffList 用 ItemsControl + Expander + FileDiffControl SubControlMode=True
    //   - AvatarImage 真实加载 / 搜索高亮 / 右键菜单 / AI 调用暂不实现
    public partial class RevisionSummaryUserControl : UserControl
    {
        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型 RevisionDetailsUserControl 待后续阶段补
        public object RevisionDetailsUserControl { get; set; }

        public RevisionSummaryUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: public void Refresh(Sha sha, BugtrackerLinkDefinition[] bugtrackers, UserColors userColors)
        //   主入口：刷新 Author/Committer/REFS/SHA/PARENTS/Subject/Description/FileChangeStatistics
        //   spike 版：用 object 占位参数，从传入对象反射读取属性
        public void Refresh(object sha, object bugtrackers, object userColors)
        {
            Console.WriteLine($"[RevisionSummary] Refresh: sha={sha}");

            // spike 版：更新 SHA 文本
            if (ShaTextBlock != null)
            {
                ShaTextBlock.Text = sha?.ToString() ?? "(no sha)";
            }

            // spike 版：更新文件变更统计（真实数据来自 FullRevisionDetails.ChangedFiles）
            // 暂用示例数据占位
            if (FileChangeStatistics != null)
            {
                FileChangeStatistics.Items.Clear();
                FileChangeStatistics.Items.Add(new FileChangeStatItem
                {
                    Path = "src/Program.cs",
                    Status = "Modified",
                    Additions = "+12",
                    Deletions = "-3"
                });
                FileChangeStatistics.Items.Add(new FileChangeStatItem
                {
                    Path = "README.md",
                    Status = "Added",
                    Additions = "+45",
                    Deletions = "-0"
                });
            }
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[RevisionSummary] ApplyLocalization (spike placeholder)");
        }

        // spike 新增：SetRevisionInfo(string author, string date, string sha, string subject, string message)
        //   供父控件 RevisionDetailsUserControl.SetRevision 调用，更新 Author/Date/SHA/Subject/Description 文本
        //   对照 WPF: RevisionSummaryUserControl.Refresh 内部从 FullRevisionDetails 取属性并赋值
        public void SetRevisionInfo(string author, string date, string sha, string subject, string message)
        {
            if (AuthorNameTextBlock != null) AuthorNameTextBlock.Text = author ?? "(unknown)";
            if (AuthorDateTextBlock != null) AuthorDateTextBlock.Text = date ?? "(unknown)";
            if (ShaTextBlock != null) ShaTextBlock.Text = sha ?? "(no sha)";
            if (SubjectTextBlock != null) SubjectTextBlock.Text = subject ?? "(no subject)";
            if (DescriptionTextBlock != null) DescriptionTextBlock.Text = message ?? "";
        }

        // 对照 WPF: public void HighlightSearchMatches(RevisionSearchQuery searchQuery)
        //   委托 ApplySearchAndButrackerHighlighting（spike 不迁移高亮逻辑）
        public void HighlightSearchMatches(object searchQuery)
        {
            Console.WriteLine($"[RevisionSummary] HighlightSearchMatches: {searchQuery}");
        }

        // ===== Button 事件占位（对照 WPF click handler）=====

        // 对照 WPF: AiExplainCommitButton_Click（AI 调用，Phase 5）
        private void AiExplainCommitButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RevisionSummary] AiExplainCommitButton_Click (spike placeholder, Phase 5)");
        }

        // 对照 WPF: ExpandAllButton_Click
        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RevisionSummary] ExpandAllButton_Click (spike placeholder)");
            // spike 版：展开所有 DiffList 中的 Expander
            if (DiffList != null)
            {
                foreach (var item in DiffList.Items)
                {
                    // Expander 展开逻辑（spike 占位）
                }
            }
        }
    }

    // spike POCO：文件变更统计项（Path / Status / Additions / Deletions）
    // 对照 WPF 的 ChangedFile + DiffEntry 统计数据
    public class FileChangeStatItem
    {
        public string Path { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Additions { get; set; } = string.Empty;
        public string Deletions { get; set; } = string.Empty;
    }
}
