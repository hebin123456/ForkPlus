// WPF → Avalonia 迁移兼容层：WebView2 占位实现的 HTML 子集渲染器。
//
// 背景（真机 bug#10 根因）：原 WPF 版所有 AI 弹窗（AI 辅助开发 / AI 解释 / AI 代码评审 /
// git mm 手册）的输出都靠 WebView2 渲染 md-ai-output.css 排版的 HTML；迁移时 WebView2 被
// 替换成 TextBlock 占位（NavigateToString 只做去标签纯文本显示），于是 AI 输出全部
// "没有样式"——标题不区分、代码块没有底色边框、列表没有缩进、表格糊成一坨。
//
// 本渲染器把这套 HTML（Biturbo md→html 的输出子集 + AiCodeReviewWindow 手工拼接的
// ai-status/ai-suggestion/details/button 结构）直接解析成 Avalonia 控件树，视觉对齐
// WPF 版 md-ai-output.css（GitHub 风格 markdown 排版，明/暗两套配色跟随 prefers-color-scheme）。
//
// 支持的标签子集（Biturbo bt_md_to_html 实测输出 + 调用方手工 HTML）：
//   块级：h1-h6 p ul ol li blockquote pre hr div details summary table thead tbody tr th td
//   行内：strong b em i code a br span u del s sub sup img（占位文本）
//   交互：button[onclick='previewSuggestion(N)/applySuggestion(N)'] → WebMessage 回调
//   样式：class="ai-current-file|ai-status|ai-suggestion|ai-empty|ai-all-results"、
//         style="color:#xxx"（ShowError 的红色提示用）
//
// 已知取舍（Avalonia TextBlock 行内无背景色/无内嵌控件）：
//   - 行内 code 只有等宽字体（CSS 的 5% 灰底渲染不出来），代码块 pre 完整还原；
//   - <a> 只有着色（CSS 本来也默认无下划线），不可点击；

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.UI.WpfCompat
{
	internal static class MarkdownHtmlRenderer
	{
		// ── 配色：对齐 md-ai-output.css 的明/暗两套 ──

		// 线程安全说明（2026-09-04，全量套件随机 3~7 例 VerifyAccess 失败的根因）：
		// SolidColorBrush 是 AvaloniaObject，有创建线程亲和。此前 Light/Dark 作为静态
		// 只读字段在类型首次触达线程上创建画刷，被两条线程共享访问——headless 套件里
		// WebView2StubTests（UI 线程走降级渲染）与 MarkdownHtmlRendererTests（worker
		// 线程直调 Render）谁先触达谁持有，输的一方读 .Color 即抛
		// "calling thread cannot access this object"。颜色值（struct，线程安全）静态缓存，
		// 画刷实例每次 Render 在调用线程上新建——谁创建谁访问，永不跨线程。
		private sealed class Palette
		{
			public Brush Body;
			public Brush Text;
			public Brush Link;
			public Brush Muted;
			public Brush PreBg;
			public Brush PreBorder;
			public Brush BlockBar;
			public Brush Hr;
			public Brush H1Border;
			public Brush TableBorder;
			public Brush TableStripe;
			public Brush TableHeaderBg;
		}

		private static Palette CreatePalette(bool dark)
		{
			return dark
				? new Palette
				{
					Body = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
					Text = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
					Link = new SolidColorBrush(Color.FromRgb(0x42, 0x9C, 0xFF)),
					Muted = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
					PreBg = new SolidColorBrush(Color.FromRgb(0x29, 0x2A, 0x2F)),
					PreBorder = new SolidColorBrush(Color.FromRgb(0x3E, 0x3F, 0x44)),
					BlockBar = new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x39)),
					Hr = new SolidColorBrush(Color.FromRgb(0x40, 0x3F, 0x3E)),
					H1Border = new SolidColorBrush(Color.FromRgb(0x3A, 0x39, 0x39)),
					TableBorder = new SolidColorBrush(Color.FromRgb(0x3E, 0x3F, 0x44)),
					TableStripe = new SolidColorBrush(Color.FromRgb(0x2E, 0x2F, 0x33)),
					TableHeaderBg = new SolidColorBrush(Color.FromRgb(0x24, 0x25, 0x29)),
				}
				: new Palette
				{
					Body = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
					Text = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)),
					Link = new SolidColorBrush(Color.FromRgb(0x03, 0x66, 0xD6)),
					Muted = new SolidColorBrush(Color.FromRgb(0x6A, 0x73, 0x7D)),
					PreBg = new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA)),
					PreBorder = new SolidColorBrush(Color.FromRgb(0xE6, 0xE5, 0xE6)),
					BlockBar = new SolidColorBrush(Color.FromRgb(0xDF, 0xE2, 0xE5)),
					Hr = new SolidColorBrush(Color.FromRgb(0xC5, 0xC5, 0xC5)),
					H1Border = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
					TableBorder = new SolidColorBrush(Color.FromRgb(0xDF, 0xE2, 0xE5)),
					TableStripe = new SolidColorBrush(Color.FromRgb(0xF6, 0xF8, 0xFA)),
					TableHeaderBg = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
				};
		}

		private const double BaseFontSize = 13.0;       // CSS body font-size: 13px
		private const double LineHeight = 19.5;         // 13 * 1.5
		private static readonly FontFamily MonoFont = new FontFamily("Consolas, $Default");

		private sealed class RenderContext
		{
			public Palette Pal;
			public Action<string> OnWebMessage;
		}

		// ── 入口 ──

		/// <summary>把 HTML body 内容渲染成带 md-ai-output.css 排版的 Avalonia 控件树。</summary>
		public static Control Render(string bodyHtml, bool dark, Action<string> onWebMessage)
		{
			Palette pal = CreatePalette(dark);
			var ctx = new RenderContext { Pal = pal, OnWebMessage = onWebMessage };
			StackPanel blocks = new StackPanel();
			foreach (Node node in Parse(bodyHtml))
			{
				RenderBlockInto(blocks, node, ctx);
			}
			// CSS body: padding 16px + 背景色（WPF 里 WebView2 直接画出这块底色，气泡内也一样）
			return new Border
			{
				Background = pal.Body,
				Padding = new Thickness(16),
				Child = blocks
			};
		}

		// ── HTML 解析（容错子集解析器：未闭合自动兜底、未知标签按块透传） ──

		private sealed class Node
		{
			public string Tag = "";                 // "" = 文本节点
			public string Text = "";                 // 文本节点的原文（未折叠空白）
			public readonly Dictionary<string, string> Attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			public readonly List<Node> Children = new List<Node>();
		}

		private static readonly HashSet<string> VoidTags = new HashSet<string>
		{
			"hr", "br", "img", "meta", "link", "input", "area", "source", "wbr"
		};

		private static readonly HashSet<string> BlockTags = new HashSet<string>
		{
			"h1", "h2", "h3", "h4", "h5", "h6", "p", "ul", "ol", "li", "blockquote",
			"pre", "hr", "div", "details", "table", "thead", "tbody", "tr", "th", "td", "button"
		};

		private static List<Node> Parse(string html)
		{
			Node root = new Node { Tag = "#root" };
			var stack = new List<Node> { root };
			int i = 0;
			while (i < html.Length)
			{
				int lt = html.IndexOf('<', i);
				if (lt < 0)
				{
					AddText(stack[stack.Count - 1], html.Substring(i));
					break;
				}
				if (lt > i)
				{
					AddText(stack[stack.Count - 1], html.Substring(i, lt - i));
				}
				int gt = html.IndexOf('>', lt);
				if (gt < 0)
				{
					break;
				}
				string inner = html.Substring(lt + 1, gt - lt - 1);
				i = gt + 1;
				if (inner.StartsWith("!--", StringComparison.Ordinal) || inner.StartsWith("!", StringComparison.Ordinal) || inner.StartsWith("?", StringComparison.Ordinal))
				{
					continue;
				}
				if (inner.StartsWith("/", StringComparison.Ordinal))
				{
					string closeName = inner.Substring(1).Trim().ToLowerInvariant();
					for (int k = stack.Count - 1; k > 0; k--)
					{
						if (stack[k].Tag == closeName)
						{
							stack.RemoveRange(k, stack.Count - k);
							break;
						}
					}
					continue;
				}
				Node node = ParseTag(inner);
				stack[stack.Count - 1].Children.Add(node);
				if (!VoidTags.Contains(node.Tag))
				{
					stack.Add(node);
				}
			}
			return root.Children;
		}

		private static void AddText(Node parent, string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			parent.Children.Add(new Node { Tag = "", Text = WebUtility.HtmlDecode(text) });
		}

		private static Node ParseTag(string inner)
		{
			Node node = new Node();
			int sp = 0;
			while (sp < inner.Length && !char.IsWhiteSpace(inner[sp]))
			{
				sp++;
			}
			node.Tag = inner.Substring(0, sp).ToLowerInvariant();
			// 解析属性：name / name="v" / name='v' / name=v
			int p = sp;
			while (p < inner.Length)
			{
				while (p < inner.Length && char.IsWhiteSpace(inner[p]))
				{
					p++;
				}
				if (p >= inner.Length)
				{
					break;
				}
				int ns = p;
				while (p < inner.Length && inner[p] != '=' && !char.IsWhiteSpace(inner[p]))
				{
					p++;
				}
				string name = inner.Substring(ns, p - ns);
				string value = "";
				if (p < inner.Length && inner[p] == '=')
				{
					p++;
					if (p < inner.Length && (inner[p] == '"' || inner[p] == '\''))
					{
						char q = inner[p++];
						int vs = p;
						while (p < inner.Length && inner[p] != q)
						{
							p++;
						}
						value = inner.Substring(vs, p - vs);
						if (p < inner.Length)
						{
							p++;
						}
					}
					else
					{
						int vs = p;
						while (p < inner.Length && !char.IsWhiteSpace(inner[p]))
						{
							p++;
						}
						value = inner.Substring(vs, p - vs);
					}
				}
				if (name.Length > 0 && !node.Attrs.ContainsKey(name))
				{
					node.Attrs[name] = WebUtility.HtmlDecode(value);
				}
			}
			return node;
		}

		// ── 块级渲染 ──

		private static void RenderBlockInto(Panel parent, Node node, RenderContext ctx)
		{
			switch (node.Tag)
			{
				case "style":
				case "script":
				case "head":
					return;
				case "h1":
				case "h2":
				case "h3":
				case "h4":
				case "h5":
				case "h6":
					parent.Children.Add(RenderHeading(node, ctx));
					return;
				case "p":
					parent.Children.Add(RenderParagraph(node, ctx));
					return;
				case "ul":
				case "ol":
					RenderList(parent, node, ctx);
					return;
				case "blockquote":
					parent.Children.Add(RenderBlockquote(node, ctx));
					return;
				case "pre":
					parent.Children.Add(RenderPre(node, ctx));
					return;
				case "hr":
					parent.Children.Add(new Border
					{
						Height = 1,
						Background = ctx.Pal.Hr,
						Margin = new Thickness(0, 12, 0, 12),
						HorizontalAlignment = HorizontalAlignment.Stretch
					});
					return;
				case "div":
					parent.Children.Add(RenderDiv(node, ctx));
					return;
				case "details":
					parent.Children.Add(RenderDetails(node, ctx));
					return;
				case "button":
					Control button = RenderButton(node, ctx);
					if (button != null)
					{
						parent.Children.Add(button);
					}
					return;
				case "table":
					parent.Children.Add(RenderTable(node, ctx));
					return;
				case "":
					// 顶层裸文本 → 按段落处理
					if (!string.IsNullOrWhiteSpace(node.Text))
					{
						parent.Children.Add(MakeTextBlock(BuildInlines(node, ctx), ctx));
					}
					return;
				default:
					// 未知块标签：内容透传
					StackPanel fallback = new StackPanel();
					foreach (Node child in node.Children)
					{
						RenderBlockInto(fallback, child, ctx);
					}
					if (fallback.Children.Count > 0)
					{
						parent.Children.Add(fallback);
					}
					return;
			}
		}

		private static Control RenderHeading(Node node, RenderContext ctx)
		{
			// CSS：h* { margin 6/6; font-weight 浏览器默认 bold }；h1 1em+下边框；h2 1.3em…
			double size = node.Tag switch
			{
				"h1" => BaseFontSize,
				"h2" => BaseFontSize * 1.3,
				"h3" => BaseFontSize * 1.1,
				"h4" => BaseFontSize,
				"h5" => BaseFontSize * 0.875,
				_ => BaseFontSize * 0.85,
			};
			TextBlock text = MakeTextBlock(BuildInlines(node, ctx), ctx);
			text.FontSize = size;
			text.FontWeight = FontWeights.Bold;
			if (node.Tag == "h6")
			{
				text.Foreground = ctx.Pal.Muted;
			}
			if (node.Tag == "h1")
			{
				return new Border
				{
					BorderBrush = ctx.Pal.H1Border,
					BorderThickness = new Thickness(0, 0, 0, 1),
					Padding = new Thickness(0, 0, 0, 5),
					Margin = new Thickness(0, 6, 0, 6),
					Child = text
				};
			}
			text.Margin = new Thickness(0, 6, 0, 6);
			return text;
		}

		private static Control RenderParagraph(Node node, RenderContext ctx)
		{
			TextBlock text = MakeTextBlock(BuildInlines(node, ctx), ctx);
			string cls = node.Attrs.TryGetValue("class", out string c) ? c : "";
			if (cls.Contains("ai-empty"))
			{
				text.Foreground = ctx.Pal.Muted;
				text.Margin = new Thickness(0, 8, 0, 14);
			}
			else
			{
				text.Margin = new Thickness(0, 0, 0, 5);
			}
			// style='color:#d33'（ShowError 的红色错误文本）
			Brush inline = ParseStyleColor(node);
			if (inline != null)
			{
				text.Foreground = inline;
			}
			return text;
		}

		private static Control RenderDiv(Node node, RenderContext ctx)
		{
			string cls = node.Attrs.TryGetValue("class", out string c) ? c : "";
			if (cls.Contains("ai-current-file") || cls.Contains("ai-status") || cls.Contains("ai-suggestion"))
			{
				// AiCodeReviewWindow 的状态条/建议卡样式（原 CSS 内联在 RenderAiReviewOutput 里）
				bool status = cls.Contains("ai-status");
				Border card = new Border
				{
					BorderBrush = status
						? new SolidColorBrush(Color.FromArgb(0x33, 0x2E, 0x7D, 0x32))
						: new SolidColorBrush(Color.FromArgb(0x33, 0x88, 0x88, 0x88)),
					Background = status
						? new SolidColorBrush(Color.FromArgb(0x18, 0x2E, 0x7D, 0x32))
						: new SolidColorBrush(Color.FromArgb(0x11, 0x88, 0x88, 0x88)),
					BorderThickness = new Thickness(1),
					CornerRadius = new CornerRadius(4),
					Padding = new Thickness(8),
					Margin = status ? new Thickness(0, 0, 0, 12) : new Thickness(0, 10, 0, 10)
				};
				StackPanel inner = new StackPanel();
				bool hasBlock = HasBlockChild(node);
				if (hasBlock)
				{
					foreach (Node child in node.Children)
					{
						RenderBlockInto(inner, child, ctx);
					}
				}
				else
				{
					TextBlock text = MakeTextBlock(BuildInlines(node, ctx), ctx);
					if (cls.Contains("ai-current-file"))
					{
						text.FontSize = 12; // .ai-current-file{font-size:12px}
					}
					inner.Children.Add(text);
				}
				card.Child = inner;
				return card;
			}
			// 普通 div：透明容器，内容透传
			StackPanel panel = new StackPanel();
			foreach (Node child in node.Children)
			{
				RenderBlockInto(panel, child, ctx);
			}
			if (panel.Children.Count == 1 && panel.Children[0] is Control only)
			{
				// 单子节点时直接返回该控件（必须先从中间面板摘除，否则双重父级）
				panel.Children.Clear();
				return only;
			}
			return panel;
		}

		private static Control RenderDetails(Node node, RenderContext ctx)
		{
			// <details class='ai-all-results'><summary>…</summary>…</details>
			Node summary = null;
			var bodyNodes = new List<Node>();
			foreach (Node child in node.Children)
			{
				if (child.Tag == "summary" && summary == null)
				{
					summary = child;
				}
				else
				{
					bodyNodes.Add(child);
				}
			}
			StackPanel body = new StackPanel();
			foreach (Node child in bodyNodes)
			{
				RenderBlockInto(body, child, ctx);
			}
			TextBlock header = MakeTextBlock(summary != null ? BuildInlines(summary, ctx) : BuildInlines(new Node(), ctx), ctx);
			header.FontWeight = FontWeights.SemiBold;
			Expander expander = new Expander
			{
				Header = header,
				Content = body
			};
			string cls = node.Attrs.TryGetValue("class", out string c) ? c : "";
			if (cls.Contains("ai-all-results"))
			{
				expander.Margin = new Thickness(0, 18, 0, 0);
			}
			return expander;
		}

		private static Control RenderButton(Node node, RenderContext ctx)
		{
			// AiCodeReviewWindow 建议卡按钮：<button onclick='previewSuggestion(0)'>…
			if (!node.Attrs.TryGetValue("onclick", out string onclick) || ctx.OnWebMessage == null)
			{
				TextBlock plain = MakeTextBlock(BuildInlines(node, ctx), ctx);
				plain.FontSize = 12;
				return plain;
			}
			Match m = Regex.Match(onclick, @"^\s*([A-Za-z_]\w*)\s*\(\s*(\d+)\s*\)\s*$");
			if (!m.Success)
			{
				TextBlock plain = MakeTextBlock(BuildInlines(node, ctx), ctx);
				plain.FontSize = 12;
				return plain;
			}
			string function = m.Groups[1].Value;
			int index = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
			string message = function switch
			{
				"previewSuggestion" => "preview-suggestion:" + index,
				"applySuggestion" => "apply-suggestion:" + index,
				_ => function + ":" + index,
			};
			var button = new Button
			{
				Content = CollectText(node),
				FontSize = 12,
				Padding = new Thickness(8, 4, 8, 4),
				Margin = new Thickness(0, 8, 6, 0),
				HorizontalAlignment = HorizontalAlignment.Left
			};
			button.Click += delegate
			{
				ctx.OnWebMessage(message);
			};
			return button;
		}

		private static Control RenderTable(Node node, RenderContext ctx)
		{
			// 收集行：thead>tr>th + tbody>tr>td
			var rows = new List<List<Node>>();
			var headerFlags = new List<bool>();
			CollectTableRows(node, rows, headerFlags);
			if (rows.Count == 0)
			{
				return new StackPanel();
			}
			int columns = 0;
			foreach (List<Node> row in rows)
			{
				columns = Math.Max(columns, row.Count);
			}
			var grid = new Grid();
			for (int c = 0; c < columns; c++)
			{
				grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			}
			for (int r = 0; r < rows.Count; r++)
			{
				grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				bool isHeader = headerFlags[r];
				bool stripe = r > 0 && r % 2 == 1;
				for (int c = 0; c < rows[r].Count; c++)
				{
					Node cell = rows[r][c];
					var border = new Border
					{
						BorderBrush = ctx.Pal.TableBorder,
						BorderThickness = new Thickness(1),
						Padding = new Thickness(13, 6, 13, 6),
						Background = isHeader ? ctx.Pal.TableHeaderBg : (stripe ? ctx.Pal.TableStripe : null),
						VerticalAlignment = VerticalAlignment.Stretch
					};
					TextBlock text = MakeTextBlock(BuildInlines(cell, ctx), ctx);
					if (isHeader)
					{
						text.FontWeight = FontWeights.Bold;
					}
					border.Child = text;
					Grid.SetRow(border, r);
					Grid.SetColumn(border, c);
					grid.Children.Add(border);
				}
			}
			return new Border
			{
				BorderBrush = ctx.Pal.TableBorder,
				BorderThickness = new Thickness(1),
				Margin = new Thickness(0, 2, 0, 6),
				Child = grid
			};
		}

		private static void CollectTableRows(Node node, List<List<Node>> rows, List<bool> headerFlags)
		{
			if (node.Tag == "tr")
			{
				var cells = new List<Node>();
				bool header = false;
				foreach (Node child in node.Children)
				{
					if (child.Tag == "th" || child.Tag == "td")
					{
						cells.Add(child);
						header = header || child.Tag == "th";
					}
				}
				if (cells.Count > 0)
				{
					rows.Add(cells);
					headerFlags.Add(header);
				}
				return;
			}
			foreach (Node child in node.Children)
			{
				CollectTableRows(child, rows, headerFlags);
			}
		}

		private static void RenderList(Panel parent, Node node, RenderContext ctx)
		{
			bool ordered = node.Tag == "ol";
			var list = new StackPanel
			{
				Margin = new Thickness(26, 0, 0, 5) // CSS padding-left: 2em
			};
			int index = 1;
			foreach (Node child in node.Children)
			{
				if (child.Tag != "li")
				{
					RenderBlockInto(list, child, ctx);
					continue;
				}
				var row = new Grid();
				row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
				row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
				var marker = new TextBlock
				{
					Text = ordered ? index.ToString(CultureInfo.InvariantCulture) + "." : "•",
					Foreground = ctx.Pal.Text,
					FontSize = BaseFontSize,
					Margin = new Thickness(0, 0, 8, 2),
					VerticalAlignment = VerticalAlignment.Top
				};
				Grid.SetColumn(marker, 0);
				row.Children.Add(marker);
				var content = new StackPanel();
				Grid.SetColumn(content, 1);
				if (HasBlockChild(child))
				{
					foreach (Node sub in child.Children)
					{
						RenderBlockInto(content, sub, ctx);
					}
				}
				else
				{
					TextBlock text = MakeTextBlock(BuildInlines(child, ctx), ctx);
					text.VerticalAlignment = VerticalAlignment.Top;
					content.Children.Add(text);
				}
				row.Children.Add(content);
				row.Margin = new Thickness(0, index == 1 && !ordered ? 0 : 3, 0, 0);
				list.Children.Add(row);
				index++;
			}
			parent.Children.Add(list);
		}

		private static Control RenderBlockquote(Node node, RenderContext ctx)
		{
			// CSS：border-left 4px #dfe2e5；color #6a737d；padding 0 1em
			var inner = new StackPanel();
			foreach (Node child in node.Children)
			{
				RenderBlockInto(inner, child, ctx);
			}
			return new Border
			{
				BorderBrush = ctx.Pal.BlockBar,
				BorderThickness = new Thickness(4, 0, 0, 0),
				Padding = new Thickness(16, 0, 16, 0),
				Child = inner
			};
		}

		private static Control RenderPre(Node node, RenderContext ctx)
	{
		// CSS：pre { bg #f6f8fa; border #E6E5E6; radius 5; padding 8; 85% 等宽; overflow auto }
		string code = CollectRawText(node).TrimEnd('\n');
		// SelectableTextBlock（2026-09-04，问题E）：代码块是手册/AI 输出最需要复制的内容，
		// WPF 版 WebView2 是浏览器内容天然可选中复制，TextBlock 不可选中。
		var text = new SelectableTextBlock
		{
			Text = code,
			FontFamily = MonoFont,
			FontSize = 11,       // 85% of 13
			LineHeight = 16,     // 1.45
			TextWrapping = TextWrapping.NoWrap,
			Foreground = ctx.Pal.Text
		};
			Border border = new Border
			{
				Background = ctx.Pal.PreBg,
				BorderBrush = ctx.Pal.PreBorder,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(5),
				Padding = new Thickness(8),
				Margin = new Thickness(0, 8, 0, 8),
				Child = new ScrollViewer
				{
					Content = text,
					HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
					VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
				}
			};
			return border;
		}

		// ── 行内渲染 ──

		private static InlineCollection BuildInlines(Node node, RenderContext ctx)
		{
			return BuildInlines(node, ctx, null);
		}

		private static InlineCollection BuildInlines(Node node, RenderContext ctx, Brush colorOverride)
		{
			var inlines = new InlineCollection();
			AppendInlines(inlines, node, ctx, null, null, colorOverride);
			return inlines;
		}

		private static void AppendInlines(InlineCollection target, Node node, RenderContext ctx, FontWeight? weight, FontStyle? style, Brush colorOverride)
		{
			foreach (Node child in node.Children)
			{
				AppendInlineNode(target, child, ctx, weight, style, colorOverride);
			}
		}

		private static void AppendInlineNode(InlineCollection target, Node node, RenderContext ctx, FontWeight? weight, FontStyle? style, Brush colorOverride)
		{
			switch (node.Tag)
			{
				case "":
				{
					string text = CollapseWhitespace(node.Text);
					if (text.Length == 0)
					{
						return;
					}
					// Migration note：FontWeight/FontStyle/Foreground 只在确有覆盖时设置，
					// 否则会遮蔽 TextBlock 层的样式（标题加粗 / h6 灰字 / 段落红字 / ai-empty）。
					var run = new Run { Text = text };
					if (weight.HasValue)
					{
						run.FontWeight = weight.Value;
					}
					if (style.HasValue)
					{
						run.FontStyle = style.Value;
					}
					if (colorOverride != null)
					{
						run.Foreground = colorOverride;
					}
					target.Add(run);
					return;
				}
				case "strong":
				case "b":
					AppendInlines(target, node, ctx, FontWeights.SemiBold, style, colorOverride);
					return;
				case "em":
				case "i":
					AppendInlines(target, node, ctx, weight, FontStyle.Italic, colorOverride);
					return;
				case "code":
				{
					string text = CollapseWhitespace(node.Text);
					if (node.Children.Count > 0)
					{
						var sb = new StringBuilder();
						CollectRawTextInto(node, sb);
						text = CollapseWhitespace(sb.ToString());
					}
					if (text.Length == 0)
					{
						return;
					}
					var codeRun = new Run
					{
						Text = text,
						FontFamily = MonoFont,
						FontSize = 11,  // 85%
					};
					if (weight.HasValue)
					{
						codeRun.FontWeight = weight.Value;
					}
					if (style.HasValue)
					{
						codeRun.FontStyle = style.Value;
					}
					if (colorOverride != null)
					{
						codeRun.Foreground = colorOverride;
					}
					target.Add(codeRun);
					return;
				}
				case "a":
				{
					Brush linkColor = colorOverride ?? ctx.Pal.Link;
					AppendInlines(target, node, ctx, weight, style, linkColor);
					return;
				}
				case "br":
					target.Add(new LineBreak());
					return;
				case "u":
				{
					// 下划线：用独立 run 段实现
					foreach (Node child in node.Children)
					{
						if (child.Tag == "")
						{
							var uRun = new Run
							{
								Text = CollapseWhitespace(child.Text),
								TextDecorations = TextDecorations.Underline
							};
							if (weight.HasValue)
							{
								uRun.FontWeight = weight.Value;
							}
							if (style.HasValue)
							{
								uRun.FontStyle = style.Value;
							}
							if (colorOverride != null)
							{
								uRun.Foreground = colorOverride;
							}
							target.Add(uRun);
						}
						else
						{
							AppendInlineNode(target, child, ctx, weight, style, colorOverride);
						}
					}
					return;
				}
			case "del":
			case "s":
			case "strike":
				{
					foreach (Node child in node.Children)
					{
						if (child.Tag == "")
						{
							var delRun = new Run
							{
								Text = CollapseWhitespace(child.Text),
								TextDecorations = TextDecorations.Strikethrough
							};
							if (weight.HasValue)
							{
								delRun.FontWeight = weight.Value;
							}
							if (style.HasValue)
							{
								delRun.FontStyle = style.Value;
							}
							if (colorOverride != null)
							{
								delRun.Foreground = colorOverride;
							}
							target.Add(delRun);
						}
						else
						{
							AppendInlineNode(target, child, ctx, weight, style, colorOverride);
						}
					}
					return;
				}
				case "span":
				{
					Brush inline = ParseStyleColor(node) ?? colorOverride;
					AppendInlines(target, node, ctx, weight, style, inline);
					return;
				}
				case "img":
				{
					string alt = node.Attrs.TryGetValue("alt", out string a) ? a : "image";
					target.Add(new Run { Text = "[" + alt + "]", Foreground = ctx.Pal.Muted });
					return;
				}
				default:
					// 未识别的行内/块混排标签：按行内内容展开（li/p/div 混合场景兜底）
					AppendInlines(target, node, ctx, weight, style, colorOverride);
					return;
			}
		}

		// ── 工具 ──

		private static TextBlock MakeTextBlock(InlineCollection inlines, RenderContext ctx)
		{
			// SelectableTextBlock（2026-09-04，问题E"git mm 手册内容无法选中"）：
			// WPF 版手册/AI 输出在 WebView2 浏览器里天然可选中复制；Avalonia TextBlock
			// 不支持选择，替换为 SelectableTextBlock（鼠标拖选 + Ctrl+A/Ctrl+C）。
			// 选区画刷（同日补，"选中文字的样式也不对"）：SelectableTextBlock.RenderTextLayout
			// 只在 SelectionBrush != null 时才画高亮——主题无 SelectableTextBlock 默认样式，
			// 不显式设置选区就"能选但看不见"。用全应用统一选区蓝 #236BD2 + 白字
			// （与 TextBox/ComboBox 的修复10选区样式一致，明暗皮肤下均高对比可读）。
			SelectableTextBlock text = new SelectableTextBlock
			{
				FontSize = BaseFontSize,
				LineHeight = LineHeight,
				TextWrapping = TextWrapping.Wrap,
				// 默认前景色放在块级（Run 仅在覆盖时携带颜色），保证标题加粗/h6 灰字/
				// 段落红字（style='color:#d33'）/ ai-empty 灰字这些块级样式不被行内遮蔽。
				Foreground = ctx.Pal.Text,
				SelectionBrush = new SolidColorBrush(Color.FromRgb(0x23, 0x6B, 0xD2)),
				// 不用 Brushes.White 静态实例——画刷有创建线程亲和（见 CreatePalette 注释），
				// 每次在调用线程新建；Colors.White 是 struct，线程安全。
				SelectionForegroundBrush = new SolidColorBrush(Colors.White)
			};
			text.Inlines = inlines;
			return text;
		}

		private static bool HasBlockChild(Node node)
		{
			foreach (Node child in node.Children)
			{
				if (child.Tag.Length > 0 && BlockTags.Contains(child.Tag))
				{
					return true;
				}
			}
			return false;
		}

		private static string CollapseWhitespace(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return string.Empty;
			}
			var sb = new StringBuilder(text.Length);
			bool inSpace = false;
			foreach (char ch in text)
			{
				if (char.IsWhiteSpace(ch))
				{
					if (!inSpace)
					{
						sb.Append(' ');
						inSpace = true;
					}
				}
				else
				{
					sb.Append(ch);
					inSpace = false;
				}
			}
			return sb.ToString().Trim();
		}

		private static string CollectText(Node node)
		{
			var sb = new StringBuilder();
			CollectRawTextInto(node, sb);
			return CollapseWhitespace(sb.ToString());
		}

		private static string CollectRawText(Node node)
		{
			var sb = new StringBuilder();
			CollectRawTextInto(node, sb);
			return sb.ToString();
		}

		private static void CollectRawTextInto(Node node, StringBuilder sb)
		{
			if (node.Tag.Length == 0)
			{
				sb.Append(node.Text);
				return;
			}
			foreach (Node child in node.Children)
			{
				CollectRawTextInto(child, sb);
			}
		}

		private static readonly Regex ColorRegex = new Regex("color\\s*:\\s*([^;'\"\\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static Brush ParseStyleColor(Node node)
		{
			if (!node.Attrs.TryGetValue("style", out string style) || string.IsNullOrEmpty(style))
			{
				return null;
			}
			Match m = ColorRegex.Match(style);
			if (!m.Success)
			{
				return null;
			}
			try
			{
				string value = m.Groups[1].Value;
				if (value.StartsWith("#", StringComparison.Ordinal))
				{
					string hex = value.Substring(1);
					if (hex.Length == 3)
					{
						hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
					}
					if (hex.Length == 6)
					{
						return new SolidColorBrush(Color.FromRgb(
							byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
							byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
							byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
					}
					if (hex.Length == 8)
					{
						return new SolidColorBrush(Color.FromArgb(
							byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
							byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
							byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
							byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
					}
				}
			}
			catch
			{
				// 非法颜色值忽略
			}
			return null;
		}
	}
}
