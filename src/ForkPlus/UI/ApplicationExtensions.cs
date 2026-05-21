using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public static class ApplicationExtensions
	{
		[DebuggerStepThrough]
		public static TabManager TabManager(this Application application)
		{
			return (application.MainWindow as MainWindow)?.TabManager;
		}

		[DebuggerStepThrough]
		public static RepositoryUserControl ActiveRepositoryUserControl(this Application application)
		{
			return (application.MainWindow as MainWindow)?.TabManager.ActiveRepositoryUserControl;
		}

		public static void RefreshLayoutScaling(this Application application)
		{
			double num = (double)ForkPlusSettings.Default.LayoutScaling * 0.01;
			application.Resources["LayoutScaleTransform"] = new ScaleTransform(num, num);
		}
	}
}
