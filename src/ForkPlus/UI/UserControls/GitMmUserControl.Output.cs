// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia（Thickness）
// - using System.Windows.Controls → using Avalonia.Controls（Button/TextBlock/TextTrimming）
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Run/Hyperlink/InlineCollection）
// - using System.Windows.Media → using Avalonia.Media（IBrush/Brushes）
// - using System.Windows.Navigation → 移除（Avalonia 无 RequestNavigateEventArgs）
// - using System.Windows.Threading → using Avalonia.Threading（DispatcherTimer/DispatcherPriority）
// - 新增 using Avalonia.Interactivity（RoutedEventArgs）、using Avalonia.VisualTree（GetVisualAncestors）
// - RichTextBox.Document.Blocks（FlowDocument 模型）→ StackPanel.Children（每行一个 TextBlock + Inlines）
//   Paragraph → TextBlock（Inlines 等价；Margin 兼容）。XAML 需同步迁移：RichTextBox → ScrollViewer+StackPanel。
// - Dispatcher.BeginInvoke(action, DispatcherPriority) → Dispatcher.Post(action, priority)（参考 AiDevelopmentWindow）
// - Hyperlink.RequestNavigate + RequestNavigateEventArgs.Uri → Hyperlink.Click + Hyperlink.NavigateUri（参考 AccountDetailsUserControl）
// - new Hyperlink(new Run(url)) → new Hyperlink + Inlines.Add(new Run(url))（Avalonia Span 无 Inline 构造函数）
// - Application.Current.TryFindResource("AccentBrush") as Brush → Theme.FindBrush("AccentBrush")（参考 ActivityManagerUserControl）
// - Brush → IBrush（Avalonia.Media.IBrush，Brushes.XXX 返回 ISolidColorBrush）
// - button.ToolTip = link → ToolTip.SetTip(button, link)（参考 FileControlHeaderUserControl）
// - OutputTextBox.ScrollToEnd() → 查找父 ScrollViewer.ScrollToEnd()（Avalonia ScrollViewer.ScrollToEnd 存在）
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class GitMmUserControl
	{
		private const int RichOutputLineLimit = 1000;

		private const int MaxOutputLineCount = 4000;

		private static readonly Regex UrlRegex = new Regex(@"https?://[^\s<>""']+", RegexOptions.Compiled);

		private static readonly Regex AnsiSgrRegex = new Regex(@"\x1B\[([0-9;]*)m", RegexOptions.Compiled);

		private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

		private readonly object _outputLock = new object();

		private readonly List<string> _pendingOutputLines = new List<string>();

		private bool _outputFlushScheduled;

		private readonly DispatcherTimer _uploadLinksAutoHideTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};

		private string[] _latestUploadLinks = new string[0];

		private sealed class OutputSegment
		{
			public string Text { get; }

			// 阶段 4.5：WPF Brush → Avalonia IBrush（Brushes.XXX 返回 ISolidColorBrush 实现 IBrush）。
			[Null]
			public IBrush Foreground { get; }

			public OutputSegment(string text, [Null] IBrush foreground)
			{
				Text = text;
				Foreground = foreground;
			}
		}

		private void AppendOutput(string text)
		{
			lock (_outputLock)
			{
				_pendingOutputLines.Add(text ?? "");
				if (_outputFlushScheduled)
				{
					return;
				}
				_outputFlushScheduled = true;
			}
			// 阶段 4.5：WPF Dispatcher.BeginInvoke(action, DispatcherPriority) → Avalonia Dispatcher.Post(action, priority)（参考 AiDevelopmentWindow）。
			Dispatcher.Post(FlushOutput, DispatcherPriority.Background);
		}

		private void AppendOutputText(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			foreach (string line in lines)
			{
				if (line.Length != 0)
				{
					AppendOutput(line);
				}
			}
		}

		private static string StripAnsiEscapes(string text)
		{
			return string.IsNullOrEmpty(text) ? text : AnsiEscapeRegex.Replace(text, "");
		}

		private void ClearOutput()
		{
			lock (_outputLock)
			{
				_pendingOutputLines.Clear();
				_outputFlushScheduled = false;
			}
			if (Dispatcher.CheckAccess())
			{
				// 阶段 4.5：WPF OutputTextBox.Document.Blocks.Clear() → Avalonia OutputTextBox.Children.Clear()（StackPanel 模型）。
				OutputTextBox.Children.Clear();
				_outputLineCount = 0;
				return;
			}
			Dispatcher.Invoke(delegate
			{
				OutputTextBox.Children.Clear();
				_outputLineCount = 0;
			});
		}

		private int _outputLineCount;

		private void FlushOutput()
		{
			List<string> lines;
			lock (_outputLock)
			{
				lines = new List<string>(_pendingOutputLines);
				_pendingOutputLines.Clear();
				_outputFlushScheduled = false;
			}
			foreach (string line in lines)
			{
				AppendOutputLine(line);
			}
			if (lines.Count > 0)
			{
				ScrollOutputToEnd();
			}
		}

		// 阶段 4.5：WPF RichTextBox.ScrollToEnd() → Avalonia 查找父 ScrollViewer.ScrollToEnd()。
		// OutputTextBox 为 StackPanel，外层 ScrollViewer 负责滚动（XAML 需同步迁移为 ScrollViewer > StackPanel）。
		private void ScrollOutputToEnd()
		{
			ScrollViewer scrollViewer = OutputTextBox.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
			scrollViewer?.ScrollToEnd();
		}

		private void AppendOutputLine(string text)
		{
			// 阶段 4.5：WPF Paragraph（FlowDocument 块）→ Avalonia TextBlock（Inlines 集合等价）。
			TextBlock lineBlock = new TextBlock
			{
				Margin = new Thickness(0.0)
			};
			if (_outputLineCount < RichOutputLineLimit)
			{
				AppendOutputInlines(lineBlock.Inlines, text);
			}
			else
			{
				AddRun(lineBlock.Inlines, StripAnsiEscapes(text ?? ""), null);
			}
			// 阶段 4.5：WPF OutputTextBox.Document.Blocks.Add(paragraph) → Avalonia OutputTextBox.Children.Add(textBlock)。
			OutputTextBox.Children.Add(lineBlock);
			_outputLineCount++;
			TrimOutputLines();
		}

		private void TrimOutputLines()
		{
			// 阶段 4.5：WPF Document.Blocks.FirstBlock/Remove → Avalonia Children[0]/RemoveAt(0)。
			while (_outputLineCount > MaxOutputLineCount && OutputTextBox.Children.Count > 0)
			{
				OutputTextBox.Children.RemoveAt(0);
				_outputLineCount--;
			}
		}

		private void AppendOutputInlines(InlineCollection inlines, string text)
		{
			foreach (OutputSegment segment in ParseAnsiSegments(text ?? ""))
			{
				AppendOutputInlines(inlines, segment.Text, segment.Foreground);
			}
		}

		private void AppendOutputInlines(InlineCollection inlines, string text, [Null] IBrush foreground)
		{
			int lastIndex = 0;
			foreach (Match match in UrlRegex.Matches(text))
			{
				if (match.Index > lastIndex)
				{
					AddRun(inlines, text.Substring(lastIndex, match.Index - lastIndex), foreground);
				}
				string trailingText;
				string url = TrimUrl(match.Value, out trailingText);
				if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
				{
					// 阶段 4.5：WPF new Hyperlink(new Run(url)) → Avalonia new Hyperlink + Inlines.Add(new Run(url))（Span 无 Inline 构造函数）。
					Hyperlink hyperlink = new Hyperlink
					{
						NavigateUri = uri
					};
					hyperlink.Inlines.Add(new Run(url));
					if (foreground != null)
					{
						hyperlink.Foreground = foreground;
					}
					// 阶段 4.5：WPF Hyperlink.RequestNavigate → Avalonia Hyperlink.Click（参考 AccountDetailsUserControl）。
					hyperlink.Click += OutputHyperlink_Click;
					inlines.Add(hyperlink);
				}
				else
				{
					AddRun(inlines, url, foreground);
				}
				if (!string.IsNullOrEmpty(trailingText))
				{
					AddRun(inlines, trailingText, foreground);
				}
				lastIndex = match.Index + match.Length;
			}
			if (lastIndex < text.Length)
			{
				AddRun(inlines, text.Substring(lastIndex), foreground);
			}
		}

		private static void AddRun(InlineCollection inlines, string text, [Null] IBrush foreground)
		{
			Run run = new Run(text);
			if (foreground != null)
			{
				run.Foreground = foreground;
			}
			inlines.Add(run);
		}

		private static IEnumerable<OutputSegment> ParseAnsiSegments(string text)
		{
			int index = 0;
			IBrush foreground = null;
			foreach (Match match in AnsiSgrRegex.Matches(text))
			{
				if (match.Index > index)
				{
					string plainText = StripAnsiEscapes(text.Substring(index, match.Index - index));
					if (!string.IsNullOrEmpty(plainText))
					{
						yield return new OutputSegment(plainText, foreground);
					}
				}
				foreground = ApplyAnsiSgr(match.Groups[1].Value, foreground);
				index = match.Index + match.Length;
			}
			if (index < text.Length)
			{
				string plainText = StripAnsiEscapes(text.Substring(index));
				if (!string.IsNullOrEmpty(plainText))
				{
					yield return new OutputSegment(plainText, foreground);
				}
			}
		}

		// 阶段 4.5：WPF Brush → Avalonia IBrush（Brushes.XXX 返回 ISolidColorBrush 实现 IBrush）。
		private static IBrush ApplyAnsiSgr(string sgr, [Null] IBrush currentForeground)
		{
			if (string.IsNullOrWhiteSpace(sgr))
			{
				return null;
			}
			IBrush foreground = currentForeground;
			foreach (string part in sgr.Split(';'))
			{
				if (!int.TryParse(part, out int code))
				{
					continue;
				}
				switch (code)
				{
					case 0:
					case 39:
						foreground = null;
						break;
					case 30:
						foreground = Brushes.Black;
						break;
					case 31:
						foreground = Brushes.IndianRed;
						break;
					case 32:
						foreground = Brushes.ForestGreen;
						break;
					case 33:
						foreground = Brushes.Goldenrod;
						break;
					case 34:
						foreground = Brushes.DodgerBlue;
						break;
					case 35:
						foreground = Brushes.MediumOrchid;
						break;
					case 36:
						foreground = Brushes.DarkCyan;
						break;
					case 37:
						foreground = Brushes.LightGray;
						break;
					case 90:
						foreground = Brushes.Gray;
						break;
					case 91:
						foreground = Brushes.Red;
						break;
					case 92:
						foreground = Brushes.LimeGreen;
						break;
					case 93:
						foreground = Brushes.Gold;
						break;
					case 94:
						foreground = Brushes.DeepSkyBlue;
						break;
					case 95:
						foreground = Brushes.Orchid;
						break;
					case 96:
						foreground = Brushes.Cyan;
						break;
					case 97:
						foreground = Brushes.White;
						break;
				}
			}
			return foreground;
		}

		private static string TrimUrl(string value, out string trailingText)
		{
			value = value ?? "";
			string url = value.TrimEnd('.', ',', ';', ':', ')', ']', '\'', '"');
			trailingText = value.Substring(url.Length);
			return url;
		}

		private static string CleanUrl(string value)
		{
			string text = StripAnsiEscapes(value ?? "").Trim().Trim('\'', '"');
			return TrimUrl(text, out _);
		}

		private static bool TryCreateHttpUri(string link, out Uri uri)
		{
			uri = null;
			string cleaned = CleanUrl(link);
			return Uri.TryCreate(cleaned, UriKind.Absolute, out uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
		}

		private static void OpenUrl(string link)
		{
			if (!TryCreateHttpUri(link, out Uri uri))
			{
				Log.Warn("Ignoring invalid upload URL: " + link);
				return;
			}
			try
			{
				uri.OpenInBrowser();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to open upload URL: " + uri, ex);
			}
		}

		// 阶段 4.5：WPF RequestNavigateEventArgs.Uri → Avalonia Hyperlink.NavigateUri（参考 HighlightingTextBlockExtensions）。
		private void OutputHyperlink_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Hyperlink hyperlink)
			{
				OpenUrl(hyperlink.NavigateUri?.AbsoluteUri);
			}
			e.Handled = true;
		}

		private static string[] ExtractUrls(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return new string[0];
			}
			return UrlRegex.Matches(text)
				.OfType<Match>()
				.Select((Match match) => CleanUrl(match.Value))
				.Where((string url) => TryCreateHttpUri(url, out _))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private void RefreshUploadLinksPanel(string[] links)
		{
			RefreshUploadLinksPanel(links, autoHide: true);
		}

		private void RefreshUploadLinksPanel(string[] links, bool autoHide)
		{
			_uploadLinksAutoHideTimer.Stop();
			_uploadLinksAutoHideTimer.Tick -= UploadLinksAutoHideTimer_Tick;
			_uploadLinksAutoHideTimer.Tick += UploadLinksAutoHideTimer_Tick;
			_latestUploadLinks = (links ?? new string[0])
				.Select(CleanUrl)
				.Where((string link) => TryCreateHttpUri(link, out _))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			UploadLinksPanel.Children.Clear();
			if (_latestUploadLinks.Length == 0)
			{
				UploadLinksContainer.Collapse();
				ShowUploadLinksButton.Collapse();
				return;
			}
			UploadLinksContainer.Show();
			ShowUploadLinksButton.Collapse();
			foreach (string link in _latestUploadLinks.Subsequence(0, 5))
			{
				Button button = new Button
				{
					Content = new TextBlock
					{
						Text = UploadLinkTitle(link),
						TextTrimming = TextTrimming.CharacterEllipsis,
						MaxWidth = 260.0
					},
					Style = Theme.TransparentButtonStyle,
					// 阶段 4.5：WPF Application.Current.TryFindResource("AccentBrush") as Brush → Theme.FindBrush("AccentBrush")（参考 ActivityManagerUserControl）。
					Foreground = Theme.FindBrush("AccentBrush"),
					FontSize = 12.0,
					Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
					Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
				};
				// 阶段 4.5：WPF button.ToolTip = link → Avalonia ToolTip.SetTip(button, link)（参考 FileControlHeaderUserControl）。
				ToolTip.SetTip(button, link);
				button.Click += delegate
				{
					OpenUrl(link);
				};
				UploadLinksPanel.Children.Add(button);
			}
			if (autoHide)
			{
				_uploadLinksAutoHideTimer.Start();
			}
		}

		private void HideUploadLinksButton_Click(object sender, RoutedEventArgs e)
		{
			HideUploadLinksPanel();
		}

		private static string UploadLinkTitle(string link)
		{
			if (TryCreateHttpUri(link, out var uri))
			{
				string path = uri.AbsolutePath.Trim('/');
				if (!string.IsNullOrWhiteSpace(path))
				{
					return uri.Host + "/" + path;
				}
				return uri.Host;
			}
			return link;
		}

		private void ShowUploadLinksButton_Click(object sender, RoutedEventArgs e)
		{
			SaveUploadLinksCollapsed(isCollapsed: false);
			RefreshUploadLinksPanel(_latestUploadLinks, autoHide: true);
		}

		private void UploadLinksAutoHideTimer_Tick(object sender, EventArgs e)
		{
			HideUploadLinksPanel();
		}

		private void HideUploadLinksPanel()
		{
			HideUploadLinksPanel(save: true);
		}

		private void HideUploadLinksPanel(bool save)
		{
			_uploadLinksAutoHideTimer.Stop();
			UploadLinksContainer.Collapse();
			ShowUploadLinksButton.Hide(_latestUploadLinks.Length == 0);
			if (save && _latestUploadLinks.Length > 0)
			{
				SaveUploadLinksCollapsed(isCollapsed: true);
			}
		}
	}
}
