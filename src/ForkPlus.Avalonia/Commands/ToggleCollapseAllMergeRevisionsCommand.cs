using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleCollapseAllMergeRevisionsCommand.cs
    // WPF: 在 revision 列表中折叠 / 展开所有 merge commit。
    public class ToggleCollapseAllMergeRevisionsCommand : IUICommand
    {
        public string Id => "ToggleCollapseAllMergeRevisions";
        public string Header => ServiceLocator.Localization.Translate("Collapse All Merges", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📂";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.CollapseAllMerges() / repo.ExpandAllMerges()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
