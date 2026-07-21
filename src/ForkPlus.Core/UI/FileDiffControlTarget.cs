namespace ForkPlus.UI
{
	/// <summary>
	/// FileDiffControl 的目标场景，决定从 ForkPlusSettings 读写哪个 DiffLayoutMode 设置项。
	/// Phase 0.4 从 WPF (namespace ForkPlus.UI.Controls) 迁入 Core，
	/// 改为 ForkPlus.UI 命名空间（与 DiffLayoutMode / FileControlHeaderMode 一致）。
	/// </summary>
	public enum FileDiffControlTarget
	{
		Revision,
		Commit,
		Popup,
		History,
		HunkHistory,
		RevisionWindow
	}
}
