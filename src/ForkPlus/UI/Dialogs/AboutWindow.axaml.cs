using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class AboutWindow : ForkPlusDialogWindow
	{

		public AboutWindow()
		{
			base.ShowLogo = false;
			// WPF 原版 AboutWindow 首行 RowDefinition Height=0，从而隐藏 ForkPlusDialogWindow 的 Header 区。
			// Avalonia 下 0 高度行可能仍会溢出渲染，为保持与原版一致，直接禁用 Header。
			base.ShowHeader = false;
			base.ShowFooter = false;
			InitializeComponent();
			string title = Translate("About " + App.AppName);
			base.Title = title;
			base.DialogTitle = title;
			VersionTextBlock.Text = string.Format(Translate("Version {0}"), App.Version);
			CopyrightTextBlock.Text = string.Format(Translate("Copyright © {0} Hebin"), DateTime.Now.Year);
			// 2026-09-19：关于窗口新增"用户手册"入口，跳转 GitHub Pages 上的在线手册,
			// 与官网链接 hebin.me 同款蓝色超链接样式（上面那条），排版在版权行上方。
			UserManualTextBlock.Text = Translate("User manual");
			// 2026-09-17：双击 About 窗口的大 Fork 图标 → 当前版本"更新内容"弹窗
			//（版本 + RELEASE_NOTE.md 当前版本章节，与弹窗左上角 Fork 图标同款入口；
			//  本窗口 ShowLogo=false 无头部小图标，故在自有大图标上接线）。
			IconImage.DoubleTapped += delegate
			{
				MainWindow.Commands.OpenReleaseNotes.Execute();
			};
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void LegalHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			new LegalWindow().ShowDialog();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
