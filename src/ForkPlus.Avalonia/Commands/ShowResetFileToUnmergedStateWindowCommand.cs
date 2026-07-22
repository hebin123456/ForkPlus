using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowResetFileToUnmergedStateWindowCommand.cs
    // WPF: 弹出 ResetFileToUnmergedStateWindow 把冲突文件重置回 unmerged 状态（merge 时使用）。
    public class ShowResetFileToUnmergedStateWindowCommand : IUICommand
    {
        public string Id => "ShowResetFileToUnmergedStateWindow";
        public string Header => ServiceLocator.Localization.Translate("Reset to Unmerged State...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 把 file 重置回 unmerged 状态（git checkout --merge / 自定义逻辑）
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
