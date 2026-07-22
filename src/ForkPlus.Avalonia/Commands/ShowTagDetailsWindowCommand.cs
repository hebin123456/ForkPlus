using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowTagDetailsWindowCommand.cs
    // WPF: 弹出 TagDetailsWindow 显示 tag 详情（标签名 / 注解 / 指向 revision / 签名）。
    public class ShowTagDetailsWindowCommand : IUICommand
    {
        public string Id => "ShowTagDetailsWindow";
        public string Header => ServiceLocator.Localization.Translate("Tag Details", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🏷";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new TagDetailsWindow(repo, tag).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
