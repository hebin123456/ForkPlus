using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UpdateReferenceFilterCommand.cs
    // WPF: 用新文本更新侧边栏 reference 过滤器（输入框文本变化时触发）。
    public class UpdateReferenceFilterCommand : IUICommand
    {
        public string Id => "UpdateReferenceFilter";
        public string Header => ServiceLocator.Localization.Translate("Filter References", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: sidebar.ReferenceFilter = newText → 刷新可见性
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
