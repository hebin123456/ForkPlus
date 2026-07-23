using System;
using System.Diagnostics;

namespace ForkPlus
{
	internal static class UriExtensions
	{
		public static void OpenInBrowser(this Uri url)
		{
			try
			{
				// .NET Core/.NET 5+ 起 UseShellExecute 默认为 false，不会用系统 shell
				// 唤起默认浏览器。迁移到 .NET 10 后此处遗漏导致"点下载按钮不跳转"。
				// 显式置 true 恢复 .NET Framework 时代的行为。
				Process.Start(new ProcessStartInfo(url.OriginalString) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open browser with url", ex);
			}
		}
	}
}
