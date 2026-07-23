using System;
using ForkPlus.Git;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的窗口管理接口（查找/激活窗口、Tab 管理、应用级操作）。
	/// WPF 实现使用 <c>Application.Current.Windows</c>，Avalonia 实现使用 <c>Application.Current.Windows</c>。
	/// 阶段 2 扩展：承接 Commands 层对 <c>MainWindow.Instance.TabManager</c> 与
	/// <c>Application.Current</c> 的直接访问，使命令不再依赖具体 View 类型。
	/// </summary>
	public interface IWindowManagerService
	{
		void ActivateAndShowNotifications();
		bool TryActivateWindowByTitle(string title);
		void DispatchToUiThread(Action action);

		// ===== 阶段 2 新增：Tab 管理（替换 Commands 对 MainWindow.Instance.TabManager 的直接访问）=====

		/// <summary>新建标签页。</summary>
		void NewTab();

		/// <summary>关闭当前活动标签页。</summary>
		void CloseActiveTab();

		/// <summary>切换到上一个标签页。</summary>
		void SelectPreviousTab();

		/// <summary>切换到下一个标签页。</summary>
		void SelectNextTab();

		/// <summary>在标签页中打开仓库。返回是否成功打开。</summary>
		/// <param name="path">仓库路径。</param>
		/// <param name="nextTo">若非空，新标签页将插入到该模块所在标签旁；否则追加。</param>
		bool OpenRepository(string path, GitModule nextTo = null);

		/// <summary>批量打开多个仓库。</summary>
		void OpenRepositories(string[] repositoryPaths);

		/// <summary>刷新当前活动的仓库管理器（RepositoryManager 视图）。</summary>
		void RefreshActiveRepositoryManager();

		// ===== 阶段 3 新增：活动仓库视图操作（替换 Commands 对 Application.Current.ActiveRepositoryUserControl() 的直接访问）=====
		// 这些方法仅引用领域/根层类型（SubDomain / RevisionDiffTarget / GitModule / TempFileManager），
		// 不引入 UI 层类型，故可安全放入本接口而不破坏 Services → UI 的分层。

		/// <summary>使活动仓库视图失效并按指定子域刷新（替换 <c>ActiveRepositoryUserControl().InvalidateAndRefresh(domain)</c>）。</summary>
		void InvalidateAndRefreshActiveRepositoryView(SubDomain domain);

		/// <summary>在活动仓库视图中激活 Revision 视图（替换 <c>ActiveRepositoryUserControl().ActivateRevisionView()</c>）。</summary>
		void ActivateRevisionViewOnActiveRepository();

		/// <summary>在活动仓库视图中展示指定 revision diff 详情（替换 <c>ActiveRepositoryUserControl().ShowRevisionDetails(target)</c>）。</summary>
		void ShowRevisionDetailsOnActiveRepository(RevisionDiffTarget target);

		/// <summary>获取活动仓库视图的临时文件管理器（替换 <c>ActiveRepositoryUserControl().TempFileManager</c>）。无活动仓库时返回 null。</summary>
		TempFileManager GetActiveRepositoryTempFileManager();

		/// <summary>获取活动仓库视图的 GitModule（替换 <c>ActiveRepositoryUserControl().GitModule</c>）。无活动仓库时返回 null。</summary>
		GitModule GetActiveRepositoryGitModule();

		// ===== 阶段 2 新增：应用级操作 =====

		/// <summary>重新计算并应用布局缩放（替换 <c>Application.Current.RefreshLayoutScaling()</c>）。</summary>
		void RefreshLayoutScaling();

		/// <summary>触发"检查更新"流程（替换 <c>MainWindow.Instance.CheckForUpdates()</c>）。</summary>
		void CheckForUpdates();
	}
}
