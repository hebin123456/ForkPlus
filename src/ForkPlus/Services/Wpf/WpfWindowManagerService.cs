using System;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的窗口管理服务。
	/// 阶段 2 扩展：承接 Commands 层对 <c>MainWindow.Instance.TabManager</c> 与
	/// <c>Application.Current</c> 的直接访问，转发到具体 View。
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

		// ===== 阶段 2 新增：Tab 管理 =====

		public void NewTab()
		{
			MainWindow.Instance?.TabManager.NewTab();
		}

		public void CloseActiveTab()
		{
			MainWindow.Instance?.TabManager.CloseActiveTab();
		}

		public void SelectPreviousTab()
		{
			MainWindow.Instance?.TabManager.SelectPreviousTab();
		}

		public void SelectNextTab()
		{
			MainWindow.Instance?.TabManager.SelectNextTab();
		}

		public bool OpenRepository(string path, GitModule nextTo = null)
		{
			return MainWindow.Instance?.TabManager.OpenRepository(path, nextTo) ?? false;
		}

		public void OpenRepositories(string[] repositoryPaths)
		{
			MainWindow.Instance?.TabManager.OpenRepositories(repositoryPaths);
		}

		public void RefreshActiveRepositoryManager()
		{
			MainWindow.Instance?.TabManager?.ActiveRepositoryManager?.Refresh();
		}

		// ===== 阶段 2 新增：应用级操作 =====

		public void RefreshLayoutScaling()
		{
			System.Windows.Application.Current?.RefreshLayoutScaling();
		}

		public void CheckForUpdates()
		{
			MainWindow.Instance?.CheckForUpdates();
		}
	}
}
