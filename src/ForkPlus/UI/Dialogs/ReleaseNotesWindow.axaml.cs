using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 首次启动新版本时的"更新内容"弹窗（v4.1.0 起）：展示当前版本在随包
	/// RELEASE_NOTE.md 里的章节。复用 ForkPlusDialogWindow 组件框架（头部
	/// 标题 + logo + Footer 按钮），Release Notes 文本框与 UpdateAvailableWindow /
	/// UpdateCheckWindow 同款样式，只保留单个 Close 按钮，风格与现有弹窗一致。
	/// 展示时机与"是否首次启动该版本"的判定见 ReleaseNotesManager。
	/// </summary>
	public partial class ReleaseNotesWindow : ForkPlusDialogWindow
	{
		public ReleaseNotesWindow(string version, string notes)
		{
			InitializeComponent();
			DialogTitle = PreferencesLocalization.FormatCurrent("What's New in {0}", version);
			ReleaseNotesTextBox.Text = notes ?? "";
			SubmitButtonTitle = PreferencesLocalization.Current("Close");
			ShowCancelButton = false;
		}
	}
}
