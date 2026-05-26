using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ForkPlus.UI.Dialogs
{
	internal static class WebView2EnvironmentHelper
	{
		private static Task<CoreWebView2Environment> _environmentTask;

		public static Task<CoreWebView2Environment> GetEnvironmentAsync()
		{
			return _environmentTask ??= CreateEnvironmentAsync();
		}

		private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
		{
			string userDataFolder = Path.Combine(App.ForkDataDirectoryPath, "WebView2");
			Directory.CreateDirectory(userDataFolder);
			return CoreWebView2Environment.CreateAsync(null, userDataFolder);
		}
	}
}
