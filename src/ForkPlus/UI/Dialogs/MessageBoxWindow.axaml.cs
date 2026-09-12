using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class MessageBoxWindow : ForkPlusDialogWindow
	{

		public MessageBoxWindow(string title, string description, string submitTitle, string cancelTitle = "Cancel", bool showCancelButton = true, double width = 600.0, bool showWarningIcon = false)
		{
			InitializeComponent();
			// Migration note：WPF 构造期 chrome 已就绪；Avalonia 12 延迟初始化，
			// 经 Customize* pending 机制在 chrome 就绪后应用（构造期安全）。
			CustomizeTitleTextBlock(delegate(TextBlock t)
			{
				t.TextTrimming = TextTrimming.CharacterEllipsis;
				t.TextWrapping = TextWrapping.Wrap;
				t.MaxHeight = 80.0;
			});
			CustomizeDescriptionTextBlock(delegate(TextBlock t)
			{
				t.TextTrimming = TextTrimming.CharacterEllipsis;
				t.TextWrapping = TextWrapping.Wrap;
				t.MaxHeight = 80.0;
			});
			base.DialogTitle = Translate(title);
			base.DialogDescription = Translate(description);
			base.SubmitButtonTitle = Translate(submitTitle);
			base.CancelButtonTitle = Translate(cancelTitle);
			base.ShowCancelButton = showCancelButton;
			base.Width = width;
			base.ShowWarningIcon = showWarningIcon;
		}

		/// <summary>v4.0.12（2026-09-12）：headless 测试看门狗读取信息窗文本用。
		/// 基类 DialogDescription 是 protected，测试程序集（InternalsVisibleTo 只覆盖
		/// internal）够不着；此 internal 转发专供看门狗捕获 bisect 收敛弹窗等内容。</summary>
		internal string DialogDescriptionText => base.DialogDescription;

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
