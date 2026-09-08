using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal static class HighlightingTextBlockExtensions
	{
		// ===== Avalonia Text/Inlines 语义适配（2026-09-04 修复"提交详情内容缩进"）=====
		// 探针实证（TextInlinesSyncProbeTests）：Avalonia 与 WPF 的关键差异——
		//   WPF：TextBlock.Text 与 Inlines 是同一文档的两个视图，Clear+Add 不会重复渲染。
		//   Avalonia：Inlines 首次 Add 时，若 Text 非空，会把 Text 内容隐式转成 Run 插到
		//     Inlines 开头并清空 Text。按 WPF 语义写的高亮逻辑（Clear 后分段 Add）因此把
		//     全文重复渲染了一遍，issue 链接按钮被推到重复文本后面——提交详情中含 #123
		//     等引用的 subject/description 出现整体缩进；RestoreText 读 Text（已被清空）
		//     则把文本恢复成空。
		// 修复：进入分段模式前把原文存入弱表并置 Text=null（阻止隐式插入）；原文优先
		//   从弱表取（Inlines.Count>0 = 分段模式；外部重设 Text 会自动清空 Inlines，
		//   回到 Text 模式，见探针步骤 9b）。
		// 原文存储：ConditionalWeakTable 随 TextBlock GC 自动清理（不新增生产类型，
		// 也无需污染 Avalonia 属性系统）。
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, string> OriginalTexts = new();

		private static string GetOriginalText(TextBlock textBlock)
		{
			if (textBlock.Inlines is { Count: > 0 } && OriginalTexts.TryGetValue(textBlock, out string original) && original != null)
			{
				return original;
			}
			return textBlock.Text ?? "";
		}

		private static void BeginInlineSegments(TextBlock textBlock, string text)
		{
			textBlock.Inlines.Clear();
			OriginalTexts.AddOrUpdate(textBlock, text);
			textBlock.Text = null; // 阻止 Avalonia 在首次 Inlines.Add 时隐式插入全文 Run
		}

		public static void ApplySearchAndButrackerHighlighting(this TextBlock textBlock, [Null] string highlightString, BugtrackerLinkDefinition[] bugtrackers)
		{
			Brush matchBrush = global::ForkPlus.UI.Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			string text = GetOriginalText(textBlock);
			List<(Range, Uri)> issueTrackerLinks = GetIssueTrackerLinks(text, bugtrackers);
			List<Range> searchRanges = GetSearchRanges(text, highlightString);
			if (issueTrackerLinks.Count == 0 && searchRanges.Count == 0)
			{
				RestoreText(textBlock);
				return;
			}
			List<Range> list = issueTrackerLinks.Map(((Range, Uri) x) => x.Item1).ToList();
			Uri[] issueTrackerUrls = issueTrackerLinks.Map(((Range, Uri) x) => x.Item2);
			BeginInlineSegments(textBlock, text);
			new Range(0, text.Length).Merge(new List<Range>[2] { list, searchRanges }, delegate(Range range, int? issueIndex, int? searchIndex, int? _)
			{
				string text2 = text.Substring(range);
				Run run = new Run(text2);
				if (searchIndex.HasValue)
				{
					run.Background = matchBrush;
				}
				if (issueIndex.HasValue)
				{
					if (range.Start == 0)
					{
						textBlock.Inlines.Add(new Run());
					}
					Uri uri = issueTrackerUrls[issueIndex.Value];
					// Migration note：WPF Hyperlink(Run) 内联超链接 → Avalonia HyperlinkButton（Content 承载 Run，
					// InlineCollection.Add(Control) 支持 Control 作为内联元素）。
					global::Avalonia.Controls.HyperlinkButton hyperlink = new global::Avalonia.Controls.HyperlinkButton
					{
						Content = run,
						NavigateUri = uri
					};
					global::Avalonia.Controls.ToolTip.SetTip(hyperlink,uri);
					global::Avalonia.Controls.ToolTip.SetTip(hyperlink,uri);
{					global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(hyperlink, global::ForkPlus.UI.Theme.FindStyle("BugtrackerHyperlinkStyle"));
}					hyperlink.ContextMenu = CreateBugtrackerHyperlinkContextMenu(text2, uri.AbsoluteUri);
					textBlock.Inlines.Add(hyperlink);
					if (range.End == text.Length)
					{
						textBlock.Inlines.Add(new Run());
					}
				}
				else
				{
					textBlock.Inlines.Add(run);
				}
			});
		}

		private static ContextMenu CreateBugtrackerHyperlinkContextMenu(string title, string url)
		{
			ContextMenu obj = new ContextMenu
			{
				FontSize = 12.0,
				FontWeight = FontWeights.Normal
			};
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Copy");
			menuItem.Click += delegate
			{
				ServiceLocator.Clipboard.SetText(title);
			};
			obj.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Copy Link");
			menuItem2.Click += delegate
			{
				ServiceLocator.Clipboard.SetText(url);
			};
			obj.Items.Add(menuItem2);
			return obj;
		}

		public static void ApplyFuzzyHighlighting(this FuzzyHighlightableTextBlock textBlock, string fuzzySearchString)
		{
			string text = GetOriginalText(textBlock);
			if (string.IsNullOrEmpty(fuzzySearchString) || !text.HasFuzzyMatch(fuzzySearchString))
			{
				RestoreText(textBlock);
				return;
			}
			int[] array = new int[fuzzySearchString.Length];
			text.MatchPositions(fuzzySearchString, array);
			BeginInlineSegments(textBlock, text);
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				int num2 = array[i];
				int length = num2 - num;
				string text2 = text.Substring(num, length);
				textBlock.Inlines.Add(text2);
				int num3 = 1;
				for (; i + 1 < array.Length && array[i + 1] == num2 + 1; i++)
				{
					num3++;
				}
				textBlock.Inlines.Add(new Run(text.Substring(num2, num3))
				{
					FontWeight = FontWeights.Bold
				});
				num = num2 + num3;
			}
			string text3 = text.Substring(num);
			textBlock.Inlines.Add(text3);
		}

		public static void ApplySearchHighlighting(this TextBlock textBlock, string highlightString)
		{
			string text = GetOriginalText(textBlock);
			if (string.IsNullOrEmpty(highlightString))
			{
				RestoreText(textBlock);
				return;
			}
			int num = text.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
			if (num == -1)
			{
				RestoreText(textBlock);
				return;
			}
			int length = highlightString.Length;
			int num2 = 0;
			Brush foreground = global::ForkPlus.UI.Theme.FindBrush("ForegroundBrush");
			Brush background = global::ForkPlus.UI.Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			BeginInlineSegments(textBlock, text);
			while (num != -1)
			{
				int length2 = num - num2;
				string text2 = text.Substring(num2, length2);
				textBlock.Inlines.Add(text2);
				textBlock.Inlines.Add(new Run(text.Substring(num, length))
				{
					Background = background,
					Foreground = foreground
				});
				num2 = num + length;
				num = text.IndexOf(highlightString, num2, StringComparison.OrdinalIgnoreCase);
			}
			string text3 = text.Substring(num2);
			textBlock.Inlines.Add(text3);
		}

		private static List<(Range, Uri)> GetIssueTrackerLinks(string text, [Null] BugtrackerLinkDefinition[] rules)
		{
			List<(Range, Uri)> list = new List<(Range, Uri)>();
			if (!ForkPlusSettings.Default.ShowBugtrackerLinks || rules == null)
			{
				return list;
			}
			foreach (BugtrackerLinkDefinition bugtrackerLinkDefinition in rules)
			{
				MatchCollection matchCollection = bugtrackerLinkDefinition.Regex.Matches(text);
				if (matchCollection.Count == 0)
				{
					continue;
				}
				List<(Range, Uri)> list2 = new List<(Range, Uri)>(matchCollection.Count);
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					if (match.Success)
					{
						Uri uri = FormatUrl(bugtrackerLinkDefinition.UrlString, match);
						if ((object)uri != null)
						{
							list2.Add((new Range(match.Index, match.Index + match.Length), uri));
						}
					}
				}
				list = Merge(list, list2);
			}
			return list;
		}

		private static List<(Range, Uri)> Merge(List<(Range, Uri)> x, List<(Range, Uri)> y)
		{
			if (x.Count == 0)
			{
				return y;
			}
			List<(Range, Uri)> list = new List<(Range, Uri)>();
			int i = 0;
			int j = 0;
			while (i < x.Count && j < y.Count)
			{
				if (x[i].Item1.Start < y[j].Item1.Start)
				{
					if (x[i].Item1.Contains(y[j].Item1.Start))
					{
						j++;
						continue;
					}
					list.Add(x[i]);
					i++;
				}
				else if (x[i].Item1.Start > y[j].Item1.Start)
				{
					if (y[j].Item1.Contains(x[i].Item1.Start))
					{
						i++;
						continue;
					}
					list.Add(y[j]);
					j++;
				}
				else if (x[i].Item1.Length > y[j].Item1.Length)
				{
					j++;
				}
				else if (x[i].Item1.Length < y[j].Item1.Length)
				{
					i++;
				}
				else
				{
					j++;
				}
			}
			for (; i < x.Count; i++)
			{
				list.Add(x[i]);
			}
			for (; j < y.Count; j++)
			{
				list.Add(y[j]);
			}
			return list;
		}

		[Null]
		private static Uri FormatUrl(string urlTemplate, Match match)
		{
			string text = urlTemplate;
			for (int i = 1; i < match.Groups.Count; i++)
			{
				Group group = match.Groups[i];
				if (group.Success)
				{
					text = text.Replace($"${i}", group.Value);
				}
			}
			try
			{
				return new Uri(text);
			}
			catch
			{
				return null;
			}
		}

		private static List<Range> GetSearchRanges(string text, [Null] string highlightString)
		{
			List<Range> list = new List<Range>();
			if (string.IsNullOrEmpty(highlightString))
			{
				return list;
			}
			int num = text.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
			while (num != -1)
			{
				int num2 = num + highlightString.Length;
				list.Add(new Range(num, num2));
				num = text.IndexOf(highlightString, num2, StringComparison.OrdinalIgnoreCase);
			}
			return list;
		}

		private static void RestoreText(TextBlock textBlock)
		{
			InlineCollection inlines = textBlock.Inlines;
			if (inlines.Count > 1)
			{
				// Avalonia 语义：分段模式下 Text 已被清空，原文在弱表里；
				// Inlines.Clear() 后用 Text 赋值恢复纯文本渲染模式。
				string text = GetOriginalText(textBlock);
				inlines.Clear();
				textBlock.Text = text;
				OriginalTexts.Remove(textBlock);
			}
		}

		private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
		}
	}
}
