using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenRepositoryInShellToolCommand.cs
    // WPF: 在指定 Shell 工具中打开当前仓库根目录（带具体 shell 类型参数）。
    public class OpenRepositoryInShellToolCommand : IUICommand
    {
        public string Id => "OpenRepositoryInShellTool";
        public string Header => ServiceLocator.Localization.Translate("Open in Shell", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "💻";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 启动指定 Shell 工具并 cd 到 repo.GitModule.WorkingDir
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
