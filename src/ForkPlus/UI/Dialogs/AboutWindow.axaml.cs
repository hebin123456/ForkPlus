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
