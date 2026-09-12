using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.UI.UserControls
{
	public partial class GitMmUserControl
	{
		private const int MaxOutputLineCount = 4000;

		// v4.0.12（2026-09-12，git mm sync 输出洪峰）：git mm sync 认证循环可在秒级产生数千行
		// 输出，FlushOutput 是 UI 线程批量渲染（每行一串 inline），洪峰期间 pending 无界堆积、
		// 一次 Flush 渲染数万行 → UI freeze（用户日志：6s 无心跳）且内存随输出量线性涨。
		// 有界 pending（渲染上限 4000 行的 2 倍余量），溢出丢最旧保最新（终端滚动语义）。
		private const int MaxPendingOutputLines = MaxOutputLineCount * 2;

		private static readonly Regex UrlRegex = new Regex(@"https?://[^\s<>""']+", RegexOptions.Compiled);

		private static readonly Regex AnsiSgrRegex = new Regex(@"\x1B\[([0-9;]*)m", RegexOptions.Compiled);

		// v4.0.12（2026-09-12，"git mm 输出一堆不可见字符被当成乱码"）：原正则只匹配 CSI
		// 序列（ESC[...终符），git mm 经管道输出时常见的其他 ANSI/控制字符全部漏网——
		// 用户看到 ESC、BEL 等以乱码/豆腐块渲染。完整覆盖：
		//   ① CSI：ESC [ 参数 中间 终符（原有，SGR/光标移动/私有序列 ?25l 等）
		//   ② OSC：ESC ] ... BEL 或 ST(ESC \)——设置终端标题/超链接等，未终结（行截断）也吞
		//   ③ 字符集指定：ESC ( B / ESC ) 0 / ESC # 8 等——吃掉 ESC + 指定符 + 终结符
		//   ④ 其他两字符转义：ESC 7 / ESC = / ESC M 等（ESC 后跟任意非 [ ] 字符）
		private static readonly Regex AnsiEscapeRegex = new Regex(
			@"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\)?|[()#%][0-9A-B]?|.)",
			RegexOptions.Compiled);

		// ①~④ 之外的 C0 控制字符（BEL/BS/VT/FF/CR/SO/SI 等）与 DEL：不可渲染，直接删除。
		// 保留 \t（0x09）——制表符有排版意义。\r 虽在 AppendOutputText 分行处理，但 git
		// 进度条类输出（"45%\r78%\r100%"）的行内 CR 会残留——ReadLine 只按 \n 分行，行内
		// \r 一并删除（终端 CR=行首重绘语义，GUI 文本取最终文本即可）。
		// 注意顺序：先删转义序列再删残余控制字符（②④ 未配对吞掉的孤立 ESC 也在此兜底）。
		private static readonly Regex ControlCharRegex = new Regex(
			@"[\x00-\x08\x0B-\x1F\x7F]", RegexOptions.Compiled);

		private readonly object _outputLock = new object();

		private readonly List<string> _pendingOutputLines = new List<string>();

		// 已渲染行（保留原文，含 ANSI 序列；重建/裁剪时重新解析）
		private readonly List<string> _outputLines = new List<string>();

		// 每行已加入 InlineCollection 的 inline 引用（与 _outputLines 一一对应；
		// 超 MaxOutputLineCount 裁掉最旧行时按引用逐个 Remove，避免整树重建）
		private readonly List<List<Inline>> _outputLineInlines = new List<List<Inline>>();

		private bool _outputFlushScheduled;

		private readonly DispatcherTimer _uploadLinksAutoHideTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};

		private string[] _latestUploadLinks = new string[0];

		private sealed class OutputSegment
	{
		public string Text { get; }

		// Migration note：WPF Brush → Avalonia IBrush（Brushes.* 在 Avalonia 12 返回 IImmutableSolidColorBrush）
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
				if (_pendingOutputLines.Count > MaxPendingOutputLines)
				{
					// 洪峰溢出：丢最旧 pending 行（渲染上限会再裁一次，终端滚动语义保最新输出）。
					_pendingOutputLines.RemoveRange(0, _pendingOutputLines.Count - MaxPendingOutputLines);
				}
				if (_outputFlushScheduled)
				{
					return;
				}
				_outputFlushScheduled = true;
			}
			Dispatcher.Post(new Action(FlushOutput), DispatcherPriority.Background);
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

		/// <summary>v4.0.12：完整 ANSI/控制字符清洗——先删 ①~④ 类转义序列，再删残余
		/// C0 控制字符（保留 \t）。供活动管理器 git-mm 视图（GetOutputText）与主视图
		/// 渲染路径（ParseAnsiSegments 纯文本段）共用。internal 供回归测试直调。</summary>
		internal static string StripAnsiEscapes(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}
			string withoutEscapes = AnsiEscapeRegex.Replace(text, "");
			return ControlCharRegex.Replace(withoutEscapes, "");
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
				ClearOutputInlines();
				return;
			}
			Dispatcher.Invoke(ClearOutputInlines);
		}

		/// <summary>
		/// 供活动管理器"git-mm"标签页读取当前命令输出（2026-09-10，git mm 输出收编到活动管理器）。
		/// 返回已渲染行拼接的纯文本（去除 ANSI 转义序列），供 ActivityManagerUserControl 在
		/// git-mm 视图下直接展示。线程安全：在 _outputLock 内快照 _outputLines。
		/// </summary>
		public string GetOutputText()
		{
			lock (_outputLock)
			{
				if (_outputLines.Count == 0)
				{
					return string.Empty;
				}
				return string.Join("\n", _outputLines.ConvertAll(StripAnsiEscapes));
			}
		}

		/// <summary>当前是否有命令输出（活动管理器 git-mm 视图据此决定是否显示空态占位）。</summary>
		public bool HasOutput
		{
			get
			{
				lock (_outputLock)
				{
					return _outputLines.Count > 0;
				}
			}
		}

		private void ClearOutputInlines()
		{
			_outputLines.Clear();
			_outputLineInlines.Clear();
			OutputTextBlock.Inlines?.Clear();
		}

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
			// 跟随最新输出滚动到底部。Render 优先级在布局之后执行——inline 追加引起的
			// 高度变化先落进 Extent，ScrollToEnd 才能真正滚到新底部（同步调用会用旧
			// Extent 计算，差一行高度的"没到底"）。
			Dispatcher.UIThread.Post(delegate
			{
				OutputScrollViewer.ScrollToEnd();
			}, DispatcherPriority.Render);
		}
	}

	private void AppendOutputLine(string text)
	{
		// 富文本路径（2026-09-09 恢复 WPF 原版语义并补齐超链接）：一行 = ANSI 颜色分段
		// → 每段内 URL 切成可点击链接、其余为 Run，行尾 LineBreak。原 TextBox 纯文本
		// 迁移降级（无颜色/无链接/模板无 ScrollViewer 被裁剪）全部在此消除。
		string line = text ?? "";
		_outputLines.Add(line);
		AppendLineInlines(line);
		TrimOutputLines();
	}

	private InlineCollection EnsureOutputInlines()
	{
		if (OutputTextBlock.Inlines == null)
		{
			OutputTextBlock.Inlines = new InlineCollection();
		}
		return OutputTextBlock.Inlines;
	}

	private void AppendLineInlines(string line)
	{
		InlineCollection inlines = EnsureOutputInlines();
		List<Inline> lineInlines = new List<Inline>();
		foreach (OutputSegment segment in ParseAnsiSegments(line))
		{
			AppendSegmentInlines(inlines, lineInlines, segment.Text, segment.Foreground);
		}
		LineBreak lineBreak = new LineBreak();
		inlines.Add(lineBreak);
		lineInlines.Add(lineBreak);
		_outputLineInlines.Add(lineInlines);
	}

	private void AppendSegmentInlines(InlineCollection inlines, List<Inline> lineInlines, string text, [Null] IBrush foreground)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		int lastIndex = 0;
		foreach (Match match in UrlRegex.Matches(text))
		{
			if (match.Index > lastIndex)
			{
				AddRun(inlines, lineInlines, text.Substring(lastIndex, match.Index - lastIndex), foreground);
			}
			string trailingText;
			string url = TrimUrl(match.Value, out trailingText);
			if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
			{
				InlineUIContainer hyperlink = CreateOutputLinkInline(url);
				inlines.Add(hyperlink);
				lineInlines.Add(hyperlink);
			}
			else
			{
				AddRun(inlines, lineInlines, url, foreground);
			}
			if (!string.IsNullOrEmpty(trailingText))
			{
				AddRun(inlines, lineInlines, trailingText, foreground);
			}
			lastIndex = match.Index + match.Length;
		}
		if (lastIndex < text.Length)
		{
			AddRun(inlines, lineInlines, text.Substring(lastIndex), foreground);
		}
	}

	/// <summary>输出区超链接：主题强调色 + 下划线 + 手型光标，按下即打开（与终端一致），
	/// 悬停加亮给出"变色"反馈。InlineUIContainer 使任意 Control 可嵌入 TextBlock 行内。</summary>
	private static InlineUIContainer CreateOutputLinkInline(string url)
	{
		TextBlock linkText = new TextBlock
		{
			Text = url,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 12.0,
			TextDecorations = Avalonia.Media.TextDecorations.Underline,
			Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
			Foreground = GetOutputLinkBrush(),
			Background = Brushes.Transparent,
			TextWrapping = TextWrapping.NoWrap
		};
		linkText.PointerEntered += delegate
		{
			linkText.Foreground = GetOutputLinkHoverBrush();
		};
		linkText.PointerExited += delegate
		{
			linkText.Foreground = GetOutputLinkBrush();
		};
		linkText.PointerPressed += delegate(object sender, Avalonia.Input.PointerPressedEventArgs e)
		{
			OpenUrl(url);
			// 吞掉按下事件：阻止外层 SelectableTextBlock 从链接上起选区（点击=打开，不是选择）。
			e.Handled = true;
		};
		return new InlineUIContainer
		{
			Child = linkText
		};
	}

	private static IBrush GetOutputLinkBrush()
	{
		return (Application.Current != null && Application.Current.TryFindResource("AccentBrush") is IBrush brush) ? brush : Brushes.DodgerBlue;
	}

	private static IBrush GetOutputLinkHoverBrush()
	{
		if (Application.Current != null && Application.Current.TryFindResource("AccentBrush") is ISolidColorBrush solid)
		{
			Color c = solid.Color;
			return new SolidColorBrush(Color.FromRgb(
				(byte)global::System.Math.Min(255, c.R + 40),
				(byte)global::System.Math.Min(255, c.G + 40),
				(byte)global::System.Math.Min(255, c.B + 40)));
		}
		return Brushes.DodgerBlue;
	}

	private void TrimOutputLines()
	{
		InlineCollection inlines = OutputTextBlock.Inlines;
		while (_outputLines.Count > MaxOutputLineCount)
		{
			_outputLines.RemoveAt(0);
			if (_outputLineInlines.Count > 0)
			{
				List<Inline> lineInlines = _outputLineInlines[0];
				_outputLineInlines.RemoveAt(0);
				if (inlines != null)
				{
					foreach (Inline inline in lineInlines)
					{
						inlines.Remove(inline);
					}
				}
			}
		}
	}

	private static void AddRun(InlineCollection inlines, List<Inline> lineInlines, string text, [Null] IBrush foreground)
	{
		Run run = new Run(text);
		if (foreground != null)
		{
			run.Foreground = foreground;
		}
		inlines.Add(run);
		lineInlines.Add(run);
	}

		private static IEnumerable<OutputSegment> ParseAnsiSegments(string text)
	{
		int index = 0;
		// Migration note：Brush → IBrush（Avalonia 12 的 Brushes.* 返回 IImmutableSolidColorBrush）
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

		private static IBrush ApplyAnsiSgr(string sgr, [Null] IBrush currentForeground)
	{
		if (string.IsNullOrWhiteSpace(sgr))
		{
			return null;
		}
		// Migration note：Brush → IBrush（Avalonia 12 的 Brushes.* 颜色表返回 IImmutableSolidColorBrush）
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
			return;
		}
		UploadLinksContainer.Show();
			foreach (string link in _latestUploadLinks.Subsequence(0, 5))
			{
				Button button = global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new Button
				{
					Content = new TextBlock
					{
						Text = UploadLinkTitle(link),
						TextTrimming = TextTrimming.CharacterEllipsis,
						MaxWidth = 260.0
					},					Foreground = Application.Current.TryFindResource("AccentBrush") as global::Avalonia.Media.IBrush,					FontSize = 12.0,					Padding = new Thickness(6.0, 1.0, 6.0, 1.0),					Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
				},link),global::ForkPlus.UI.Theme.TransparentButtonStyle);
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
		if (save && _latestUploadLinks.Length > 0)
		{
			SaveUploadLinksCollapsed(isCollapsed: true);
		}
	}
	}
}
