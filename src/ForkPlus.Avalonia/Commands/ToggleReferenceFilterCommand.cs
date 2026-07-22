using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ToggleReferenceFilterCommand.cs
    // WPF: 切换侧边栏 reference 过滤器（按类型过滤显示的 branch / tag）。
    public class ToggleReferenceFilterCommand : IUICommand
    {
        public string Id => "ToggleReferenceFilter";
        public string Header => ServiceLocator.Localization.Translate("Toggle Reference Filter", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 在 sidebar 切换 reference 过滤器状态
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
