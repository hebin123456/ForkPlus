using System;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 修复（2026-09-14，"另存为补丁内容多时界面卡住"）：补丁生成进度弹窗。
	/// CreatePatchGitCommand 逐文件各起一次 git 进程，大改动在 UI 线程同步执行会长时间
	/// 冻结界面。此弹窗配合 Task.Run 后台生成使用：footer 走 ForkPlusDialogStatus.InProgress
	/// （自带旋转 spinner），进度文本由 UpdateProgress 逐文件刷新；Submit/Cancel 按钮均
	/// 隐藏（不可中断、不可误触，Esc 亦不关窗——ShowCancelButton=false 时基类不响应 Esc）。
	/// </summary>
	public partial class PatchProgressWindow : ForkPlusDialogWindow
	{
		private readonly string _baseStatusText;

		public PatchProgressWindow()
		{
			InitializeComponent();
			DialogTitle = PreferencesLocalization.Translate("Save as Patch…", ForkPlusSettings.Default.UiLanguage);
			DialogDescription = PreferencesLocalization.Translate("Generating...", ForkPlusSettings.Default.UiLanguage);
			ShowSubmitButton = false;
			ShowCancelButton = false;
			_baseStatusText = PreferencesLocalization.Translate("Generating...", ForkPlusSettings.Default.UiLanguage);
			SetStatus(ForkPlusDialogStatus.InProgress, _baseStatusText);
		}

		public void UpdateProgress(int index, int total)
		{
			SetStatus(ForkPlusDialogStatus.InProgress, _baseStatusText + " (" + index + "/" + total + ")");
		}

		public void SetFilePath(string filePath)
		{
			FilePathTextBlock.Text = filePath;
		}
	}
}
