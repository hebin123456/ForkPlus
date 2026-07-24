using Avalonia.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 重做最近被撤销的操作。
	/// v3.0.0 新增。
	/// 快捷键：Ctrl+Shift+Z（兼容 Ctrl+Y）
	/// </summary>
	public class RedoCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Redo";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);

		public KeyGesture SecondaryShortcut { get; } = new KeyGesture(Key.Y, KeyModifiers.Control);

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			if (repositoryUserControl == null)
			{
				return;
			}
			repositoryUserControl.Redo();
		}
	}
}
