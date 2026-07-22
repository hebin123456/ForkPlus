using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RunExternalMergeToolCommand.cs
    // WPF: 启动用户配置的外部 Merge 工具解决冲突文件。
    public class RunExternalMergeToolCommand : IUICommand
    {
        public string Id => "RunExternalMergeTool";
        public string Header => ServiceLocator.Localization.Translate("Open in Merge Tool", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 启动 git mergetool（用户配置的 MergeTool）
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
