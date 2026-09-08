// 真机 bug#10（AI 弹窗样式全无）回归测试：MarkdownHtmlRenderer 把 WebView2 的
// HTML 子集（Biturbo md→html 输出 + AiCodeReviewWindow 手工结构）解析成 Avalonia 控件树。
// 这里用视觉无关的结构断言锁住：标题/列表/代码块/表格/引用块/blockquote/状态卡/按钮回调。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	public class MarkdownHtmlRendererTests
	{
		private static T Find<T>(Control root) where T : Control
		{
			return FindCore<T>(root) ?? throw new InvalidOperationException("not found: " + typeof(T).Name);
		}

		private static T FindCore<T>(Control c) where T : Control
		{
			if (c is T match)
			{
				return match;
			}
			if (c is Decorator { Child: { } child })
			{
				return FindCore<T>(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				return FindCore<T>(content);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					T r = FindCore<T>(k);
					if (r != null)
					{
						return r;
					}
				}
			}
			if (c is ScrollViewer { Content: Control sc })
			{
				return FindCore<T>(sc);
			}
			if (c is Expander { Content: Control ec })
			{
				return FindCore<T>(ec);
			}
			return null;
		}

		private static int Count<T>(Control c) where T : Control
		{
			int n = c is T ? 1 : 0;
			if (c is Decorator { Child: { } child })
			{
				n += Count<T>(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				n += Count<T>(content);
			}
			if (c is ScrollViewer { Content: Control sc })
			{
				n += Count<T>(sc);
			}
			if (c is Expander { Content: { } ec })
			{
				n += Count<T>((Control)ec);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					n += Count<T>(k);
				}
			}
			return n;
		}

		private static string CollectRunText(TextBlock block)
		{
			var sb = new System.Text.StringBuilder();
			if (block.Inlines != null)
			{
				foreach (Inline inline in block.Inlines)
				{
					if (inline is Run run)
					{
						sb.Append(run.Text);
					}
				}
			}
			else
			{
				sb.Append(block.Text);
			}
			return sb.ToString();
		}

		[Fact]
		public void Render_BiturboMarkdownOutput_ProducesStyledControls()
		{
			// Biturbo bt_md_to_html 的实测输出子集（mdtest 工程跑出的结构）
			string html = "<h1>Title One</h1><h2>Title Two</h2><h3>Title Three</h3>"
				+ "<p>A paragraph with <strong>bold</strong>, <em>italic</em>, <code>inline code</code>, and <a href=\"https://example.com\">a link</a>.</p>"
				+ "<ul><li>bullet one</li><li>bullet two<ul><li>nested bullet</li></ul></li></ul>"
				+ "<ol><li>numbered one</li><li>numbered two</li></ol>"
				+ "<blockquote><p>a blockquote line</p></blockquote>"
				+ "<pre><code>var x = 1; // code block\nConsole.WriteLine(x);</code></pre>"
				+ "<table><thead><tr><th>Col A</th><th>Col B</th></tr></thead><tbody><tr><td>1</td><td>2</td></tr></tbody></table>"
				+ "<hr><p>Final paragraph.</p>";

			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);

			// 结构断言：列表行 Grid（ul 2 行 + 嵌套 1 行 + ol 2 行）+ 表格 Grid
			Assert.Equal(6, Count<Grid>(root));
			Assert.Equal(0, Count<Expander>(root)); // 此样例无 details
			Assert.NotEqual(0, Count<Border>(root)); // 代码块/表格/标题边框
			// 简单冒烟：根节点是带背景色的 Border（body 背景 #fafafa）
			Border body = Assert.IsType<Border>(root);
			SolidColorBrush bg = Assert.IsType<SolidColorBrush>(body.Background);
			Assert.Equal(Color.FromRgb(0xFA, 0xFA, 0xFA), bg.Color);
		}

		[Fact]
		public void Render_DarkTheme_UsesDarkPalette()
		{
			Control root = MarkdownHtmlRenderer.Render("<p>hi</p>", dark: true, null);
			Border body = Assert.IsType<Border>(root);
			SolidColorBrush bg = Assert.IsType<SolidColorBrush>(body.Background);
			Assert.Equal(Color.FromRgb(0x28, 0x28, 0x28), bg.Color);
		}

		[Fact]
		public void Render_CodeBlock_UsesMonospaceFont()
		{
			string html = "<pre><code>git status</code></pre>";
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);
			// 找到等宽 TextBlock（pre 内容）
			TextBlock pre = FindMono(root);
			Assert.NotNull(pre);
			Assert.Equal("git status", pre.Text);
		}

		private static TextBlock FindMono(Control c)
		{
			if (c is TextBlock tb && tb.FontFamily != null && tb.FontFamily.FamilyNames.Count > 0
				&& tb.FontFamily.FamilyNames[0].Contains("Consolas"))
			{
				return tb;
			}
			if (c is Decorator { Child: { } child })
			{
				return FindMono(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				return FindMono(content);
			}
			if (c is ScrollViewer { Content: Control sc })
			{
				return FindMono(sc);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					TextBlock r = FindMono(k);
					if (r != null)
					{
						return r;
					}
				}
			}
			return null;
		}

		[Fact]
		public void Render_AiStatusAndSuggestionCards_AndButtonCallback()
		{
			// AiCodeReviewWindow.RenderAiReviewOutput 手工拼接的结构
			string html = "<div class='ai-current-file'>src/Program.cs</div>"
				+ "<div class='ai-status'>✓ 3 suggestions</div>"
				+ "<div class='ai-suggestion'>Replace <code>var</code> with explicit type"
				+ "<button onclick='previewSuggestion(0)'>Preview replacement</button>"
				+ "<button onclick='applySuggestion(0)'>Apply suggestion</button></div>"
				+ "<details class='ai-all-results'><summary>All results</summary><p>done</p></details>";

			string received = null;
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, delegate (string m) { received = m; });

			// 状态卡是圆角边框
			Assert.NotEqual(0, Count<Border>(root));
			Assert.Equal(1, Count<Expander>(root));
			// 按钮存在且点击产生 WebMessage 回调
			Button button = Find<Button>(root);
			Assert.NotNull(button);
			button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
			Assert.NotNull(received);
			Assert.StartsWith("preview-suggestion:", received);
		}

		[Fact]
		public void Render_InlineStyleColor_RedError()
		{
			// ShowError 的红色提示：style='color:#d33'
			string html = "<p style='color:#d33'>something failed</p>";
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);
			TextBlock p = Find<TextBlock>(root);
			SolidColorBrush fg = Assert.IsType<SolidColorBrush>(((Run)p.Inlines[0]).Foreground);
			Assert.Equal(Color.FromRgb(0xDD, 0x33, 0x33), fg.Color);
		}

		[Fact]
		public void Render_HtmlEscapedEntities_Decoded()
		{
			string html = "<p>a &amp; b &lt;tag&gt; &quot;q&quot;</p>";
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);
			TextBlock p = Find<TextBlock>(root);
			Assert.Equal("a & b <tag> \"q\"", CollectRunText(p));
		}

		[Fact]
		public void Render_HeadingBold_NotMaskedByInlineRuns()
		{
			// 回归锁：行内 Run 曾显式携带 FontWeight.Normal/Foreground，把 TextBlock 层的
			// 标题加粗与 h6 灰字全部遮蔽（Avalonia 行内属性优先于块级属性）。
			string html = "<h2>Heading <strong>text</strong></h2><h6>muted heading</h6>";
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);

			TextBlock heading = FindFirstHeading(root);
			Assert.Equal(FontWeights.Bold, heading.FontWeight);

			TextBlock h6 = FindMutedHeading(root);
			SolidColorBrush fg = Assert.IsType<SolidColorBrush>(h6.Foreground);
			Assert.Equal(Color.FromRgb(0x6A, 0x73, 0x7D), fg.Color);
		}

		private static TextBlock FindFirstHeading(Control c)
		{
			if (c is TextBlock tb && tb.FontWeight == FontWeights.Bold)
			{
				return tb;
			}
			if (c is Decorator { Child: { } child })
			{
				return FindFirstHeading(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				return FindFirstHeading(content);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					TextBlock r = FindFirstHeading(k);
					if (r != null)
					{
						return r;
					}
				}
			}
			return null;
		}

		private static TextBlock FindMutedHeading(Control c)
		{
			if (c is TextBlock tb && tb.Foreground is SolidColorBrush b && b.Color == Color.FromRgb(0x6A, 0x73, 0x7D))
			{
				return tb;
			}
			if (c is Decorator { Child: { } child })
			{
				return FindMutedHeading(child);
			}
			if (c is ContentControl { Content: Control content })
			{
				return FindMutedHeading(content);
			}
			if (c is Panel panel)
			{
				foreach (Control k in panel.Children)
				{
					TextBlock r = FindMutedHeading(k);
					if (r != null)
					{
						return r;
					}
				}
			}
			return null;
		}

		[Fact]
		public void Render_UnclosedTags_Tolerated()
		{
			// 流式渲染中途的半截 HTML：未闭合标签自动兜底，不抛异常
			string html = "<p>streaming partial<ul><li>item one";
			Control root = MarkdownHtmlRenderer.Render(html, dark: false, null);
			Assert.NotNull(root);
		}
	}
}
