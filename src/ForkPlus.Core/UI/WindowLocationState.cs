namespace ForkPlus.UI
{
	/// <summary>
	/// 窗口位置/尺寸/状态的不可变快照，用于持久化到 settings.json。
	/// Phase 0.4 从 <c>src/ForkPlus/UI/WindowLocationState.cs</c> 迁入 Core，
	/// 同时将 <see cref="WindowState"/> 属性类型从 <c>System.Windows.WindowState</c>
	/// 替换为跨平台的 <see cref="ForkPlus.UI.WindowState"/>。
	/// </summary>
	public class WindowLocationState
	{
		public double Left { get; }

		public double Top { get; }

		public double Width { get; }

		public double Height { get; }

		public WindowState WindowState { get; }

		public WindowLocationState(double left, double top, double width, double height, WindowState windowState)
		{
			Left = left;
			Top = top;
			Width = width;
			Height = height;
			WindowState = windowState;
		}
	}
}
