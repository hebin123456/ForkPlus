using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowPushWindowCommand.cs
    // WPF: 弹出 PushWindow push 当前分支到远端（git push）。Ctrl+Shift+P。
    public class ShowPushWindowCommand : IUICommand
    {
        public string Id => "ShowPushWindow";
        public string Header => ServiceLocator.Localization.Translate("Push...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⬆";
        public string ShortcutText => "Ctrl+Shift+P";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 打开 PushWindow 对话框
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}
