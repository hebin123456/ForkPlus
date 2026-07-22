using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenFileInExternalEditorCommand.cs
    // WPF: 用用户配置的外部编辑器打开当前选中的文件。
    public class OpenFileInExternalEditorCommand : IUICommand
    {
        public string Id => "OpenFileInExternalEditor";
        public string Header => ServiceLocator.Localization.Translate("Open in External Editor", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📝";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 用 ExternalProjectEditor 打开选中文件
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
