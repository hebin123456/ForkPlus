using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAiResultWindowCommand.cs
    // WPF: 弹出 AiResultWindow 显示 AI 生成结果（commit message / code review 等）。
    public class ShowAiResultWindowCommand : IUICommand
    {
        public string Id => "ShowAiResultWindow";
        public string Header => ServiceLocator.Localization.Translate("AI Result", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🤖";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AiTextResultWindow().ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}
