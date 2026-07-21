using System;
using System.ComponentModel;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionViewModel POCO（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RevisionViewModel.cs（42 行）：
    //   WPF 版继承 INotifyPropertyChanged，含：
    //   - RevisionWithFiles Revision（核心数据）
    //   - AuthorDate / Sha / AbbreviatedSha / RevisionSubject / Author 属性
    //   - FilePath / ChangedFile 属性
    //   - 构造函数 RevisionViewModel(RevisionWithFiles revision)
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF RevisionViewModel 在 ForkPlus.UI.Dialogs 命名空间（WPF 工程，Avalonia 不可访问）
    //     → 本 spike POCO 定义在 ForkPlus.Avalonia.Views.UserControls 命名空间
    //   - WPF RevisionWithFiles → Revision（来自 ForkPlus.Git Core，简化）
    //   - 保留核心属性：Sha / Author / AuthorDate / Subject / AbbreviatedSha
    //   - spike 新增 Body / Committer / CommitterDate（RevisionGraphTooltip 显示需要）
    public class RevisionViewModel : INotifyPropertyChanged
    {
        // commit SHA（对照 WPF: Sha => Revision.Sha）
        public string Sha { get; set; } = string.Empty;

        // 缩写 SHA（对照 WPF: AbbreviatedSha => Revision.Sha.ToAbbreviatedString()）
        public string AbbreviatedSha { get; set; } = string.Empty;

        // commit 主题（对照 WPF: RevisionSubject => Revision.Message）
        public string Subject { get; set; } = string.Empty;

        // commit body（spike 新增，tooltip 显示需要）
        public string Body { get; set; } = string.Empty;

        // 作者（对照 WPF: Author => Revision.Author.Name）
        public string Author { get; set; } = string.Empty;

        // 作者邮箱（spike 新增，tooltip 显示需要）
        public string AuthorEmail { get; set; } = string.Empty;

        // 作者日期（对照 WPF: AuthorDate => Revision.AuthorDate）
        public DateTime AuthorDate { get; set; }

        // 提交者（spike 新增，tooltip 显示需要）
        public string Committer { get; set; } = string.Empty;

        // 提交日期（spike 新增，tooltip 显示需要）
        public DateTime CommitterDate { get; set; }

        // 文件路径（对照 WPF: FilePath）
        public string FilePath { get; set; } = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public RevisionViewModel()
        {
        }
    }
}
