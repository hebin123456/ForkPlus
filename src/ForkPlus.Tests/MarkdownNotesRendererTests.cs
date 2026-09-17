// 单元 + UI 冒烟（2026-09-17，"更新内容/检查更新弹窗用 markdown 读"）：MarkdownNotesRenderer
// 的块解析（ParseBlocks）/行内解析（ParseInlines）为纯函数，行为在此锁定：
// heading（# 个数）、quote（> 行合并）、bullet（- 前缀剥离）、table（分隔线剔除 + 单元格切分）、
// code 围栏、para；**加粗** / `行内代码` / 未闭合标记按普通文本。Render 把真实版本章节
// 渲染成原生控件（标题 TextBlock / 引用 Border 左条 / 列表 Grid 悬挂缩进 / 表格 Grid /
// 等宽代码块），替代此前只读 TextBox 直显 markdown 源码。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class MarkdownNotesRendererTests
	{
		[Fact]
		public void ParseBlocks_SplitsHeadingQuoteBulletTableCodePara()
		{
			string md = "### 修复\n\n> 摘要第一行\n> 摘要第二行\n\n- **条目标题**：正文\n\n"
				+ "| 平台 | RID | 产物 |\n|------|-----|------|\n| Windows x64 | `win-x64` | zip |\n\n"
				+ "```\ncode line 1\ncode line 2\n```\n\n普通段落第一行\n普通段落第二行";
			var blocks = ForkPlus.UI.Controls.MarkdownNotesRenderer.ParseBlocks(md).ToList();

			Assert.Equal(6, blocks.Count);
			Assert.Equal("heading", blocks[0].Kind);
			Assert.Equal(3, blocks[0].Level);
			Assert.Equal(new[] { "修复" }, blocks[0].Lines);
			Assert.Equal("quote", blocks[1].Kind);
			Assert.Equal(new[] { "摘要第一行", "摘要第二行" }, blocks[1].Lines);
			Assert.Equal("bullet", blocks[2].Kind);
			Assert.Equal(new[] { "**条目标题**：正文" }, blocks[2].Lines);
			Assert.Equal("table", blocks[3].Kind);
			// 分隔线行剔除：表头 + 数据行共 2 行，单元格以 \t 连接
			Assert.Equal(2, blocks[3].Lines.Length);
			Assert.Equal("平台\tRID\t产物", blocks[3].Lines[0]);
			Assert.Equal("Windows x64\t`win-x64`\tzip", blocks[3].Lines[1]);
			Assert.Equal("code", blocks[4].Kind);
			Assert.Equal(new[] { "code line 1", "code line 2" }, blocks[4].Lines);
			Assert.Equal("para", blocks[5].Kind);
			Assert.Equal(new[] { "普通段落第一行", "普通段落第二行" }, blocks[5].Lines);
		}

		[Fact]
		public void ParseInlines_BoldAndCode_UnclosedLiteral()
		{
			var parts = ForkPlus.UI.Controls.MarkdownNotesRenderer.ParseInlines("前缀**加粗**中段`code`后缀**未闭合`").ToList();
			Assert.Equal(5, parts.Count);
			Assert.Equal(("前缀", false, false), (parts[0].Text, parts[0].Bold, parts[0].Code));
			Assert.Equal(("加粗", true, false), (parts[1].Text, parts[1].Bold, parts[1].Code));
			Assert.Equal(("中段", false, false), (parts[2].Text, parts[2].Bold, parts[2].Code));
			Assert.Equal(("code", false, true), (parts[3].Text, parts[3].Bold, parts[3].Code));
			// 未闭合的 ** 不出样式、不吞字符（` 也未闭合 → 整体普通文本）
			Assert.Equal(("后缀**未闭合`", false, false), (parts[4].Text, parts[4].Bold, parts[4].Code));
		}

		[Fact]
		public void Render_BuildsNativeControlsForRealSection()
		{
			string md = "### 修复\n\n- **FileDiff 行间距**：对齐 `win-x64` 平台。\n\n> 摘要引用行\n\n"
				+ "| 平台 | RID | 产物 |\n|------|-----|------|\n| Windows x64 | `win-x64` | zip |\n\n"
				+ "```\ncode line\n```\n\n普通段落文本";
			HeadlessAppBootstrap.Run(delegate
			{
				var panel = new StackPanel();
				var window = new Window { Width = 480, Height = 400, Content = panel };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ForkPlus.UI.Controls.MarkdownNotesRenderer.Render(panel, md);
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				// 块序：heading、bullet、quote、table、code、para
				Assert.Equal(6, panel.Children.Count);

				// heading：Medium 字重、### = 14 号
				var heading = panel.Children[0] as TextBlock;
				Assert.NotNull(heading);
				Assert.Equal("修复", heading.Text);
				Assert.Equal(FontWeight.Medium, heading.FontWeight);
				Assert.Equal(14, heading.FontSize);

				// bullet：悬挂缩进 Grid（• 列 + 内容列），内容 Inlines 含加粗与行内代码 Run
				var bullet = panel.Children[1] as Grid;
				Assert.NotNull(bullet);
				Assert.Equal(2, bullet.ColumnDefinitions.Count);
				TextBlock bulletContent = bullet.Children.OfType<TextBlock>()
					.FirstOrDefault(delegate (TextBlock tb) { return tb.Inlines != null && tb.Inlines.Count > 1; });
				Assert.NotNull(bulletContent);
				Run boldRun = bulletContent.Inlines.OfType<Run>()
					.FirstOrDefault(delegate (Run r) { return r.FontWeight == FontWeight.Bold; });
				Assert.NotNull(boldRun);
				Assert.Equal("FileDiff 行间距", boldRun.Text);
				Run codeRun = bulletContent.Inlines.OfType<Run>()
					.FirstOrDefault(delegate (Run r) { return r.FontFamily != null && r.FontFamily.FamilyNames.Contains("Consolas"); });
				Assert.NotNull(codeRun);
				Assert.Equal("win-x64", codeRun.Text);

				// quote：左侧 3px 竖条 Border
				var quote = panel.Children[2] as Border;
				Assert.NotNull(quote);
				Assert.Equal(3, quote.BorderThickness.Left);
				TextBlock quoteText = quote.Child as TextBlock;
				Assert.NotNull(quoteText);
				Assert.Equal("摘要引用行", string.Join("", quoteText.Inlines.OfType<Run>().Select(delegate (Run r) { return r.Text; })));

				// table：分隔线剔除后 2 行 × 3 列；表头单元格带底边框
				var table = panel.Children[3] as Grid;
				Assert.NotNull(table);
				Assert.Equal(2, table.RowDefinitions.Count);
				Assert.Equal(3, table.ColumnDefinitions.Count);
				Assert.Contains(table.Children.OfType<Border>(),
					delegate (Border b) { return b.BorderThickness.Bottom == 1; });

				// code：等宽字体原文
				var code = panel.Children[4] as TextBlock;
				Assert.NotNull(code);
				Assert.Equal("code line", code.Text);
				Assert.True(code.FontFamily.FamilyNames.Contains("Consolas"));

				// para：Inlines 单 Run 保留原文
				var para = panel.Children[5] as TextBlock;
				Assert.NotNull(para);
				Assert.Equal("普通段落文本", string.Join("", para.Inlines.OfType<Run>().Select(delegate (Run r) { return r.Text; })));

				window.Close();
				Dispatcher.UIThread.RunJobs();
			});
		}
	}
}
