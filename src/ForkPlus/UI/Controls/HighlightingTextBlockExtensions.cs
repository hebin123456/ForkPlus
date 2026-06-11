using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Controls
{
	internal static class HighlightingTextBlockExtensions
	{
		public static void ApplySearchAndButrackerHighlighting(this TextBlock textBlock, [Null] string highlightString, BugtrackerLinkDefinition[] bugtrackers)
		{
			Brush matchBrush = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			string text = textBlock.Text;
			List<(Range, Uri)> issueTrackerLinks = GetIssueTrackerLinks(text, bugtrackers);
			List<Range> searchRanges = GetSearchRanges(text, highlightString);
			if (issueTrackerLinks.Count == 0 && searchRanges.Count == 0)
			{
				RestoreText(textBlock);
				return;
			}
			List<Range> list = issueTrackerLinks.Map(((Range, Uri) x) => x.Item1).ToList();
			Uri[] issueTrackerUrls = issueTrackerLinks.Map(((Range, Uri) x) => x.Item2);
			textBlock.Inlines.Clear();
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
					Hyperlink hyperlink = new Hyperlink(run);
					hyperlink.NavigateUri = uri;
					hyperlink.ToolTip = uri;
					hyperlink.ToolTip = uri;
					hyperlink.Style = Theme.FindStyle("BugtrackerHyperlinkStyle");
					hyperlink.ContextMenu = CreateBugtrackerHyperlinkContextMenu(text2, uri.AbsoluteUri);
					hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
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
			string text = textBlock.Text;
			if (string.IsNullOrEmpty(fuzzySearchString) || !text.HasFuzzyMatch(fuzzySearchString))
			{
				RestoreText(textBlock);
				return;
			}
			int[] array = new int[fuzzySearchString.Length];
			text.MatchPositions(fuzzySearchString, array);
			textBlock.Inlines.Clear();
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
			string text = textBlock.Text;
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
			Brush foreground = Theme.FindBrush("ForegroundBrush");
			Brush background = Theme.FindBrush("RevisionList.SearchMatch.ForegroundBrush");
			textBlock.Inlines.Clear();
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
				string text = textBlock.Text;
				inlines.Clear();
				inlines.Add(text);
			}
		}

		private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
		}
	}
}
