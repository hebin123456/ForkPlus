using Avalonia.Input;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowAiResultWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Ai Result...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, [Null] AiAgent aiAgent = null)
		{
			// v3.10.3：弹窗行为统一走 AiWindowService（非模态 + owner=主窗口 + 任务栏 + 跟随主窗口所在屏幕）。
			// agent 路径（如 Claude CLI）不依赖 OpenAI 配置，不做未配置拦截。
			if (aiAgent == null && !AiWindowService.EnsureAiConfigured())
			{
				return;
			}
			AiWindowService.Show(new AiCodeReviewWindow(repositoryUserControl, target, aiAgent));
		}
	}
}
