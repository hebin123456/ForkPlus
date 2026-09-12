namespace ForkPlus.Git
{
	public static class RepositoryStatusExtensions
	{
		// v4.0.12（2026-09-12，CreateBranchWindow 竞态崩溃）：仓库刚打开、状态刷新
		//（RepositoryStatusUpdate 管线）尚未完成时 RepositoryStatus 为 null——此时打开
		// 创建分支等窗口（Ctrl+Shift+B / 菜单），CreateBranchWindow 构造第 102 行直呼
		// 本扩展 NRE 崩溃（E2e28 RepoDialogs 实证）；CheckoutRevision / TrackRemoteBranch /
		// CheckoutBranch / LeanBranchingStart 四窗同款调用一并暴露。扩展方法收口 null 安全：
		// 状态未知按"干净"处理（各窗口只用它决定 stash 提示文案/选项，不涉及写入路径）。
		public static bool WorkingDirectoryIsDirty(this RepositoryStatus repositoryStatus)
		{
			return repositoryStatus?.ChangedFiles.AnyItem((ChangedFile x) => x.Tracked && !(x is SubmoduleChangedFile)) == true;
		}
	}
}
