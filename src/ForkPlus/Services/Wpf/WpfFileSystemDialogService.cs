using ForkPlus.UI;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// 阶段 4 里程碑 4.7-d：原 WPF 平台实现，现委托给已迁移到 Avalonia StorageProvider 的 <see cref="OpenDialog"/>。
	/// 类名保留 "Wpf" 前缀以避免 App.axaml.cs 注册点的大面积改动；实际底层已使用 Avalonia 原生文件对话框。
	/// </summary>
	public class WpfFileSystemDialogService : IFileSystemDialogService
	{
		public bool SelectDirectory(string title, string initialDirectory, out string directoryPath)
		{
			return OpenDialog.SelectDirectory(MainWindow.Instance, title, initialDirectory, out directoryPath);
		}

		public bool SelectFile(string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
		{
			return OpenDialog.SelectFile(MainWindow.Instance, title, initialDirectory, fileTypeName, extensionPattern, out filePath);
		}

		public bool SelectExecutableFile(string title, string initialDirectory, out string filePath)
		{
			return OpenDialog.SelectExecutableFile(MainWindow.Instance, title, initialDirectory, out filePath);
		}

		public bool SelectPatchSaveLocation(string title, string initialDirectory, string defaultFileName, out string filePath)
		{
			return OpenDialog.SelectPatchSaveLocation(MainWindow.Instance, title, initialDirectory, defaultFileName, out filePath);
		}

		public bool SelectFileSaveLocation(string title, string initialDirectory, string defaultFileName, out string resultFilePath)
		{
			return OpenDialog.SelectFileSaveLocation(MainWindow.Instance, title, initialDirectory, defaultFileName, out resultFilePath);
		}
	}
}
