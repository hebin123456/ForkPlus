using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/SwitchRevisionListOrientationCommand.cs
    // WPF: 切换 revision 列表的布局方向（横向 / 纵向）。
    public class SwitchRevisionListOrientationCommand : IUICommand
    {
        public string Id => "SwitchRevisionListOrientation";
        public string Header => ServiceLocator.Localization.Translate("Switch Layout", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.RevisionListLayout toggle → repo 刷新布局
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
