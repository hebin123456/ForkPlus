namespace ForkPlus.UI
{
	public enum ActivityManagerViewMode
	{
		Debug,
		All,
		User,
		Background,
		// 2026-09-10：git mm 命令输出收编到活动管理器——独立内容视图（直接展示 git mm 输出，
		// 不走作业列表筛选）。与 All/User/Background（按 JobFlags 筛选作业列表）不同，GitMm
		// 视图隐藏作业列表，右侧直接显示当前活动 GitMmUserControl 的命令输出。
		GitMm
	}
}
