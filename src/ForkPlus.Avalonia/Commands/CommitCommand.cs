using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CommitCommand.cs（195 行）
    // WPF: 提交暂存区（CommitGitCommand），处理 sequencer/am/rebase in-progress 继续 + commit-and-push。
    // spike: 核心逻辑在 CommitGitCommand + CommitUserControl 视图层，命令仅暴露 Execute 签名。
    public class CommitCommand : IUICommand
    {
        public string Id => "Commit";
        public string Header => ServiceLocator.Localization.Translate("Commit", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✔";
        public string ShortcutText => "Ctrl+Shift+Enter";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: CommitGitCommand.Execute(gitModule, message, amend, commitAndPush, monitor)
            //        → 处理 SequencerInProgress / AmInProgress / RebaseInProgress 继续
            //        → commit-and-push 时调 QuickPush.Execute
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
