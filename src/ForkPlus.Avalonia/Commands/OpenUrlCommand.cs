using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenUrlCommand.cs
    // WPF: 在系统默认浏览器中打开指定 URL（无快捷键，由其他代码调用）。
    public class OpenUrlCommand : IUICommand
    {
        public string Id => "OpenUrl";
        public string Header => "";
        public string Icon => "🔗";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new Uri(url).OpenInBrowser() —— WPF 接收 string url 参数
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
