namespace ForkPlus.UI
{
	/// <summary>
	/// 跨平台窗口状态枚举。Phase 0.4 从 WPF 迁入 Core 时引入，
	/// 替代 <c>System.Windows.WindowState</c>（WPF-only）。
	/// 枚举值与 <c>System.Windows.WindowState</c> 保持一致（Normal=0, Minimized=1, Maximized=2），
	/// 因此可以通过强制转换互转，便于 WPF 工程过渡期桥接。
	/// </summary>
	public enum WindowState
	{
		Normal = 0,
		Minimized = 1,
		Maximized = 2
	}
}
