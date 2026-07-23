using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Microsoft.Web.WebView2.Core;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitMmReferenceWindow : ForkPlusDialogWindow
	{
		public GitMmReferenceWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("git mm Reference");
			base.DialogDescription = Translate("Command reference for git mm start, sync, and upload.");
			base.CancelButtonTitle = Translate("Close");
			base.ShowSubmitButton = false;
			base.Loaded += async delegate
			{
				await InitializeManualWebView();
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

		private async System.Threading.Tasks.Task InitializeManualWebView()
		{
			string markdown = LoadManual();
			GitCommandResult<string> htmlResult = MarkdownToHtml(markdown);
			if (!htmlResult.Succeeded)
			{
				ShowFallback(htmlResult.Error.FriendlyDescription);
				return;
			}
			try
			{
				await ManualWebView.EnsureCoreWebView2Async(await WebView2EnvironmentHelper.GetEnvironmentAsync());
				ManualWebView.CoreWebView2.ContextMenuRequested += delegate(object sender, CoreWebView2ContextMenuRequestedEventArgs args)
				{
					args.Handled = true;
				};
				ManualWebView.NavigateToString(CreateHtmlDocument(htmlResult.Result));
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to show git mm reference in WebView", ex);
				ShowFallback(ex.Message);
			}
		}

		internal static GitCommandResult<string> MarkdownToHtml(string markdown)
		{
			string[] lines = (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			StringBuilder html = new StringBuilder(markdown?.Length ?? 0);
			StringBuilder markdownChunk = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				if (IsTableRow(lines[i]) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
				{
					GitCommandResult flushResult = AppendMarkdownChunk(html, markdownChunk);
					if (!flushResult.Succeeded)
					{
						return GitCommandResult<string>.Failure(flushResult.Error);
					}
					List<string[]> rows = new List<string[]>
					{
						SplitTableRow(lines[i])
					};
					i += 2;
					while (i < lines.Length && IsTableRow(lines[i]))
					{
						rows.Add(SplitTableRow(lines[i]));
						i++;
					}
					i--;
					AppendHtmlTable(html, rows);
					continue;
				}
				markdownChunk.AppendLine(lines[i]);
			}
			GitCommandResult finalFlushResult = AppendMarkdownChunk(html, markdownChunk);
			if (!finalFlushResult.Succeeded)
			{
				return GitCommandResult<string>.Failure(finalFlushResult.Error);
			}
			return GitCommandResult<string>.Success(html.ToString());
		}

		private static GitCommandResult AppendMarkdownChunk(StringBuilder html, StringBuilder markdownChunk)
		{
			if (markdownChunk.Length == 0)
			{
				return GitCommandResult.Success();
			}
			GitCommandResult<string> chunkResult = MarkdownChunkToHtml(markdownChunk.ToString());
			if (!chunkResult.Succeeded)
			{
				return GitCommandResult.Failure(chunkResult.Error);
			}
			html.AppendLine(chunkResult.Result);
			markdownChunk.Clear();
			return GitCommandResult.Success();
		}

		private static GitCommandResult<string> MarkdownChunkToHtml(string markdown)
		{
			return BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult result)
			{
				return Bt.bt_md_to_html(markdown, ref result);
			}, delegate(ref BtMdToHtmlResult result)
			{
				return GitCommandResult<string>.Success(result.html.GetUtf8String());
			}, delegate(ref BtMdToHtmlResult result)
			{
				Bt.bt_release_md_to_html(ref result);
			});
		}

		private static bool IsTableRow(string line)
		{
			return !string.IsNullOrWhiteSpace(line)
				&& line.TrimStart().StartsWith("|", StringComparison.Ordinal)
				&& line.TrimEnd().EndsWith("|", StringComparison.Ordinal);
		}

		private static bool IsTableSeparator(string line)
		{
			if (!IsTableRow(line))
			{
				return false;
			}
			string value = line.Replace("|", "").Replace("-", "").Replace(":", "").Trim();
			return value.Length == 0;
		}

		private static string[] SplitTableRow(string line)
		{
			return line.Trim().Trim('|').Split('|');
		}

		private static void AppendHtmlTable(StringBuilder result, List<string[]> rows)
		{
			if (rows.Count == 0)
			{
				return;
			}
			result.AppendLine("<table>");
			result.AppendLine("<thead>");
			AppendHtmlTableRow(result, rows[0], "th");
			result.AppendLine("</thead>");
			if (rows.Count > 1)
			{
				result.AppendLine("<tbody>");
				for (int i = 1; i < rows.Count; i++)
				{
					AppendHtmlTableRow(result, rows[i], "td");
				}
				result.AppendLine("</tbody>");
			}
			result.AppendLine("</table>");
			result.AppendLine();
		}

		private static void AppendHtmlTableRow(StringBuilder result, string[] cells, string cellTag)
		{
			result.AppendLine("<tr>");
			foreach (string cell in cells)
			{
				result.Append('<').Append(cellTag).Append('>');
				result.Append(ConvertInlineMarkdownToHtml(cell.Trim()));
				result.Append("</").Append(cellTag).AppendLine(">");
			}
			result.AppendLine("</tr>");
		}

		private static string ConvertInlineMarkdownToHtml(string text)
		{
			string[] parts = (text ?? "").Split('`');
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < parts.Length; i++)
			{
				string encoded = WebUtility.HtmlEncode(parts[i]);
				if (i % 2 == 1)
				{
					result.Append("<code>").Append(encoded).Append("</code>");
				}
				else
				{
					result.Append(encoded);
				}
			}
			return result.ToString();
		}

		private void ShowFallback(string message)
		{
			ManualWebView.Collapse();
			ManualFallback.Show();
			ManualFallback.FallbackTitle = Translate("Error");
			ManualFallback.FallbackMessage = message;
		}

		private static string CreateHtmlDocument(string bodyHtml)
		{
			return "<!doctype html><html><head><meta charset=\"utf-8\"><style>"
				+ "body{font-family:'Segoe UI',Arial,sans-serif;font-size:13px;line-height:1.5;margin:18px;color:#222;background:#fff;}"
				+ "h1{font-size:24px;margin:0 0 16px;}h2{font-size:19px;margin:24px 0 10px;}h3{font-size:16px;margin:18px 0 8px;}"
				+ "pre{background:#f4f4f4;border:1px solid #ddd;border-radius:4px;padding:10px;overflow:auto;}code{font-family:Consolas,monospace;background:#f4f4f4;padding:1px 3px;border-radius:3px;}"
				+ "table{border-collapse:collapse;width:100%;margin:10px 0 18px;}th,td{border-bottom:1px solid #ddd;text-align:left;vertical-align:top;padding:6px 10px;}th{background:#f2f2f2;font-weight:600;}"
				+ "blockquote{border-left:4px solid #ddd;margin-left:0;padding-left:12px;color:#666;}a{color:#2678c8;}"
				+ "@media (prefers-color-scheme: dark){body{color:#ddd;background:#1e1e1e;}pre,code,th{background:#2d2d2d;}th,td,pre{border-color:#444;}a{color:#6aa9ff;}}"
				+ "</style></head><body>" + bodyHtml + "</body></html>";
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
