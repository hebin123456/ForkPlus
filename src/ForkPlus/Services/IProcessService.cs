namespace ForkPlus.Services
{
	/// <summary>
	/// 进程启动抽象（替换散落在 <see cref="ForkPlus.UriExtensions"/>、<see cref="ForkPlus.FileHelper"/>、
	/// <see cref="ForkPlus.UI.Commands.OpenFileInDefaultEditorCommand"/> 中的 <c>System.Diagnostics.Process.Start</c> 直接调用）。
	/// 各平台实现封装 ShellExecute / xdg-open / open 等差异。
	/// </summary>
	public interface IProcessService
	{
		/// <summary>用系统默认浏览器打开 URL（Windows: ShellExecute；Linux: xdg-open；macOS: open）。</summary>
		void OpenUrlInBrowser(string url);

		/// <summary>用系统默认关联程序打开文件（如 .txt 用记事本，.png 用图片查看器）。</summary>
		void OpenFileInDefaultApplication(string filePath);

		/// <summary>在文件管理器中打开指定目录（Windows: explorer.exe；Linux: xdg-open；macOS: open）。</summary>
		void OpenDirectoryInFileExplorer(string directoryPath);

		/// <summary>在文件管理器中打开并选中指定文件（Windows: explorer.exe /select；其他平台回退到打开所在目录）。</summary>
		void RevealFileInFileExplorer(string filePath);
	}
}
