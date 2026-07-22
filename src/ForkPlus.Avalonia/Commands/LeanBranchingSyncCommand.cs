using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/LeanBranchingSyncCommand.cs
    // WPF: LeanBranching.StartSync(...) + NextSyncStep 循环 — 精简分支同步流程。
    public class LeanBranchingSyncCommand : IUICommand
    {
        public string Id => "LeanBranchingSync";
        public string Header => ServiceLocator.Localization.Translate("Sync", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: LeanBranching.StartSync(localMain, mainBranch, activeBranch, upstream, submodules, monitor)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
