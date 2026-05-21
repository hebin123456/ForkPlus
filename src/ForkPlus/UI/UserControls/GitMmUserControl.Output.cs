using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

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

			[Null]
			public Brush Foreground { get; }

			public OutputSegment(string text, [Null] Brush foreground)
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
			Dispatcher.BeginInvoke(new Action(FlushOutput), DispatcherPriority.Background);
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
				OutputTextBox.Document.Blocks.Clear();
				_outputLineCount = 0;
				return;
			}
			Dispatcher.Invoke(delegate
			{
				OutputTextBox.Document.Blocks.Clear();
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
				OutputTextBox.ScrollToEnd();
			}
		}

		private void AppendOutputLine(string text)
		{
			Paragraph paragraph = new Paragraph
			{
				Margin = new Thickness(0.0)
			};
			if (_outputLineCount < RichOutputLineLimit)
			{
				AppendOutputInlines(paragraph.Inlines, text);
			}
			else
			{
				AddRun(paragraph.Inlines, StripAnsiEscapes(text ?? ""), null);
			}
			OutputTextBox.Document.Blocks.Add(paragraph);
			_outputLineCount++;
			TrimOutputLines();
		}

		private void TrimOutputLines()
		{
			while (_outputLineCount > MaxOutputLineCount && OutputTextBox.Document.Blocks.FirstBlock != null)
			{
				OutputTextBox.Document.Blocks.Remove(OutputTextBox.Document.Blocks.FirstBlock);
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

		private void AppendOutputInlines(InlineCollection inlines, string text, [Null] Brush foreground)
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
					Hyperlink hyperlink = new Hyperlink(new Run(url))
					{
						NavigateUri = uri
					};
					if (foreground != null)
					{
						hyperlink.Foreground = foreground;
					}
					hyperlink.RequestNavigate += OutputHyperlink_RequestNavigate;
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

		private static void AddRun(InlineCollection inlines, string text, [Null] Brush foreground)
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
			Brush foreground = null;
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

		private static Brush ApplyAnsiSgr(string sgr, [Null] Brush currentForeground)
		{
			if (string.IsNullOrWhiteSpace(sgr))
			{
				return null;
			}
			Brush foreground = currentForeground;
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

		private void OutputHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			OpenUrl(e.Uri?.AbsoluteUri);
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
					Foreground = Application.Current.TryFindResource("AccentBrush") as System.Windows.Media.Brush,
					ToolTip = link,
					FontSize = 12.0,
					Padding = new Thickness(6.0, 1.0, 6.0, 1.0),
					Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
				};
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
