using Avalonia;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// v3.12.1：mm 子仓内触发单仓 Push 时的引导窗口（与 v3.11.2 的 pull 引导窗口同构）。
	/// 检测：由调用方通过 TabManager.FindGitMmWorkspacePathForSubrepo 判定当前仓库是否隶属 git mm 工作区；
	/// 引导：默认推荐切换到 git mm 工作区执行 git mm upload，多子仓变更一起推送，保持子仓间一致性；
	/// 逃生口：用户明确选择"仅推送当前仓库"时按普通单仓 push 继续。
	/// </summary>
	public partial class MmSubrepoPushGuidanceWindow : ForkPlusDialogWindow
	{
		/// <summary>v3.12.1：用户选择使用 git mm 上传（推荐路径）；false 表示逃生口（仅推送当前仓库）。</summary>
		public bool UseMmUpload { get; private set; } = true;

		public MmSubrepoPushGuidanceWindow(string workspacePath)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Push in git mm workspace");
			base.DialogDescription = Translate("This repository is part of a git mm workspace.");
			base.SubmitButtonTitle = Translate("Continue");
			base.ShowWarningIcon = true;
			WorkspacePathTextBlock.Text = workspacePath ?? "";
			global::Avalonia.Controls.ToolTip.SetTip(WorkspacePathTextBlock,WorkspacePathTextBlock.Text);
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
		}

		private void Option_Changed(object sender, RoutedEventArgs e)
		{
			// null 安全：XAML 解析 IsChecked="True" 时若先于 x:Name 赋值触发本事件，字段可能为 null
			UseMmUpload = MmUploadRadioButton?.IsChecked == true;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
