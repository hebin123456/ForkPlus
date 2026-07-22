using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleHideStashesInRevisionListCommand.cs
    // WPF: 切换 "在 revision 列表中隐藏 stash" 偏好开关。
    public class ToggleHideStashesInRevisionListCommand : IUICommand
    {
        public string Id => "ToggleHideStashesInRevisionList";
        public string Header => ServiceLocator.Localization.Translate("Hide Stashes in Revision List", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "👁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.HideStashesInRevisionList toggle → repo.Refresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
