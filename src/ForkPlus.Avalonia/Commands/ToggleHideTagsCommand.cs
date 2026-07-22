using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleHideTagsCommand.cs
    // WPF: 切换 "在 revision 列表中隐藏 tag" 偏好开关。
    public class ToggleHideTagsCommand : IUICommand
    {
        public string Id => "ToggleHideTags";
        public string Header => ServiceLocator.Localization.Translate("Hide Tags", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "👁";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.HideTags toggle → repo.Refresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
