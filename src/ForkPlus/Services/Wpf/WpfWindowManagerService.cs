using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// 平台窗口管理服务（阶段 4.5：WPF System.Windows.Application → Avalonia.Application）。
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
			// 阶段 4.5：WPF System.Windows.Application.Current.Windows → Avalonia.Application.Current.Windows。
			var windows = Application.Current?.Windows;
			if (windows == null)
			{
				return false;
			}
			foreach (Window window in windows)
			{
				if (window is AiCodeReviewWindow aiCodeReviewWindow && aiCodeReviewWindow.Title == title)
				{
					aiCodeReviewWindow.Activate();
					return true;
				}
			}
			return false;
		}

		public void DispatchToUiThread(Action action)
		{
			// 阶段 4.5：WPF Application.Current.Dispatcher.Async → Avalonia Dispatcher.UIThread.Post。
			Dispatcher.UIThread.Post(action);
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

		// ===== 阶段 3 新增：活动仓库视图操作 =====
		// 全部转发到 MainWindow.Instance.TabManager.ActiveRepositoryUserControl，
		// 与 ApplicationExtensions.ActiveRepositoryUserControl() 同源；逐方法 null-safe。

		public void InvalidateAndRefreshActiveRepositoryView(SubDomain domain)
		{
			MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.InvalidateAndRefresh(domain);
		}

		public void ActivateRevisionViewOnActiveRepository()
		{
			MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.ActivateRevisionView();
		}

		public void ShowRevisionDetailsOnActiveRepository(RevisionDiffTarget target)
		{
			MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.ShowRevisionDetails(target);
		}

		public TempFileManager GetActiveRepositoryTempFileManager()
		{
			return MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.TempFileManager;
		}

		public GitModule GetActiveRepositoryGitModule()
		{
			return MainWindow.Instance?.TabManager?.ActiveRepositoryUserControl?.GitModule;
		}

		// ===== 阶段 2 新增：应用级操作 =====

		public void RefreshLayoutScaling()
		{
			// 阶段 4.5：WPF Application.Current.RefreshLayoutScaling() → Avalonia ApplicationExtensions.RefreshLayoutScaling()。
			Application.Current?.RefreshLayoutScaling();
		}

		public void CheckForUpdates()
		{
			MainWindow.Instance?.CheckForUpdates();
		}
	}
}
