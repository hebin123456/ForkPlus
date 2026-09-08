using System;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using Avalonia.Threading;

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
		// Migration note：WPF Application.Current.Windows（Avalonia.WindowCollection 命名空间下不存在该类型，
		// CS0234）改为 WpfCompat WpfApp.Windows：转发到 IClassicDesktopStyleApplicationLifetime.Windows
		// （IReadOnlyList<Window>），遍历语义不变。
		System.Collections.Generic.IReadOnlyList<global::Avalonia.Controls.Window> windowCollection = global::ForkPlus.UI.WpfCompat.WpfApp.Windows;
		if (windowCollection == null)
		{
			return false;
		}
		foreach (global::Avalonia.Controls.Window item in windowCollection)
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
			global::Avalonia.Application.Current?.Dispatcher.Post(action);
		}
	}
}
