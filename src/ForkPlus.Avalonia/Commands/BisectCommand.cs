using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/BisectCommand.cs
    // WPF: 执行 git bisect start/skip/reset/bad/good（RepositoryUserControl + BisectGitCommand.BisectCommand）。
    // spike: 核心逻辑在 BisectGitCommand，命令仅暴露 Execute 签名。
    public class BisectCommand : IUICommand
    {
        public string Id => "Bisect";
        public string Header => ServiceLocator.Localization.Translate("Bisect", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔬";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: BisectGitCommand.Execute(gitModule, bisectCommand, monitor) → InvalidateAndRefresh(Status|Head|References)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
