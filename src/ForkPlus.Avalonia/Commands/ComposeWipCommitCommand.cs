using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ComposeWipCommitCommand.cs
    // WPF: v3.4.1 WIP 编排为提交（AI Commit Composer），独立快捷键 Ctrl+Alt+Enter。
    public class ComposeWipCommitCommand : IUICommand
    {
        public string Id => "ComposeWipCommit";
        public string Header => ServiceLocator.Localization.Translate("Compose WIP into commits...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🤖";
        public string ShortcutText => "Ctrl+Alt+Enter";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 打开 AiCommitComposerWindow 编排 WIP 为多个提交
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
