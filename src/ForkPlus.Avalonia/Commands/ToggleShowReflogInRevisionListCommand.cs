using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleShowReflogInRevisionListCommand.cs
    // WPF: 切换 "在 revision 列表中显示 reflog 条目" 偏好开关。
    public class ToggleShowReflogInRevisionListCommand : IUICommand
    {
        public string Id => "ToggleShowReflogInRevisionList";
        public string Header => ServiceLocator.Localization.Translate("Show Reflog", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📜";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.ShowReflogInRevisionList toggle → repo.Refresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
