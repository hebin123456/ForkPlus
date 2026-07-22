using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowFileInFileTreeCommand.cs
    // WPF: 在 revision 详情面板的文件树中定位到选中的文件。
    public class ShowFileInFileTreeCommand : IUICommand
    {
        public string Id => "ShowFileInFileTree";
        public string Header => ServiceLocator.Localization.Translate("Show in File Tree", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🌲";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 滚动并选中 revision 文件树中的指定文件
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
