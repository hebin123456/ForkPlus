using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public static class ApplicationExtensions
	{
		// 阶段 5：Avalonia Application 无 MainWindow 属性；需通过
		// IClassicDesktopStyleApplicationLifetime.MainWindow 访问。
		[DebuggerStepThrough]
		private static Window GetMainWindow(Application application)
		{
			return (application?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
		}

		[DebuggerStepThrough]
		public static TabManager TabManager(this Application application)
		{
			return (GetMainWindow(application) as MainWindow)?.TabManager;
		}

		[DebuggerStepThrough]
		public static RepositoryUserControl ActiveRepositoryUserControl(this Application application)
		{
			return (GetMainWindow(application) as MainWindow)?.TabManager.ActiveRepositoryUserControl;
		}

		public static void RefreshLayoutScaling(this Application application)
		{
			double num = (double)ForkPlusSettings.Default.LayoutScaling * 0.01;
			application.Resources["LayoutScaleTransform"] = new ScaleTransform(num, num);
		}
	}
}
