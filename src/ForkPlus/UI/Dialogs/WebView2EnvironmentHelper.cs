using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ForkPlus.UI.Dialogs
{
	internal static class WebView2EnvironmentHelper
	{
		/// <summary>
		/// WebView2 用户数据目录（ForkData 之下），所有 WebView 宿主统一使用。
		/// v4.1.4 起不只是传给兼容层空环境的历史参数：WebView2Stub 的原生路径
		/// （NativeWebView，Windows=WebView2 引擎）经 EnvironmentRequested 把它写入
		/// WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder——不配置时
		/// WebView2 运行时对未打包 Win32 应用回落到 exe 同级 "&lt;exe名&gt;.WebView2"
		///（AI 辅助开发/AI 解释一打开就在程序目录释放 WebView2 目录的根因）。
		/// </summary>
		public static readonly string UserDataFolder = Path.Combine(App.ForkDataDirectoryPath, "WebView2");

		private static Task<CoreWebView2Environment> _environmentTask;

		public static Task<CoreWebView2Environment> GetEnvironmentAsync()
		{
			return _environmentTask ??= CreateEnvironmentAsync();
		}

		private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
		{
			Directory.CreateDirectory(UserDataFolder);
			return CoreWebView2Environment.CreateAsync(null, UserDataFolder);
		}
	}
}
