using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 更新内容 markdown 渲染器（2026-09-17，"更新内容/检查更新弹窗用 markdown 读"）：
	/// ReleaseNotesWindow / UpdateAvailableWindow / UpdateCheckWindow 此前把 markdown
	/// 原文塞进只读 TextBox（Consolas 等宽），用户看到的是带 ##、**、|---| 的源码。
	/// 本渲染器解析 RELEASE_NOTE.md 章节与 GitHub Release body 实际用到的 markdown
	/// 子集（### 标题 / &gt; 摘要引用 / - 列表（**加粗** 与 `行内代码`）/ | 表格 /
	/// ``` 代码围栏 / 普通段落），渲染成原生 Avalonia 控件（无新依赖、可 headless 测试）。
	/// 块解析（ParseBlocks）与行内解析（ParseInlines）为纯函数，行为由单元测试锁定。
	/// </summary>
	public static class MarkdownNotesRenderer
	{
		/// <summary>
		/// 把 markdown 渲染为原生控件并填入目标面板（先清空）。markdown 为空时面板清空。
		/// </summary>
		public static void Render(StackPanel target, string markdown)
		{
			if (target == null)
			{
				throw new ArgumentNullException("target");
			}
			target.Children.Clear();
			if (string.IsNullOrEmpty(markdown))
			{
				return;
			}
			foreach ((string kind, int level, string[] lines) in ParseBlocks(markdown))
			{
				Control control = BuildBlockControl(kind, level, lines, target);
				if (control != null)
				{
					target.Children.Add(control);
				}
			}
		}

		/// <summary>
		/// 块级解析。返回 (kind, level, lines) 列表：
		/// heading（level = # 个数 1..4，lines = [标题文本]）；quote（lines = 去除 "&gt; " 前缀的行）；
		/// bullet（lines = [去掉 "- " 前缀的行内内容]，每个 "- " 行一个块）；
		/// table（lines = 每行单元格以 \t 连接，分隔线行 |---| 已剔除）；
		/// code（``` 围栏内原文行）；para（连续普通行，保留原文换行）。
		/// </summary>
		public static List<(string Kind, int Level, string[] Lines)> ParseBlocks(string markdown)
		{
			var blocks = new List<(string, int, string[])>();
			if (string.IsNullOrEmpty(markdown))
			{
				return blocks;
			}
			string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
			int i = 0;
			while (i < lines.Length)
			{
				string trimmed = lines[i].Trim();
				if (trimmed.Length == 0)
				{
					i++;
					continue;
				}
				// 代码围栏：``` 到 ``` 原样收集
				if (trimmed.StartsWith("```", StringComparison.Ordinal))
				{
					var codeLines = new List<string>();
					i++;
					while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
					{
						codeLines.Add(lines[i]);
						i++;
					}
					if (i < lines.Length)
					{
						i++; // 跳过收尾 ```
					}
					blocks.Add(("code", 0, codeLines.ToArray()));
					continue;
				}
				// 标题：# ~ ####
				if (trimmed.StartsWith("#", StringComparison.Ordinal))
				{
					int level = 0;
					while (level < trimmed.Length && trimmed[level] == '#')
					{
						level++;
					}
					if (level <= 4 && level < trimmed.Length && trimmed[level] == ' ')
					{
						blocks.Add(("heading", level, new[] { trimmed.Substring(level + 1).Trim() }));
						i++;
						continue;
					}
				}
				// 引用块：连续 "> " 行合并
				if (trimmed.StartsWith(">", StringComparison.Ordinal))
				{
					var quoteLines = new List<string>();
					while (i < lines.Length)
					{
						string t = lines[i].Trim();
						if (!t.StartsWith(">", StringComparison.Ordinal))
						{
							break;
						}
						quoteLines.Add(t.Length > 1 && t[1] == ' ' ? t.Substring(2) : (t.Length > 1 ? t.Substring(1) : ""));
						i++;
					}
					blocks.Add(("quote", 0, quoteLines.ToArray()));
					continue;
				}
				// 表格：连续 "|" 开头的行；形如 |---|---| 的分隔线行剔除
				if (trimmed.StartsWith("|", StringComparison.Ordinal))
				{
					var tableRows = new List<string>();
					while (i < lines.Length)
					{
						string t = lines[i].Trim();
						if (!t.StartsWith("|", StringComparison.Ordinal))
						{
							break;
						}
						bool isSeparator = t.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Length == 0;
						if (!isSeparator)
						{
							tableRows.Add(string.Join("\t", SplitTableRow(t)));
						}
						i++;
					}
					if (tableRows.Count > 0)
					{
						blocks.Add(("table", 0, tableRows.ToArray()));
					}
					continue;
				}
				// 列表："- " / "* " 行各成一个块
				if ((trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal)) && trimmed.Length > 2)
				{
					blocks.Add(("bullet", 0, new[] { trimmed.Substring(2).Trim() }));
					i++;
					continue;
				}
				// 普通段落：收集到空行/其它块起点前的连续行
				var paraLines = new List<string>();
				while (i < lines.Length)
				{
					string t = lines[i].Trim();
					bool isBlockStart = t.Length == 0
						|| t.StartsWith("#", StringComparison.Ordinal)
						|| t.StartsWith(">", StringComparison.Ordinal)
						|| t.StartsWith("|", StringComparison.Ordinal)
						|| t.StartsWith("- ", StringComparison.Ordinal)
						|| t.StartsWith("* ", StringComparison.Ordinal)
						|| t.StartsWith("```", StringComparison.Ordinal);
					if (isBlockStart)
					{
						break;
					}
					paraLines.Add(t);
					i++;
				}
				blocks.Add(("para", 0, paraLines.ToArray()));
			}
			return blocks;
		}

		/// <summary>
		/// 行内解析：**加粗** 与 `行内代码` 切分为 (text, bold, code) 片段；未闭合的
		/// 标记按普通文本处理（不出样式也不吞字符）。
		/// </summary>
		public static List<(string Text, bool Bold, bool Code)> ParseInlines(string text)
		{
			var parts = new List<(string, bool, bool)>();
			if (string.IsNullOrEmpty(text))
			{
				return parts;
			}
			int i =  0;
			int plainStart = 0;
			while (i < text.Length)
			{
				if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
				{
					int close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
					if (close > i + 1)
					{
						AppendPlain(parts, text, plainStart, i);
						parts.Add((text.Substring(i + 2, close - i - 2), true, false));
						i = close + 2;
						plainStart = i;
						continue;
					}
				}
				if (text[i] == '`')
				{
					int close = text.IndexOf('`', i + 1);
					if (close > i)
					{
						AppendPlain(parts, text, plainStart, i);
						parts.Add((text.Substring(i + 1, close - i - 1), false, true));
						i = close + 1;
						plainStart = i;
						continue;
					}
				}
				i++;
			}
			AppendPlain(parts, text, plainStart, text.Length);
			return parts;
		}

		private static void AppendPlain(List<(string, bool, bool)> parts, string text, int start, int end)
		{
			if (end > start)
			{
				parts.Add((text.Substring(start, end - start), false, false));
			}
		}

		private static string[] SplitTableRow(string row)
		{
			string body = row.Trim();
			if (body.StartsWith("|", StringComparison.Ordinal))
			{
				body = body.Substring(1);
			}
			if (body.EndsWith("|", StringComparison.Ordinal))
			{
				body = body.Substring(0, body.Length - 1);
			}
			// 单元格两侧空白剔除（渲染与解析口径一致）
			string[] cells = body.Split('|');
			for (int i = 0; i < cells.Length; i++)
			{
				cells[i] = cells[i].Trim();
			}
			return cells;
		}

		// ===== 控件构建（视觉与弹窗现有 13 号正文一致；等宽/代码 12 号） =====

		private static Control BuildBlockControl(string kind, int level, string[] lines, Control resourceHost)
		{
			switch (kind)
			{
				case "heading":
				{
					var tb = new TextBlock
					{
						Text = string.Join(" ", lines),
						FontWeight = FontWeight.Medium,
						FontSize = level <= 2 ? 15 : 14,
						Margin = new Thickness(0, 10, 0, 4),
						TextWrapping = TextWrapping.Wrap
					};
					return tb;
				}
				case "quote":
				{
					var tb = BuildInlineTextBlock(string.Join("\n", lines), 13, resourceHost);
					var border = new Border
					{
						BorderThickness = new Thickness(3, 0, 0, 0),
						Padding = new Thickness(8, 2, 4, 2),
						Margin = new Thickness(0, 2, 0, 4),
						Child = tb
					};
					border.Bind(Border.BorderBrushProperty, resourceHost.GetResourceObservable("BorderBrush"));
					return border;
				}
				case "bullet":
				{
					var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
					grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
					grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
					var bullet = new TextBlock { Text = "•", FontSize = 13 };
					Grid.SetColumn(bullet, 0);
					TextBlock content = BuildInlineTextBlock(string.Join("\n", lines), 13, resourceHost);
					Grid.SetColumn(content, 1);
					grid.Children.Add(bullet);
					grid.Children.Add(content);
					return grid;
				}
				case "table":
				{
					var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
					int columnCount = 0;
					foreach (string row in lines)
					{
						columnCount = Math.Max(columnCount, row.Split('\t').Length);
					}
					for (int c = 0; c < columnCount; c++)
					{
						grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
					}
					for (int r = 0; r < lines.Length; r++)
					{
						grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
						string[] cells = lines[r].Split('\t');
						for (int c = 0; c < cells.Length && c < columnCount; c++)
						{
							bool isHeader = r == 0;
							TextBlock cellText = BuildInlineTextBlock(cells[c].Trim(), 12, resourceHost);
							if (isHeader)
							{
								cellText.FontWeight = FontWeight.Medium;
							}
							Control cell = cellText;
							if (isHeader)
							{
								var cellBorder = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = cellText };
								cellBorder.Bind(Border.BorderBrushProperty, resourceHost.GetResourceObservable("BorderBrush"));
								cell = cellBorder;
							}
							Grid.SetRow(cell, r);
							Grid.SetColumn(cell, c);
							grid.Children.Add(cell);
						}
					}
					return grid;
				}
				case "code":
				{
					var tb = new TextBlock
					{
						Text = string.Join("\n", lines),
						FontFamily = new FontFamily("Consolas"),
						FontSize = 12,
						TextWrapping = TextWrapping.Wrap,
						Margin = new Thickness(0, 4, 0, 4)
					};
					return tb;
				}
				default:
				{
					TextBlock para = BuildInlineTextBlock(string.Join("\n", lines), 13, resourceHost);
					para.Margin = new Thickness(0, 2, 0, 2);
					return para;
				}
			}
		}

		/// <summary>
		/// 构建带行内格式（**加粗** / `代码`）的 TextBlock：始终走 Inlines（无标记文本
		/// 退化为单 Run）；行内代码用 Consolas + Accent 色区分。
		/// </summary>
		private static TextBlock BuildInlineTextBlock(string text, double fontSize, Control resourceHost)
		{
			var tb = new TextBlock { FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
			foreach ((string t, bool bold, bool code) in ParseInlines(text))
			{
				var run = new Run(t);
				if (bold)
				{
					run.FontWeight = FontWeight.Bold;
				}
				if (code)
				{
					run.FontFamily = new FontFamily("Consolas");
					run.Bind(TextElement.ForegroundProperty, resourceHost.GetResourceObservable("AccentColorBrush"));
				}
				tb.Inlines.Add(run);
			}
			return tb;
		}
	}
}
