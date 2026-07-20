using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的对话框服务抽象。
	///
	/// 替换以下 WPF/Windows-only 调用：
	/// - System.Windows.MessageBox.Show（49 处调用）
	/// - ForkPlus.UI.Dialogs.ErrorWindow（多处）
	/// - Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog（4 处，全在 UI/OpenDialog.cs）
	/// - CommonOpenFolderDialog
	///
	/// 实施策略（Phase 0.5+）：
	/// 1. 主工程实现 WpfDialogService，包装现有 MessageBox.Show / ErrorWindow / CommonOpenFileDialog。
	/// 2. ServiceLocator.Initialize 时注入实例。
	/// 3. 业务层通过 ServiceLocator.Dialogs.ShowMessage(...) 调用。
	/// 4. UI 层暂保留直接调用（迁移到 Avalonia 时再统一改）。
	/// </summary>
	public interface IDialogService
	{
		/// <summary>
		/// 显示错误窗口（替代 ErrorWindow）。
		/// </summary>
		void ShowError(string title, string message, Exception exception = null);

		/// <summary>
		/// 显示消息框（替代 System.Windows.MessageBox.Show）。
		/// </summary>
		MessageBoxResult ShowMessage(
			string message,
			string title = "",
			MessageBoxButton buttons = MessageBoxButton.OK,
			MessageBoxImage icon = MessageBoxImage.Information);

		/// <summary>
		/// 显示文件选择对话框（替代 CommonOpenFileDialog）。
		/// 返回选中的文件路径数组；用户取消时返回空数组。
		/// </summary>
		string[] ShowOpenFileDialog(
			string title,
			string filter = null,
			bool multiselect = false,
			string initialDirectory = null);

		/// <summary>
		/// 显示文件夹选择对话框。
		/// 返回选中的文件夹路径；用户取消时返回 null。
		/// </summary>
		string ShowOpenFolderDialog(string title, string initialDirectory = null);
	}

	/// <summary>
	/// 消息框按钮选项（对应 WPF MessageBoxButton，但定义为平台无关枚举）。
	/// </summary>
	public enum MessageBoxButton
	{
		OK,
		OKCancel,
		YesNo,
		YesNoCancel
	}

	/// <summary>
	/// 消息框图标选项（对应 WPF MessageBoxImage）。
	/// </summary>
	public enum MessageBoxImage
	{
		None,
		Information,
		Warning,
		Error,
		Question
	}

	/// <summary>
	/// 消息框返回值（对应 WPF MessageBoxResult）。
	/// </summary>
	public enum MessageBoxResult
	{
		None,
		OK,
		Cancel,
		Yes,
		No
	}
}
