namespace ForkPlus.UI
{
	/// <summary>
	/// FileControlHeaderUserControl 的工具栏显示模式。
	/// Phase 0.4 从 WPF (namespace ForkPlus.UI.UserControls) 迁入 Core，
	/// 改为 ForkPlus.UI 命名空间（与 DiffLayoutMode / WindowState 一致）。
	/// </summary>
	public enum FileControlHeaderMode
	{
		None,
		Text,
		Image,
		// v3.1.0：Hex 视图模式（HexContentControl 内部自带工具栏，header 仅显示路径）
		Hex
	}
}
