using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 首次启动新版本时的"更新内容"弹窗（v4.1.0 起）：展示当前版本在随包
	/// RELEASE_NOTE.md 里的章节。复用 ForkPlusDialogWindow 组件框架（头部
	/// 标题 + logo + Footer 按钮），Release Notes 区与 UpdateAvailableWindow /
	/// UpdateCheckWindow 同款样式，只保留单个 Close 按钮，风格与现有弹窗一致。
	/// 2026-09-17 起正文经 MarkdownNotesRenderer 渲染（此前只读 TextBox 直显 markdown 源码）。
	/// 展示时机与"是否首次启动该版本"的判定见 ReleaseNotesManager。
	/// </summary>
	public partial class ReleaseNotesWindow : ForkPlusDialogWindow
	{
		/// <summary>弹窗展示的原始 markdown（看门狗捕获正文 / 测试断言用）。</summary>
		public string NotesMarkdown { get; }

		public ReleaseNotesWindow(string version, string notes)
		{
			InitializeComponent();
			DialogTitle = PreferencesLocalization.FormatCurrent("What's New in {0}", version);
			NotesMarkdown = notes ?? "";
			MarkdownNotesRenderer.Render(ReleaseNotesPanel, NotesMarkdown);
			SubmitButtonTitle = PreferencesLocalization.Current("Close");
			ShowCancelButton = false;
		}
	}
}
