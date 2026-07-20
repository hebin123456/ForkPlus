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
				Process.Start(new ProcessStartInfo(url.OriginalString));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open browser with url", ex);
			}
		}
	}
}
