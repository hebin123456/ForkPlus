using ForkPlus.UI;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 <see cref="IFileSystemDialogService"/> 实现，委托给现有 <see cref="OpenDialog"/> 静态类
	/// （内部使用 <c>Microsoft.WindowsAPICodePack.CommonOpenFileDialog/CommonSaveFileDialog</c>）。
	/// 阶段 0 仅注册，不替换现有调用点。
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
