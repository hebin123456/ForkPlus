using System.Windows.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 撤销最近一次仓库操作（commit / reset / checkout / merge / 等）。
	/// v3.0.0 新增。
	/// 快捷键：Ctrl+Z
	/// </summary>
	public class UndoCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Undo";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Z, ModifierKeys.Control);

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			if (repositoryUserControl == null)
			{
				return;
			}
			repositoryUserControl.Undo();
		}
	}
}
