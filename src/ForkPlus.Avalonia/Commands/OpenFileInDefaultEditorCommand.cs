using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenFileInDefaultEditorCommand.cs
    // WPF: 用系统默认编辑器打开当前选中的文件。
    public class OpenFileInDefaultEditorCommand : IUICommand
    {
        public string Id => "OpenFileInDefaultEditor";
        public string Header => ServiceLocator.Localization.Translate("Open in Default Editor", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 调用系统默认编辑器打开 repo.GitModule 下的选中文件
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
