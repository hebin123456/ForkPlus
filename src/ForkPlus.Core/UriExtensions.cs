using System;
using System.Diagnostics;

namespace ForkPlus
{
    /// <summary>
    /// Uri 扩展方法。Phase 4.17b 改为 public（原 internal），供 Avalonia 工程的
    /// UpdateAvailableWindow / AboutDialogWindow 等使用 OpenInBrowser。
    /// </summary>
    public static class UriExtensions
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
