using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// v3.12.1：mm 子仓 push 防呆——检测 + 引导 + 逃生口（与 v3.11.2 的 pull 防呆同构）。
	/// 背景：git mm 工作区由多个子仓组成，单独对某个子仓执行 git push 会破坏子仓间变更一致性
	///       （多仓协同的变更应通过 git mm upload 一起推送，避免远端只落到部分子仓）。
	/// 检测：通过 TabManager.FindGitMmWorkspacePathForSubrepo 判定当前仓库是否隶属 git mm 工作区
	///       （覆盖"mm 页签内选中子仓"与"单仓页签打开但路径位于 mm 工作区内"两种场景）；
	/// 引导：默认推荐切换到所属 git mm 工作区执行 git mm upload；
	/// 逃生口：用户明确选择"仅推送当前仓库"时，按普通单仓 push 继续。
	/// </summary>
	internal static class MmSubrepoPushGuard
	{
		/// <summary>
		/// 检测当前仓库是否隶属 git mm 工作区；若是则弹出引导窗口。
		/// 返回 true 表示继续执行单仓 push（非 mm 子仓，或用户选择逃生口）；
		/// 返回 false 表示已引导切换到 mm 上传，或用户取消。
		/// </summary>
		public static bool ConfirmSingleRepoPush(RepositoryUserControl repositoryUserControl)
		{
			string subrepoPath = repositoryUserControl?.GitModule?.Path;
			if (string.IsNullOrWhiteSpace(subrepoPath))
			{
				return true;
			}
			string mmWorkspacePath = MainWindow.Instance?.TabManager?.FindGitMmWorkspacePathForSubrepo(subrepoPath);
			if (string.IsNullOrWhiteSpace(mmWorkspacePath))
			{
				return true;
			}
			MmSubrepoPushGuidanceWindow dialog = new MmSubrepoPushGuidanceWindow(mmWorkspacePath);
			if (dialog.ShowDialog() != true)
			{
				return false;
			}
			if (dialog.UseMmUpload)
			{
				// 切换（或新开）所属 git mm 工作区页签，并打上传窗口执行 git mm upload
				var tabManager = MainWindow.Instance?.TabManager;
				if (tabManager != null && tabManager.OpenRepository(mmWorkspacePath))
				{
					tabManager.ActiveGitMmUserControl?.OpenUploadWindow();
				}
				return false;
			}
			// 逃生口：用户明确选择仅推送当前仓库
			return true;
		}
	}
}
