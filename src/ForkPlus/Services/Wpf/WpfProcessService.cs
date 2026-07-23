using System.Diagnostics;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF/Windows 平台的 <see cref="IProcessService"/> 实现。
	/// 委托逻辑与现有 <see cref="ForkPlus.UriExtensions"/>、<see cref="ForkPlus.FileHelper"/> 一致，
	/// 阶段 0 仅注册，不替换现有调用点。
	/// </summary>
	public class WpfProcessService : IProcessService
	{
		public void OpenUrlInBrowser(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to open browser with url", ex);
			}
		}

		public void OpenFileInDefaultApplication(string filePath)
		{
			try
			{
				Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to open '" + filePath + "' in default application", ex);
			}
		}

		public void OpenDirectoryInFileExplorer(string directoryPath)
		{
			try
			{
				Process.Start(new ProcessStartInfo("explorer.exe", directoryPath) { UseShellExecute = true });
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to open directory in Explorer", ex);
			}
		}

		public void RevealFileInFileExplorer(string filePath)
		{
			try
			{
				string arguments = "/select, \"" + filePath + "\"";
				Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to show file in Explorer", ex);
			}
		}
	}
}
