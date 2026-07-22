using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenRepositoryInExternalEditorCommand.cs
    // WPF: 用用户配置的外部编辑器（VSCode 等）打开当前仓库根目录。
    public class OpenRepositoryInExternalEditorCommand : IUICommand
    {
        public string Id => "OpenRepositoryInExternalEditor";
        public string Header => ServiceLocator.Localization.Translate("Open in External Editor", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🛠";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 用 ExternalRepositoryEditor 打开 repo.GitModule.WorkingDir
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
