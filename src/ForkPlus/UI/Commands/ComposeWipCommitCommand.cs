using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// v3.4.1：WIP 编排为提交（AI Commit Composer）独立命令。
	/// 此前菜单项复用 CommitCommand.CreateMenuItem，导致显示的快捷键文本（Ctrl+Shift+Enter）
	/// 与实际触发的 Commit 动作冲突。新建此独立命令绑定 Ctrl+Alt+Enter，并让菜单项显示正确的快捷键。
	/// </summary>
	public class ComposeWipCommitCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Compose WIP into commits...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Return, ModifierKeys.Control | ModifierKeys.Alt);

		public KeyGesture SecondaryShortcut => null;
	}
}
