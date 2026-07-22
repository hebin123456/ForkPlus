using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenRepositoryInDefaultShellToolCommand.cs
    // WPF: 在系统默认 Shell 工具中打开当前仓库根目录。
    public class OpenRepositoryInDefaultShellToolCommand : IUICommand
    {
        public string Id => "OpenRepositoryInDefaultShellTool";
        public string Header => ServiceLocator.Localization.Translate("Open in Default Shell", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "💻";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 启动默认 Shell 工具并 cd 到 repo.GitModule.WorkingDir
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
