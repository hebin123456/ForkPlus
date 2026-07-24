using System;
using System.IO;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 阶段 4 里程碑 4.7-c-1：WebView2 NavigateToString(HTML) → Markdown.Avalonia 原生控件渲染。
	/// 原 WPF 流程：.md 文件 → Bt.bt_md_to_html（native）+ 自研表格解析 → HTML 字符串 → WebView2.NavigateToString。
	/// 新 Avalonia 流程：.md 文件 → MarkdownScrollViewer.Markdown 属性 → 原生 Avalonia 控件（Border/Grid/TextBlock）。
	/// 移除了全部 HTML 生成代码（MarkdownToHtml / AppendHtmlTable / ConvertInlineMarkdownToHtml / CreateHtmlDocument），
	/// GFM 表格由 Markdown.Avalonia 内置支持。
	/// </summary>
	public partial class GitMmReferenceWindow : ForkPlusDialogWindow
	{
		public GitMmReferenceWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("git mm Reference");
			base.DialogDescription = Translate("Command reference for git mm start, sync, and upload.");
			base.CancelButtonTitle = Translate("Close");
			base.ShowSubmitButton = false;
			base.Loaded += delegate
			{
				InitializeManual();
			};
		}

		internal static string LoadManual()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			string docsDirectory = Path.Combine(AppContext.BaseDirectory, "Docs");
			string[] candidates =
			{
				Path.Combine(docsDirectory, "gitmm." + language + ".md"),
				Path.Combine(docsDirectory, "gitmm.en.md"),
				Path.Combine(docsDirectory, "gitmm.zh-Hans.md"),
				Path.Combine(docsDirectory, "gitmm.md")
			};
			foreach (string path in candidates)
			{
				if (File.Exists(path))
				{
					return File.ReadAllText(path);
				}
			}
			return Translate("git mm reference document was not found.");
		}

		private void InitializeManual()
		{
			try
			{
				string markdown = LoadManual();
				ManualMarkdownViewer.Markdown = markdown;
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to show git mm reference", ex);
				ShowFallback(ex.Message);
			}
		}

		private void ShowFallback(string message)
		{
			ManualMarkdownViewer.Collapse();
			ManualFallback.Show();
			ManualFallback.FallbackTitle = Translate("Error");
			ManualFallback.FallbackMessage = message;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
