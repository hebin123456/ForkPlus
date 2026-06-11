using System;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的窗口管理服务。
	/// </summary>
	public class WpfWindowManagerService : IWindowManagerService
	{
		public void ActivateAndShowNotifications()
		{
			MainWindow instance = MainWindow.Instance;
			if (instance != null)
			{
				instance.Activate();
				instance.ShowNotificationManager();
			}
		}

		public bool TryActivateWindowByTitle(string title)
		{
			System.Windows.WindowCollection windowCollection = System.Windows.Application.Current?.Windows;
			if (windowCollection == null)
			{
				return false;
			}
			foreach (object item in windowCollection)
			{
				if (item is AiCodeReviewWindow aiCodeReviewWindow && aiCodeReviewWindow.Title == title)
				{
					aiCodeReviewWindow.Activate();
					return true;
				}
			}
			return false;
		}

		public void DispatchToUiThread(Action action)
		{
			System.Windows.Application.Current?.Dispatcher.Async(action);
		}
	}
}
