using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RunExternalDiffToolCommand.cs
    // WPF: 启动用户配置的外部 Diff 工具（如 Beyond Compare）比较两个 revision / 文件。
    public class RunExternalDiffToolCommand : IUICommand
    {
        public string Id => "RunExternalDiffTool";
        public string Header => ServiceLocator.Localization.Translate("Open in Diff Tool", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 启动 git difftool（用户配置的 DiffTool）
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
