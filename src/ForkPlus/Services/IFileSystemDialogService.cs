namespace ForkPlus.Services
{
	/// <summary>
	/// 文件/目录选择对话框抽象（替换 WPF <c>Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog</c> /
	/// <c>CommonSaveFileDialog</c>，即 <see cref="ForkPlus.UI.OpenDialog"/> 静态类）。
	/// Avalonia 实现将使用 <c>StorageProvider.OpenFilePickerAsync</c> / <c>SaveFilePickerAsync</c>。
	/// </summary>
	public interface IFileSystemDialogService
	{
		/// <summary>选择目录。返回是否选择了（true 则 <paramref name="directoryPath"/> 有值）。</summary>
		bool SelectDirectory(string title, string initialDirectory, out string directoryPath);

		/// <summary>选择任意文件。</summary>
		bool SelectFile(string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath);

		/// <summary>选择可执行文件（过滤器固定为 *.exe）。</summary>
		bool SelectExecutableFile(string title, string initialDirectory, out string filePath);

		/// <summary>选择 patch 保存位置（默认扩展名 .patch）。</summary>
		bool SelectPatchSaveLocation(string title, string initialDirectory, string defaultFileName, out string filePath);

		/// <summary>选择任意文件保存位置（根据 defaultFileName 扩展名决定过滤器）。</summary>
		bool SelectFileSaveLocation(string title, string initialDirectory, string defaultFileName, out string resultFilePath);
	}
}
