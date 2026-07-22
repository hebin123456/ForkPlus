using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowNewYearNotification.cs
    // WPF: 新年彩蛋。WPF 中 Title = null（不显示菜单项，仅程序化触发）。
    // spike: 实现 IForkPlusCommand（无 Icon / ShortcutText），Header 留空。
    public class ShowNewYearNotification : IForkPlusCommand
    {
        public string Id => "ShowNewYearNotification";
        public string Header => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ForkPlusSettings.Default.SeenNewYear2026 = true → JobQueue 投递动画
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo != null;
    }
}
